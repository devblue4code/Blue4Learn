using Blue4Learn.Web.Data;
using Blue4Learn.Web.Domain;
using Blue4Learn.Web.Services;
using Blue4Learn.Web.Services.Ai;
using Blue4Learn.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Blue4Learn.Web.Controllers;

[Authorize]
public class LessonsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IAccessService _access;
    private readonly IMarkdownService _markdown;
    private readonly IFileStorageService _files;
    private readonly IAiTutorService _ai;

    public LessonsController(
        ApplicationDbContext db,
        IAccessService access,
        IMarkdownService markdown,
        IFileStorageService files,
        IAiTutorService ai)
    {
        _db = db;
        _access = access;
        _markdown = markdown;
        _files = files;
        _ai = ai;
    }

    public async Task<IActionResult> Index()
    {
        var user = await _access.GetCurrentUserAsync(User);
        if (user is null) return Challenge();

        var classGroupIds = await _access.GetAccessibleClassGroupIdsAsync(user);
        var lessons = await _db.Lessons
            .AsNoTracking()
            .Include(l => l.Module)
            .Include(l => l.JournalEntries)
            .Where(l => l.Status == ContentStatus.Published && classGroupIds.Contains(l.ClassGroupId))
            .OrderBy(l => l.SortOrder)
            .Select(l => new LessonSummaryViewModel
            {
                Id = l.Id,
                Title = l.Title,
                ModuleTitle = l.Module.Title,
                Objective = l.Objective,
                SortOrder = l.SortOrder,
                HasJournal = l.JournalEntries.Any(j => j.UserId == user.Id)
            })
            .ToListAsync();

        return View(lessons);
    }

    public Task<IActionResult> Workspace(Guid id) =>
        LessonPageAsync(id, "content", "Workspace");

    public Task<IActionResult> Read(Guid id) =>
        LessonPageAsync(id, "content", "Read");

    public Task<IActionResult> Journal(Guid id) =>
        LessonPageAsync(id, "journal", "Journal");

    public Task<IActionResult> Activity(Guid id) =>
        LessonPageAsync(id, "activity", "Activity");

    public Task<IActionResult> Evidence(Guid id) =>
        LessonPageAsync(id, "evidence", "Evidence");

    private async Task<IActionResult> LessonPageAsync(Guid id, string section, string viewName)
    {
        var (error, vm) = await TryLoadWorkspaceAsync(id);
        if (error is not null) return error;
        ViewData["WorkspaceSection"] = section;
        ViewData["LessonId"] = id;
        return View(viewName, vm);
    }

    private async Task<(IActionResult? Error, LessonWorkspaceViewModel? Vm)> TryLoadWorkspaceAsync(Guid id)
    {
        var user = await _access.GetCurrentUserAsync(User);
        if (user is null) return (Challenge(), null);

        if (!await _access.CanAccessLessonAsync(user, id))
        {
            return (Forbid(), null);
        }

        var canPreviewDrafts = await _access.IsTeacherOrAdminAsync(user);

        var lesson = await _db.Lessons
            .Include(l => l.Module).ThenInclude(m => m.Course)
            .Include(l => l.ContentDocument)
            .Include(l => l.Concepts)
            .Include(l => l.Activities)
            .FirstOrDefaultAsync(l =>
                l.Id == id &&
                (l.Status == ContentStatus.Published || canPreviewDrafts));

        if (lesson is null) return (NotFound(), null);

        var siblings = await _db.Lessons
            .AsNoTracking()
            .Where(l =>
                l.ModuleId == lesson.ModuleId &&
                l.ClassGroupId == lesson.ClassGroupId &&
                (l.Status == ContentStatus.Published || canPreviewDrafts && l.Status != ContentStatus.Archived))
            .OrderBy(l => l.SortOrder)
            .Select(l => new { l.Id, l.SortOrder })
            .ToListAsync();

        var journal = await _db.StudentJournalEntries
            .Include(j => j.Questions)
            .Include(j => j.ConceptMarks)
            .FirstOrDefaultAsync(j => j.LessonId == id && j.UserId == user.Id);

        var activity = lesson.Activities.OrderBy(a => a.Title).FirstOrDefault();
        ActivitySubmission? submission = null;
        if (activity is not null)
        {
            submission = await _db.ActivitySubmissions
                .Include(s => s.Attachments)
                .FirstOrDefaultAsync(s => s.ActivityId == activity.Id && s.UserId == user.Id);
        }

        var publishedCount = siblings.Count;
        var journalCount = await _db.StudentJournalEntries
            .CountAsync(j => j.UserId == user.Id && siblings.Select(s => s.Id).Contains(j.LessonId));

        var vm = new LessonWorkspaceViewModel
        {
            LessonId = lesson.Id,
            Title = lesson.Title,
            Objective = lesson.Objective,
            ModuleTitle = lesson.Module.Title,
            CourseTitle = lesson.Module.Course.Title,
            ContentHtml = _markdown.ToSafeHtml(lesson.ContentDocument?.Markdown),
            PreviousLessonId = siblings.LastOrDefault(s => s.SortOrder < lesson.SortOrder)?.Id,
            NextLessonId = siblings.FirstOrDefault(s => s.SortOrder > lesson.SortOrder)?.Id,
            ProgressPercent = publishedCount == 0 ? 0 : (int)Math.Round(100.0 * journalCount / publishedCount),
            ModuleLessonCount = publishedCount,
            RegisteredInModule = journalCount,
            HasJournal = journal is not null,
            SortOrder = lesson.SortOrder,
            Concepts = lesson.Concepts.Select(c =>
            {
                var mark = journal?.ConceptMarks.FirstOrDefault(m => m.ConceptId == c.Id);
                return new ConceptOptionViewModel
                {
                    Id = c.Id,
                    Name = c.Name,
                    Marked = mark is not null,
                    Understood = mark?.Understood ?? false
                };
            }).ToList(),
            Journal = new JournalFormViewModel
            {
                LessonId = lesson.Id,
                Note = journal?.Note ?? string.Empty,
                Reflection = journal?.Reflection ?? string.Empty,
                UnderstoodObjective = journal?.UnderstoodObjective ?? false,
                PracticedConcept = journal?.PracticedConcept ?? false,
                NeedsReview = journal?.NeedsReview ?? false,
                MarkedConceptIds = journal?.ConceptMarks.Select(m => m.ConceptId).ToList() ?? [],
                UnderstoodConceptIds = journal?.ConceptMarks.Where(m => m.Understood).Select(m => m.ConceptId).ToList() ?? [],
                Questions = journal?.Questions
                    .OrderByDescending(q => q.CreatedAtUtc)
                    .Select(q => new QuestionItemViewModel
                    {
                        Id = q.Id,
                        Text = q.Text,
                        Status = q.Status,
                        CreatedAtUtc = q.CreatedAtUtc
                    }).ToList() ?? []
            },
            Activity = activity is null ? null : new ActivityFormViewModel
            {
                ActivityId = activity.Id,
                Title = activity.Title,
                Prompt = activity.Prompt,
                PromptHtml = _markdown.ToSafeHtml(activity.Prompt),
                DueAtUtc = activity.DueAtUtc,
                RequiresGitHubDelivery = activity.RequiresGitHubDelivery,
                ProblemDescription = submission?.ProblemDescription ?? string.Empty,
                SolutionDescription = submission?.SolutionDescription ?? string.Empty,
                TextResponse = submission?.TextResponse ?? string.Empty,
                GitHubUrl = submission?.GitHubUrl,
                GitHubPrUrl = submission?.GitHubPrUrl,
                DeliveryNote = submission?.DeliveryNote,
                Status = submission?.Status ?? ActivityStatus.NotStarted,
                TeacherFeedback = submission?.TeacherFeedback,
                Attachments = submission?.Attachments
                    .OrderByDescending(a => a.UploadedAtUtc)
                    .Select(a => new AttachmentItemViewModel
                    {
                        Id = a.Id,
                        FileName = a.OriginalFileName,
                        SizeBytes = a.SizeBytes
                    }).ToList() ?? []
            }
        };

        ApplyNextStep(vm);
        return (null, vm);
    }

    private async Task<IActionResult> ReturnActivityPageWithPostedValuesAsync(
        Guid lessonId,
        string? returnTo,
        ActivityFormViewModel posted)
    {
        var (error, vm) = await TryLoadWorkspaceAsync(lessonId);
        if (error is not null) return error;
        if (vm?.Activity is null) return NotFound();

        vm.Activity.ProblemDescription = posted.ProblemDescription?.Trim() ?? string.Empty;
        vm.Activity.SolutionDescription = posted.SolutionDescription?.Trim() ?? string.Empty;
        vm.Activity.TextResponse = posted.TextResponse?.Trim() ?? string.Empty;
        vm.Activity.GitHubUrl = string.IsNullOrWhiteSpace(posted.GitHubUrl) ? null : posted.GitHubUrl.Trim();
        vm.Activity.GitHubPrUrl = string.IsNullOrWhiteSpace(posted.GitHubPrUrl) ? null : posted.GitHubPrUrl.Trim();
        vm.Activity.DeliveryNote = string.IsNullOrWhiteSpace(posted.DeliveryNote) ? null : posted.DeliveryNote.Trim();

        var isActivity = string.Equals(returnTo, "activity", StringComparison.OrdinalIgnoreCase);
        ViewData["WorkspaceSection"] = isActivity ? "activity" : "evidence";
        ViewData["LessonId"] = lessonId;
        return View(isActivity ? "Activity" : "Evidence", vm);
    }

    private static void ApplyNextStep(LessonWorkspaceViewModel vm)
    {
        if (!vm.HasJournal)
        {
            vm.NextStepKey = "journal";
            vm.NextStepLabel = "Registrar o que aprendi";
            return;
        }

        if (vm.Activity is not null && !vm.ActivityDone)
        {
            vm.NextStepKey = "activity";
            vm.NextStepLabel = vm.ActivityStarted ? "Continuar atividade" : "Fazer atividade";
            return;
        }

        if (vm.Activity is not null && !vm.EvidenceDone)
        {
            vm.NextStepKey = "evidence";
            vm.NextStepLabel = "Enviar evidência";
            return;
        }

        if (vm.NextLessonId is not null)
        {
            vm.NextStepKey = "next";
            vm.NextStepLabel = "Próxima aula";
            return;
        }

        vm.NextStepKey = "progress";
        vm.NextStepLabel = "Ver meu diário";
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuidingQuestions(
        [FromForm] GuidingQuestionsRequest request,
        CancellationToken cancellationToken)
    {
        var user = await _access.GetCurrentUserAsync(User);
        if (user is null) return Challenge();
        if (!await _access.CanAccessLessonAsync(user, request.LessonId))
        {
            return Forbid();
        }

        if (!_ai.IsEnabled)
        {
            return Json(new { questions = Array.Empty<string>(), message = "IA desabilitada." });
        }

        var lesson = await _db.Lessons
            .AsNoTracking()
            .Include(l => l.Concepts)
            .FirstOrDefaultAsync(l => l.Id == request.LessonId, cancellationToken);

        if (lesson is null) return NotFound();

        var journal = await _db.StudentJournalEntries
            .AsNoTracking()
            .Include(j => j.ConceptMarks).ThenInclude(m => m.Concept)
            .Include(j => j.Questions)
            .FirstOrDefaultAsync(j => j.LessonId == request.LessonId && j.UserId == user.Id, cancellationToken);

        var allConcepts = lesson.Concepts.Select(c => c.Name).ToList();
        var marked = journal?.ConceptMarks.Select(m => m.Concept.Name).ToList() ?? [];
        var understood = journal?.ConceptMarks.Where(m => m.Understood).Select(m => m.Concept.Name).ToList() ?? [];
        var openQuestions = journal?.Questions
            .Where(q => q.Status == QuestionStatus.Open)
            .Select(q => q.Text)
            .ToList() ?? [];

        var questions = await _ai.SuggestGuidingQuestionsAsync(new GuidingQuestionContext
        {
            LessonTitle = lesson.Title,
            Objective = lesson.Objective,
            AllConcepts = allConcepts,
            MarkedConcepts = marked,
            UnderstoodConcepts = understood,
            OpenQuestions = openQuestions,
            NeedsReview = journal?.NeedsReview == true,
            UnderstoodObjective = journal?.UnderstoodObjective == true,
            NoteExcerpt = string.IsNullOrWhiteSpace(journal?.Note)
                ? null
                : journal.Note.Length <= 280 ? journal.Note : journal.Note[..280]
        }, cancellationToken);

        return Json(new { questions });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveJournal([Bind(Prefix = "Journal")] JournalFormViewModel model)
    {
        var user = await _access.GetCurrentUserAsync(User);
        if (user is null) return Challenge();

        if (!await _access.CanAccessLessonAsync(user, model.LessonId))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Não foi possível salvar o diário. Verifique os campos.";
            return RedirectToAction(nameof(Journal), new { id = model.LessonId });
        }

        var lessonExists = await _db.Lessons.AnyAsync(l => l.Id == model.LessonId && l.Status == ContentStatus.Published);
        if (!lessonExists) return NotFound();

        var journal = await _db.StudentJournalEntries
            .Include(j => j.ConceptMarks)
            .Include(j => j.Questions)
            .FirstOrDefaultAsync(j => j.LessonId == model.LessonId && j.UserId == user.Id);

        if (journal is null)
        {
            journal = new StudentJournalEntry
            {
                LessonId = model.LessonId,
                UserId = user.Id
            };
            _db.StudentJournalEntries.Add(journal);
        }

        journal.Note = model.Note?.Trim() ?? string.Empty;
        journal.Reflection = model.Reflection?.Trim() ?? string.Empty;
        journal.UnderstoodObjective = model.UnderstoodObjective;
        journal.PracticedConcept = model.PracticedConcept;
        journal.NeedsReview = model.NeedsReview;
        journal.UpdatedAtUtc = DateTime.UtcNow;

        var validConceptIds = await _db.Concepts
            .Where(c => c.LessonId == model.LessonId)
            .Select(c => c.Id)
            .ToListAsync();

        var selectedConceptIds = model.MarkedConceptIds
            .Distinct()
            .Where(validConceptIds.Contains)
            .ToHashSet();
        var understoodConceptIds = model.UnderstoodConceptIds
            .Distinct()
            .Where(selectedConceptIds.Contains)
            .ToHashSet();

        // Avoid mutating the navigation collection (Remove/Add/Clear): relationship fixup can
        // dirty ConceptId/JournalEntryId and break SQLite Guid TEXT updates.
        var currentMarks = journal.ConceptMarks.ToList();
        foreach (var mark in currentMarks)
        {
            if (!selectedConceptIds.Contains(mark.ConceptId))
            {
                _db.ConceptMarks.Remove(mark);
                continue;
            }

            var nextUnderstood = understoodConceptIds.Contains(mark.ConceptId);
            if (mark.Understood != nextUnderstood)
            {
                mark.Understood = nextUnderstood;
            }
        }

        var keptConceptIds = currentMarks
            .Where(m => selectedConceptIds.Contains(m.ConceptId))
            .Select(m => m.ConceptId)
            .ToHashSet();

        foreach (var conceptId in selectedConceptIds)
        {
            if (keptConceptIds.Contains(conceptId))
            {
                continue;
            }

            _db.ConceptMarks.Add(new ConceptMark
            {
                JournalEntryId = journal.Id,
                ConceptId = conceptId,
                Understood = understoodConceptIds.Contains(conceptId)
            });
        }

        if (!string.IsNullOrWhiteSpace(model.NewQuestion))
        {
            journal.Questions.Add(new JournalQuestion
            {
                Text = model.NewQuestion.Trim(),
                Status = QuestionStatus.Open
            });
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = "Diário salvo. Seu processo de aprendizagem ficou registrado.";
        return RedirectToAction(nameof(Journal), new { id = model.LessonId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveActivity(
        ActivityFormViewModel model,
        Guid lessonId,
        string? returnTo = null)
    {
        var user = await _access.GetCurrentUserAsync(User);
        if (user is null) return Challenge();

        if (!await _access.CanAccessLessonAsync(user, lessonId))
        {
            return Forbid();
        }

        IActionResult Back() => RedirectToAction(
            string.Equals(returnTo, "activity", StringComparison.OrdinalIgnoreCase)
                ? nameof(Activity)
                : nameof(Evidence),
            new { id = lessonId });

        var isActivityPage = string.Equals(returnTo, "activity", StringComparison.OrdinalIgnoreCase);

        if (user.TenantId is null)
        {
            TempData["Error"] = "Usuário sem instituição associada.";
            return Back();
        }

        var activity = await _db.Activities.FirstOrDefaultAsync(a => a.Id == model.ActivityId && a.LessonId == lessonId);
        if (activity is null) return NotFound();

        var submission = await _db.ActivitySubmissions
            .Include(s => s.Attachments)
            .FirstOrDefaultAsync(s => s.ActivityId == model.ActivityId && s.UserId == user.Id);

        if (submission is null)
        {
            submission = new ActivitySubmission
            {
                ActivityId = model.ActivityId,
                UserId = user.Id
            };
            _db.ActivitySubmissions.Add(submission);
            await _db.SaveChangesAsync();
        }

        // Feedback e status da professora não podem ser alterados pelo estudante.
        if (submission.Status == ActivityStatus.Reviewed)
        {
            TempData["Error"] = "Esta atividade já foi revisada pela professora e não pode ser alterada.";
            return Back();
        }

        var repoUrl = string.IsNullOrWhiteSpace(model.GitHubUrl) ? null : model.GitHubUrl.Trim();
        var prUrl = string.IsNullOrWhiteSpace(model.GitHubPrUrl) ? null : model.GitHubPrUrl.Trim();

        if (activity.RequiresGitHubDelivery)
        {
            if (!GitHubUrlValidator.TryValidateRepositoryUrl(repoUrl, out var repoError))
            {
                TempData["Error"] = repoError;
                return await ReturnActivityPageWithPostedValuesAsync(lessonId, returnTo, model);
            }

            if (!GitHubUrlValidator.TryValidatePullRequestUrl(prUrl, out var prError))
            {
                TempData["Error"] = prError;
                return await ReturnActivityPageWithPostedValuesAsync(lessonId, returnTo, model);
            }
        }
        else if (!string.IsNullOrWhiteSpace(repoUrl)
                 && !GitHubUrlValidator.TryValidateRepositoryUrl(repoUrl, out var optionalRepoError))
        {
            TempData["Error"] = optionalRepoError;
            return await ReturnActivityPageWithPostedValuesAsync(lessonId, returnTo, model);
        }

        var previousFeedback = submission.TeacherFeedback;

        submission.ProblemDescription = model.ProblemDescription?.Trim() ?? string.Empty;
        submission.SolutionDescription = model.SolutionDescription?.Trim() ?? string.Empty;
        submission.TextResponse = model.TextResponse?.Trim() ?? string.Empty;
        submission.GitHubUrl = repoUrl;
        submission.GitHubPrUrl = prUrl;
        submission.DeliveryNote = string.IsNullOrWhiteSpace(model.DeliveryNote) ? null : model.DeliveryNote.Trim();
        submission.TeacherFeedback = previousFeedback;
        submission.UpdatedAtUtc = DateTime.UtcNow;

        if (model.Attachment is { Length: > 0 })
        {
            var (ok, error, file) = await _files.SaveSubmissionAttachmentAsync(
                user.TenantId.Value,
                submission.Id,
                model.Attachment);

            if (!ok || file is null)
            {
                TempData["Error"] = error ?? "Falha ao enviar anexo.";
                return await ReturnActivityPageWithPostedValuesAsync(lessonId, returnTo, model);
            }

            submission.Attachments.Add(new SubmissionAttachment
            {
                OriginalFileName = file.OriginalFileName,
                StoredFileName = file.StoredFileName,
                ContentType = file.ContentType,
                SizeBytes = file.SizeBytes
            });
        }

        var hasGitHubDelivery = activity.RequiresGitHubDelivery
            && !string.IsNullOrWhiteSpace(submission.GitHubUrl);

        var hasOtherContent = !string.IsNullOrWhiteSpace(submission.TextResponse)
            || !string.IsNullOrWhiteSpace(submission.ProblemDescription)
            || !string.IsNullOrWhiteSpace(submission.SolutionDescription)
            || submission.Attachments.Count > 0;

        var hasContent = hasGitHubDelivery || hasOtherContent;

        if (activity.RequiresGitHubDelivery && hasOtherContent && !hasGitHubDelivery)
        {
            submission.Status = ActivityStatus.InProgress;
            await _db.SaveChangesAsync();
            TempData["Error"] = "Esta atividade exige a URL do repositório no GitHub.";
            return await ReturnActivityPageWithPostedValuesAsync(lessonId, returnTo, model);
        }

        submission.Status = hasContent
            ? (activity.RequiresGitHubDelivery && !hasGitHubDelivery
                ? ActivityStatus.InProgress
                : ActivityStatus.Submitted)
            : ActivityStatus.InProgress;

        await _db.SaveChangesAsync();
        TempData["Success"] = hasContent
            ? (isActivityPage
                ? "Atividade salva. A professora poderá revisar em breve."
                : "Evidência enviada. A professora poderá revisar em breve.")
            : (isActivityPage
                ? "Rascunho da atividade salvo."
                : "Rascunho da evidência salvo.");
        return Back();
    }
}
