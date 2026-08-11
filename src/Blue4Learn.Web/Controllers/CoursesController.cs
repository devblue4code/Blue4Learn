using System.ComponentModel.DataAnnotations;
using Blue4Learn.Web.Data;
using Blue4Learn.Web.Domain;
using Blue4Learn.Web.Services;
using Blue4Learn.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Blue4Learn.Web.Controllers;

[Authorize]
public class CoursesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IAccessService _access;
    private readonly ILearningContextService _context;
    private readonly IMarkdownService _markdown;

    public CoursesController(
        ApplicationDbContext db,
        IAccessService access,
        ILearningContextService context,
        IMarkdownService markdown)
    {
        _db = db;
        _access = access;
        _context = context;
        _markdown = markdown;
    }

    [HttpGet]
    public async Task<IActionResult> Syllabus(Guid? courseId = null, Guid? classId = null)
    {
        var user = await _access.GetCurrentUserAsync(User);
        if (user is null) return Challenge();

        if (classId is Guid cid)
        {
            var cls = await _db.ClassGroups.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == cid);
            if (cls is not null && await _access.CanAccessCourseAsync(user, cls.CourseId))
            {
                var canSeeClass = await _access.CanManageClassAsync(user, cls.Id)
                    || await _db.Enrollments.AnyAsync(e => e.ClassGroupId == cls.Id && e.UserId == user.Id);
                if (canSeeClass)
                {
                    _context.SetCourse(cls.CourseId, cls.Id);
                }
            }
        }

        var course = await _context.ResolveCourseAsync(user, courseId);
        var classGroup = await _context.ResolveClassAsync(user, classId);

        if (course is null)
        {
            return View(new SyllabusViewModel
            {
                CourseTitle = "Disciplina",
                Description = "Nenhuma disciplina vinculada à sua conta ainda."
            });
        }

        var modules = await _db.Modules
            .AsNoTracking()
            .Where(m => m.CourseId == course.Id)
            .OrderBy(m => m.SortOrder)
            .Select(m => new SyllabusModuleViewModel
            {
                Id = m.Id,
                Title = m.Title,
                SortOrder = m.SortOrder,
                Lessons = m.Lessons
                    .OrderBy(l => l.SortOrder)
                    .Select(l => new SyllabusLessonViewModel
                    {
                        Id = l.Id,
                        Title = l.Title,
                        Objective = l.Objective,
                        SortOrder = l.SortOrder,
                        Status = l.Status
                    })
                    .ToList()
            })
            .ToListAsync();

        var syllabusMarkdown = !string.IsNullOrWhiteSpace(course.Syllabus)
            ? course.Syllabus
            : course.Description;

        return View(new SyllabusViewModel
        {
            CourseId = course.Id,
            CourseTitle = course.Title,
            Description = string.IsNullOrWhiteSpace(course.Description)
                ? "Ementa ainda não detalhada para esta disciplina."
                : course.Description,
            SyllabusHtml = _markdown.ToSafeHtml(syllabusMarkdown),
            MethodologiesHtml = _markdown.ToSafeHtml(course.Methodologies),
            ClassName = classGroup?.Name,
            ClassCode = classGroup?.Code,
            Modules = modules
        });
    }

    [Authorize(Roles = $"{AppRoles.Teacher},{AppRoles.Admin}")]
    [HttpGet]
    public async Task<IActionResult> Index(Guid? courseId = null)
    {
        var user = await _access.GetCurrentUserAsync(User);
        if (user is null) return Challenge();
        if (!await _access.CanManageContentAsync(user)) return Forbid();

        var course = await _context.ResolveCourseAsync(user, courseId);

        if (course is null)
        {
            return View(new CourseComponentsViewModel
            {
                CourseTitle = "Disciplina"
            });
        }

        var modules = await _db.Modules
            .AsNoTracking()
            .Where(m => m.CourseId == course.Id)
            .OrderBy(m => m.SortOrder)
            .Select(m => new ModuleEditorItemViewModel
            {
                Id = m.Id,
                Title = m.Title,
                SortOrder = m.SortOrder,
                LessonCount = m.Lessons.Count,
                Lessons = m.Lessons
                    .OrderBy(l => l.SortOrder)
                    .Select(l => new SyllabusLessonViewModel
                    {
                        Id = l.Id,
                        Title = l.Title,
                        Objective = l.Objective,
                        SortOrder = l.SortOrder,
                        Status = l.Status
                    })
                    .ToList()
            })
            .ToListAsync();

        return View(new CourseComponentsViewModel
        {
            CourseId = course.Id,
            CourseTitle = course.Title,
            Modules = modules,
            NewModule = new ModuleCreateViewModel { CourseId = course.Id }
        });
    }

    [Authorize(Roles = $"{AppRoles.Teacher},{AppRoles.Admin}")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateModule([Bind(Prefix = "NewModule")] ModuleCreateViewModel model)
    {
        var user = await _access.GetCurrentUserAsync(User);
        if (user is null) return Challenge();
        if (!await _access.CanManageContentAsync(user)) return Forbid();
        if (!await _access.CanAccessCourseAsync(user, model.CourseId)) return Forbid();

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Informe um título válido para o módulo.";
            return RedirectToAction(nameof(Index));
        }

        var maxOrder = await _db.Modules
            .Where(m => m.CourseId == model.CourseId)
            .Select(m => (int?)m.SortOrder)
            .MaxAsync() ?? 0;

        _db.Modules.Add(new Module
        {
            CourseId = model.CourseId,
            Title = model.Title.Trim(),
            SortOrder = maxOrder + 1
        });
        await _db.SaveChangesAsync();
        TempData["Success"] = "Módulo criado.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = $"{AppRoles.Teacher},{AppRoles.Admin}")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RenameModule(Guid id, [Required, MaxLength(200)] string title)
    {
        var user = await _access.GetCurrentUserAsync(User);
        if (user is null) return Challenge();
        if (!await _access.CanManageContentAsync(user)) return Forbid();

        var module = await _db.Modules.Include(m => m.Course).FirstOrDefaultAsync(m => m.Id == id);
        if (module is null) return NotFound();
        if (!await _access.CanAccessCourseAsync(user, module.CourseId)) return Forbid();

        if (string.IsNullOrWhiteSpace(title))
        {
            TempData["Error"] = "Título inválido.";
            return RedirectToAction(nameof(Index));
        }

        module.Title = title.Trim();
        await _db.SaveChangesAsync();
        TempData["Success"] = "Módulo atualizado.";
        return RedirectToAction(nameof(Index));
    }
}
