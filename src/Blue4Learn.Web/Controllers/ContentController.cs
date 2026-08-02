using Blue4Learn.Web.Data;
using Blue4Learn.Web.Domain;
using Blue4Learn.Web.Services;
using Blue4Learn.Web.Services.Ai;
using Blue4Learn.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Blue4Learn.Web.Controllers;

[Authorize(Roles = $"{AppRoles.Teacher},{AppRoles.Admin}")]
public class ContentController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IMarkdownService _markdown;
    private readonly IAccessService _access;
    private readonly IAiTutorService _ai;

    public ContentController(
        ApplicationDbContext db,
        IMarkdownService markdown,
        IAccessService access,
        IAiTutorService ai)
    {
        _db = db;
        _markdown = markdown;
        _access = access;
        _ai = ai;
    }

    public async Task<IActionResult> Index()
    {
        var user = await _access.GetCurrentUserAsync(User);
        if (user is null) return Challenge();
        if (!await _access.CanManageContentAsync(user)) return Forbid();

        var courseIds = await _access.GetAccessibleCourseIdsAsync(user);
        var course = await _db.Courses.AsNoTracking()
            .Where(c => courseIds.Contains(c.Id))
            .OrderBy(c => c.Title)
            .FirstOrDefaultAsync();

        var lessons = await _db.Lessons
            .AsNoTracking()
            .Where(l => courseIds.Contains(l.Module.CourseId))
            .OrderBy(l => l.Module.SortOrder)
            .ThenBy(l => l.SortOrder)
            .Select(l => new ContentLessonItemViewModel
            {
                Id = l.Id,
                Title = l.Title,
                Slug = l.Slug,
                ModuleTitle = l.Module.Title,
                SortOrder = l.SortOrder,
                Status = l.Status,
                UpdatedAtUtc = l.ContentDocument != null ? l.ContentDocument.UpdatedAtUtc : null,
                ConceptCount = l.Concepts.Count,
                ConceptNames = l.Concepts.Select(c => c.Name).ToList()
            })
            .ToListAsync();

        foreach (var lesson in lessons)
        {
            lesson.ConceptNames = lesson.ConceptNames.OrderBy(n => n).ToList();
        }

        return View(new ContentLibraryViewModel
        {
            CourseTitle = course?.Title ?? "Disciplina",
            Lessons = lessons
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var user = await _access.GetCurrentUserAsync(User);
        if (user is null) return Challenge();
        if (!await _access.CanManageContentAsync(user)) return Forbid();

        var model = await BuildEditorAsync(user, new LessonEditorViewModel
        {
            Markdown = """
                       # Nova aula

                       Escreva o conteúdo em Markdown.

                       ## Exemplo

                       ```html
                       <h1>Olá, Blue4Learn</h1>
                       ```

                       > **Dica:** use o preview ao lado para validar a leitura.
                       """,
            ActivityPrompt = "Descreva o problema, a solução e anexe o link do repositório.",
            Status = ContentStatus.Draft
        });

        return View("Edit", model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        var user = await _access.GetCurrentUserAsync(User);
        if (user is null) return Challenge();
        if (!await _access.CanManageContentAsync(user) || !await _access.CanAccessLessonAsync(user, id))
        {
            return Forbid();
        }

        var lesson = await _db.Lessons
            .Include(l => l.ContentDocument)
            .Include(l => l.Concepts)
            .Include(l => l.Activities)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (lesson is null) return NotFound();

        var model = await BuildEditorAsync(user, new LessonEditorViewModel
        {
            Id = lesson.Id,
            ModuleId = lesson.ModuleId,
            Title = lesson.Title,
            Slug = lesson.Slug,
            Objective = lesson.Objective,
            SortOrder = lesson.SortOrder,
            Status = lesson.Status,
            Markdown = lesson.ContentDocument?.Markdown ?? string.Empty,
            ConceptsText = string.Join(Environment.NewLine, lesson.Concepts.Select(c => c.Name)),
            ActivityPrompt = lesson.Activities.OrderBy(a => a.Title).FirstOrDefault()?.Prompt,
            PreviewHtml = _markdown.ToSafeHtml(lesson.ContentDocument?.Markdown)
        });

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(LessonEditorViewModel model)
    {
        var user = await _access.GetCurrentUserAsync(User);
        if (user is null) return Challenge();
        if (!await _access.CanManageContentAsync(user)) return Forbid();

        await ApplyMarkdownUploadAsync(model);
        model.Modules = await LoadModulesAsync(user);

        if (string.IsNullOrWhiteSpace(model.Slug))
        {
            model.Slug = SlugHelper.FromTitle(model.Title);
        }
        else
        {
            model.Slug = SlugHelper.FromTitle(model.Slug);
        }

        var moduleCourseId = await _db.Modules
            .Where(m => m.Id == model.ModuleId)
            .Select(m => (Guid?)m.CourseId)
            .FirstOrDefaultAsync();
        if (moduleCourseId is null || !await _access.CanAccessCourseAsync(user, moduleCourseId.Value))
        {
            ModelState.AddModelError(nameof(model.ModuleId), "Módulo inválido.");
        }

        if (model.Id is Guid existingId && !await _access.CanAccessLessonAsync(user, existingId))
        {
            return Forbid();
        }

        var slugTaken = await _db.Lessons.AnyAsync(l =>
            l.ModuleId == model.ModuleId &&
            l.Slug == model.Slug &&
            (!model.Id.HasValue || l.Id != model.Id.Value));

        if (slugTaken)
        {
            ModelState.AddModelError(nameof(model.Slug), "Já existe uma aula com este slug neste módulo.");
        }

        if (!ModelState.IsValid)
        {
            model.PreviewHtml = _markdown.ToSafeHtml(model.Markdown);
            return View("Edit", model);
        }

        Lesson lesson;
        if (model.Id is Guid id)
        {
            lesson = await _db.Lessons
                .Include(l => l.ContentDocument)
                .Include(l => l.Concepts)
                .Include(l => l.Activities)
                .FirstOrDefaultAsync(l => l.Id == id)
                ?? throw new InvalidOperationException("Aula não encontrada.");
        }
        else
        {
            lesson = new Lesson();
            _db.Lessons.Add(lesson);
        }

        lesson.ModuleId = model.ModuleId;
        lesson.Title = model.Title.Trim();
        lesson.Slug = model.Slug!;
        lesson.Objective = model.Objective.Trim();
        lesson.SortOrder = model.SortOrder;
        lesson.Status = model.Status;

        if (lesson.ContentDocument is null)
        {
            lesson.ContentDocument = new ContentDocument();
        }

        lesson.ContentDocument.Title = lesson.Title;
        lesson.ContentDocument.Markdown = model.Markdown;
        lesson.ContentDocument.UpdatedAtUtc = DateTime.UtcNow;

        SyncConcepts(lesson, model.ConceptsText);
        SyncActivity(lesson, model.ActivityPrompt);

        await _db.SaveChangesAsync();

        TempData["Success"] = model.Status == ContentStatus.Published
            ? "Aula salva e publicada. Os estudantes já podem estudar."
            : "Aula salva como rascunho.";

        return RedirectToAction(nameof(Edit), new { id = lesson.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetStatus(Guid id, ContentStatus status)
    {
        var user = await _access.GetCurrentUserAsync(User);
        if (user is null) return Challenge();
        if (!await _access.CanManageContentAsync(user) || !await _access.CanAccessLessonAsync(user, id))
        {
            return Forbid();
        }

        if (status is not (ContentStatus.Draft or ContentStatus.Published or ContentStatus.Archived))
        {
            return BadRequest();
        }

        var lesson = await _db.Lessons
            .Include(l => l.ContentDocument)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (lesson is null) return NotFound();

        if (status == ContentStatus.Published &&
            string.IsNullOrWhiteSpace(lesson.ContentDocument?.Markdown))
        {
            TempData["Error"] = "Não é possível publicar uma aula sem conteúdo Markdown.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        lesson.Status = status;
        if (lesson.ContentDocument is not null)
        {
            lesson.ContentDocument.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        TempData["Success"] = status switch
        {
            ContentStatus.Published => "Aula publicada.",
            ContentStatus.Archived => "Aula arquivada.",
            _ => "Aula movida para rascunho."
        };

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Preview([FromForm] MarkdownPreviewRequest request)
    {
        var html = _markdown.ToSafeHtml(request.Markdown);
        return PartialView("_MarkdownPreview", html);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SuggestConcepts(
        [FromForm] SuggestConceptsRequest request,
        CancellationToken cancellationToken)
    {
        var user = await _access.GetCurrentUserAsync(User);
        if (user is null) return Challenge();
        if (!await _access.CanManageContentAsync(user)) return Forbid();
        if (!_ai.IsEnabled)
        {
            return Json(new { concepts = Array.Empty<string>(), message = "IA desabilitada." });
        }

        var concepts = await _ai.SuggestConceptsAsync(request.Markdown, request.Objective, cancellationToken);
        return Json(new { concepts });
    }

    private async Task<LessonEditorViewModel> BuildEditorAsync(ApplicationUser user, LessonEditorViewModel model)
    {
        model.Modules = await LoadModulesAsync(user);
        model.AiEnabled = _ai.IsEnabled;
        if (model.ModuleId == Guid.Empty && model.Modules.Count > 0)
        {
            model.ModuleId = model.Modules[0].Id;
        }

        if (model.SortOrder < 1)
        {
            var maxOrder = await _db.Lessons
                .Where(l => l.ModuleId == model.ModuleId)
                .Select(l => (int?)l.SortOrder)
                .MaxAsync() ?? 0;
            model.SortOrder = maxOrder + 1;
        }

        model.PreviewHtml ??= _markdown.ToSafeHtml(model.Markdown);
        return model;
    }

    private async Task<IReadOnlyList<ModuleOptionViewModel>> LoadModulesAsync(ApplicationUser user)
    {
        var courseIds = await _access.GetAccessibleCourseIdsAsync(user);
        return await _db.Modules
            .AsNoTracking()
            .Where(m => courseIds.Contains(m.CourseId))
            .OrderBy(m => m.SortOrder)
            .Select(m => new ModuleOptionViewModel { Id = m.Id, Title = m.Title })
            .ToListAsync();
    }

    private static async Task ApplyMarkdownUploadAsync(LessonEditorViewModel model)
    {
        if (model.MarkdownFile is null || model.MarkdownFile.Length == 0)
        {
            return;
        }

        var fileName = model.MarkdownFile.FileName;
        if (!fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase) &&
            !fileName.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (model.MarkdownFile.Length > 512 * 1024)
        {
            return;
        }

        using var reader = new StreamReader(model.MarkdownFile.OpenReadStream());
        var content = await reader.ReadToEndAsync();
        if (!string.IsNullOrWhiteSpace(content))
        {
            model.Markdown = content;
        }
    }

    private static void SyncConcepts(Lesson lesson, string? conceptsText)
    {
        var names = (conceptsText ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(n => n.Trim())
            .Where(n => n.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var toRemove = lesson.Concepts
            .Where(c => !names.Any(n => string.Equals(n, c.Name, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        foreach (var concept in toRemove)
        {
            lesson.Concepts.Remove(concept);
        }

        // Marks are cascade-deleted with Concept (see ApplicationDbContext).

        foreach (var name in names)
        {
            if (!lesson.Concepts.Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                lesson.Concepts.Add(new Concept
                {
                    Name = name,
                    Description = $"Conceito: {name}"
                });
            }
        }
    }

    private static void SyncActivity(Lesson lesson, string? prompt)
    {
        var activity = lesson.Activities.OrderBy(a => a.Title).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return;
        }

        if (activity is null)
        {
            lesson.Activities.Add(new Domain.Activity
            {
                Title = $"Atividade — {lesson.Title}",
                Prompt = prompt.Trim(),
                DueAtUtc = DateTime.UtcNow.AddDays(7)
            });
            return;
        }

        activity.Title = $"Atividade — {lesson.Title}";
        activity.Prompt = prompt.Trim();
    }
}
