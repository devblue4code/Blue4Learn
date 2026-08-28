using System.ComponentModel.DataAnnotations;
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
public class TeacherController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IAccessService _access;
    private readonly ILearningContextService _context;
    private readonly IMarkdownService _markdown;
    private readonly IAiTutorService _ai;
    private readonly IGitHubCommitService _github;

    public TeacherController(
        ApplicationDbContext db,
        IAccessService access,
        ILearningContextService context,
        IMarkdownService markdown,
        IAiTutorService ai,
        IGitHubCommitService github)
    {
        _db = db;
        _access = access;
        _context = context;
        _markdown = markdown;
        _ai = ai;
        _github = github;
    }

    public async Task<IActionResult> Dashboard()
    {
        var user = await _access.GetCurrentUserAsync(User);
        if (user is null) return Challenge();

        var classOptions = await LoadClassOptionsAsync(user);

        var firstName = string.IsNullOrWhiteSpace(user.FullName)
            ? (user.Email?.Split('@').FirstOrDefault() ?? "Professora")
            : user.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? user.FullName;

        var classGroup = await _context.ResolveClassAsync(user);
        if (classGroup is null)
        {
            return View(new TeacherDashboardViewModel
            {
                FirstName = firstName,
                HasClass = false,
                ClassOptions = classOptions
            });
        }

        var classMemberIds = await _db.Enrollments
            .AsNoTracking()
            .Where(e => e.ClassGroupId == classGroup.Id)
            .Select(e => e.UserId)
            .Distinct()
            .ToListAsync();
        var classMemberSet = classMemberIds.ToHashSet();

        // Scope the dashboard to the selected class only, avoiding cross-class data leakage.
        var sharedStudentIds = await _access.GetStudentIdsInSharedClassesAsync(user);
        var studentIds = sharedStudentIds
            .Where(classMemberSet.Contains)
            .Distinct()
            .ToList();
        var studentIdSet = studentIds.ToHashSet();
        var selectedCourseId = classGroup.CourseId;

        var students = await _db.Users
            .AsNoTracking()
            .Where(u => studentIdSet.Contains(u.Id))
            .OrderBy(u => u.FullName)
            .ToListAsync();

        var publishedLessons = await _db.Lessons
            .CountAsync(l => l.Status == ContentStatus.Published && l.Module.CourseId == selectedCourseId);

        var journals = await _db.StudentJournalEntries
            .AsNoTracking()
            .Include(j => j.Questions)
            .Include(j => j.ConceptMarks).ThenInclude(m => m.Concept)
            .Include(j => j.Lesson).ThenInclude(l => l.Module)
            .Include(j => j.User)
            .Where(j => studentIdSet.Contains(j.UserId) && j.Lesson.Module.CourseId == selectedCourseId)
            .ToListAsync();

        var submissions = await _db.ActivitySubmissions
            .AsNoTracking()
            .Include(s => s.Activity).ThenInclude(a => a.Lesson).ThenInclude(l => l.Module)
            .Where(s => studentIdSet.Contains(s.UserId)
                        && s.Status >= ActivityStatus.Submitted
                        && s.Activity.Lesson.Module.CourseId == selectedCourseId)
            .ToListAsync();

        var openQuestions = journals.SelectMany(j => j.Questions).Where(q => q.Status == QuestionStatus.Open).ToList();
        var expectedJournals = students.Count * publishedLessons;
        var pendingJournals = Math.Max(0, expectedJournals - journals.Count);
        var needsReviewJournals = journals.Count(j => j.NeedsReview);
        var awaitingFeedback = submissions.Count(s => s.Status == ActivityStatus.Submitted);
        var coveragePercent = expectedJournals == 0
            ? 0
            : (int)Math.Round(100.0 * journals.Count / expectedJournals);

        var topConcepts = journals
            .SelectMany(j => j.ConceptMarks)
            .GroupBy(m => m.Concept.Name)
            .Select(g => new ConceptStatViewModel
            {
                Name = g.Key,
                Marks = g.Count(),
                Understood = g.Count(x => x.Understood)
            })
            .OrderByDescending(x => x.Marks)
            .Take(6)
            .ToList();

        var studentProgress = students.Select(s =>
        {
            var studentJournals = journals.Where(j => j.UserId == s.Id).ToList();
            var journalCount = studentJournals.Count;
            var focusLessonId = studentJournals
                .Where(j => j.NeedsReview || j.Questions.Any(q => q.Status == QuestionStatus.Open))
                .OrderBy(j => j.Lesson.SortOrder)
                .Select(j => (Guid?)j.LessonId)
                .FirstOrDefault()
                ?? studentJournals
                    .OrderByDescending(j => j.UpdatedAtUtc)
                    .Select(j => (Guid?)j.LessonId)
                    .FirstOrDefault();
            var focusSubmissionId = submissions
                .Where(x => x.UserId == s.Id && x.Status == ActivityStatus.Submitted)
                .OrderByDescending(x => x.UpdatedAtUtc)
                .Select(x => (Guid?)x.Id)
                .FirstOrDefault();

            return new StudentProgressViewModel
            {
                UserId = s.Id,
                FullName = s.FullName,
                JournalCount = journalCount,
                ExpectedJournals = publishedLessons,
                ProgressPercent = publishedLessons == 0
                    ? 0
                    : (int)Math.Round(100.0 * journalCount / publishedLessons),
                OpenQuestions = studentJournals.SelectMany(j => j.Questions).Count(q => q.Status == QuestionStatus.Open),
                NeedsReviewCount = studentJournals.Count(j => j.NeedsReview),
                SubmittedActivities = submissions.Count(x => x.UserId == s.Id),
                AwaitingFeedback = submissions.Count(x => x.UserId == s.Id && x.Status == ActivityStatus.Submitted),
                FocusLessonId = focusLessonId,
                FocusSubmissionId = focusSubmissionId
            };
        })
        .OrderByDescending(s => s.NeedsAttention)
        .ThenBy(s => s.ProgressPercent)
        .ThenBy(s => s.FullName)
        .ToList();

        var recentQuestions = openQuestions
            .OrderByDescending(q => q.CreatedAtUtc)
            .Take(8)
            .Select(q =>
            {
                var journal = journals.First(j => j.Id == q.JournalEntryId);
                return new QuestionFeedItemViewModel
                {
                    Id = q.Id,
                    StudentUserId = journal.UserId,
                    StudentName = journal.User.FullName,
                    LessonId = journal.LessonId,
                    LessonTitle = journal.Lesson.Title,
                    Text = q.Text,
                    CreatedAtUtc = q.CreatedAtUtc
                };
            }).ToList();

        return View(new TeacherDashboardViewModel
        {
            FirstName = firstName,
            HasClass = true,
            ClassId = classGroup.Id,
            ClassName = classGroup.Name,
            ClassCode = classGroup.Code,
            CourseTitle = classGroup.Course.Title,
            StudentCount = students.Count,
            PublishedLessons = publishedLessons,
            OpenQuestions = openQuestions.Count,
            PendingJournals = pendingJournals,
            NeedsReviewJournals = needsReviewJournals,
            SubmittedActivities = submissions.Count,
            AwaitingFeedback = awaitingFeedback,
            CoveragePercent = coveragePercent,
            ClassOptions = classOptions,
            TopConcepts = topConcepts,
            Students = studentProgress,
            RecentQuestions = recentQuestions
        });
    }

    public async Task<IActionResult> Journals(string? filter = null)
    {
        var user = await _access.GetCurrentUserAsync(User);
        if (user is null) return Challenge();

        var classGroup = await _context.ResolveClassAsync(user);
        if (classGroup is null)
        {
            return View(new ClassJournalsViewModel
            {
                ClassName = "Turma",
                CourseTitle = "Disciplina",
                Entries = []
            });
        }

        var classMemberIds = await _db.Enrollments
            .AsNoTracking()
            .Where(e => e.ClassGroupId == classGroup.Id)
            .Select(e => e.UserId)
            .Distinct()
            .ToListAsync();
        var classMemberSet = classMemberIds.ToHashSet();
        var studentIds = (await _access.GetStudentIdsInSharedClassesAsync(user))
            .Where(classMemberSet.Contains)
            .Distinct()
            .ToList();
        var selectedCourseId = classGroup.CourseId;

        filter = (filter ?? "all").ToLowerInvariant();

        var journals = await _db.StudentJournalEntries
            .AsNoTracking()
            .Include(j => j.User)
            .Include(j => j.Questions)
            .Include(j => j.Lesson).ThenInclude(l => l.Module)
            .Where(j => studentIds.Contains(j.UserId) && j.Lesson.Module.CourseId == selectedCourseId)
            .OrderByDescending(j => j.NeedsReview)
            .ThenByDescending(j => j.UpdatedAtUtc)
            .ToListAsync();

        journals = filter switch
        {
            "review" => journals.Where(j => j.NeedsReview).ToList(),
            "questions" => journals.Where(j => j.Questions.Any(q => q.Status == QuestionStatus.Open)).ToList(),
            _ => journals
        };

        var entries = journals.Select(j => new ClassJournalItemViewModel
        {
            Id = j.Id,
            StudentName = j.User.FullName,
            StudentUserId = j.UserId,
            LessonId = j.LessonId,
            LessonTitle = j.Lesson.Title,
            Reflection = j.Reflection,
            NeedsReview = j.NeedsReview,
            UnderstoodObjective = j.UnderstoodObjective,
            OpenQuestions = j.Questions.Count(q => q.Status == QuestionStatus.Open),
            UpdatedAtUtc = j.UpdatedAtUtc
        }).ToList();

        ViewData["JournalFilter"] = filter;
        return View(new ClassJournalsViewModel
        {
            ClassName = classGroup?.Name ?? "Turma",
            CourseTitle = classGroup?.Course.Title ?? "Disciplina",
            Entries = entries
        });
    }

    public async Task<IActionResult> Submissions(string? filter)
    {
        var user = await _access.GetCurrentUserAsync(User);
        if (user is null) return Challenge();

        var classGroup = await _context.ResolveClassAsync(user);
        if (classGroup is null)
        {
            return View(new SubmissionListViewModel
            {
                ClassName = "Turma",
                Filter = filter ?? "all",
                Items = []
            });
        }

        var classMemberIds = await _db.Enrollments
            .AsNoTracking()
            .Where(e => e.ClassGroupId == classGroup.Id)
            .Select(e => e.UserId)
            .Distinct()
            .ToListAsync();
        var classMemberSet = classMemberIds.ToHashSet();
        var studentIds = (await _access.GetStudentIdsInSharedClassesAsync(user))
            .Where(classMemberSet.Contains)
            .Distinct()
            .ToList();
        var selectedCourseId = classGroup.CourseId;

        var items = await _db.ActivitySubmissions
            .AsNoTracking()
            .Include(s => s.User)
            .Include(s => s.Attachments)
            .Include(s => s.Activity).ThenInclude(a => a.Lesson).ThenInclude(l => l.Module)
            .Where(s => studentIds.Contains(s.UserId)
                        && s.Status >= ActivityStatus.Submitted
                        && s.Activity.Lesson.Module.CourseId == selectedCourseId)
            .OrderByDescending(s => s.UpdatedAtUtc)
            .Select(s => new SubmissionListItemViewModel
            {
                SubmissionId = s.Id,
                StudentName = s.User.FullName,
                LessonTitle = s.Activity.Lesson.Title,
                ActivityTitle = s.Activity.Title,
                Status = s.Status,
                UpdatedAtUtc = s.UpdatedAtUtc,
                AttachmentCount = s.Attachments.Count,
                HasFeedback = s.TeacherFeedback != null && s.TeacherFeedback != "",
                RequiresGitHubDelivery = s.Activity.RequiresGitHubDelivery,
                MissingGitHubDelivery = s.Activity.RequiresGitHubDelivery
                    && (s.GitHubUrl == null || s.GitHubUrl == ""),
                GitHubUrl = s.GitHubUrl,
                GitHubPrUrl = s.GitHubPrUrl
            })
            .ToListAsync();

        var missingGitHubCount = items.Count(i => i.MissingGitHubDelivery);

        if (string.Equals(filter, "missing-github", StringComparison.OrdinalIgnoreCase))
        {
            items = items.Where(i => i.MissingGitHubDelivery).ToList();
        }

        return View(new SubmissionListViewModel
        {
            ClassName = classGroup?.Name ?? "Turma",
            Filter = filter ?? "all",
            MissingGitHubCount = missingGitHubCount,
            Items = items
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SwitchClass(Guid classId)
    {
        var user = await _access.GetCurrentUserAsync(User);
        if (user is null) return Challenge();

        var selected = await _context.ResolveClassAsync(user, classId);
        if (selected is null)
        {
            return Forbid();
        }

        TempData["Success"] = $"Turma selecionada: {selected.Name}.";
        return RedirectToAction(nameof(Dashboard));
    }

    [HttpGet]
    public async Task<IActionResult> Review(Guid id)
    {
        var user = await _access.GetCurrentUserAsync(User);
        if (user is null) return Challenge();

        var submission = await LoadSubmissionAsync(id);
        if (submission is null) return NotFound();
        if (!await _access.CanViewStudentAsync(user, submission.UserId))
        {
            return Forbid();
        }

        return View(ToReviewVm(submission));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(Guid id, [Required, StringLength(4000)] string teacherFeedback)
    {
        var user = await _access.GetCurrentUserAsync(User);
        if (user is null) return Challenge();

        var submission = await LoadSubmissionAsync(id);
        if (submission is null) return NotFound();
        if (!await _access.CanViewStudentAsync(user, submission.UserId))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(teacherFeedback))
        {
            ModelState.AddModelError(nameof(teacherFeedback), "Escreva um feedback orientador.");
            return View(ToReviewVm(submission));
        }

        var markReviewed = Request.Form["markReviewed"].Any(v => v == "true");
        submission.TeacherFeedback = teacherFeedback.Trim();
        submission.Status = markReviewed ? ActivityStatus.Reviewed : ActivityStatus.Submitted;
        submission.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        TempData["Success"] = markReviewed
            ? "Feedback enviado e atividade marcada como revisada."
            : "Feedback salvo. A atividade permanece como enviada.";
        return RedirectToAction(nameof(Submissions));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Analyze(Guid id, CancellationToken cancellationToken)
    {
        var user = await _access.GetCurrentUserAsync(User);
        if (user is null) return Challenge();

        var submission = await LoadSubmissionAsync(id);
        if (submission is null) return NotFound();
        if (!await _access.CanViewStudentAsync(user, submission.UserId))
        {
            return Forbid();
        }

        var commits = await _github.GetRecentCommitsAsync(submission.GitHubUrl, 8, cancellationToken);
        var commitInfos = commits.Select(c => new EvidenceCommitInfo
        {
            Sha = c.Sha,
            Message = c.Message,
            Author = c.Author,
            Date = c.Date == DateTimeOffset.MinValue
                ? "—"
                : c.Date.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
            Files = c.Files.Select(f => new EvidenceCommitFileInfo
            {
                Filename = f.Filename,
                Status = f.Status,
                Additions = f.Additions,
                Deletions = f.Deletions,
                PatchExcerpt = f.PatchExcerpt
            }).ToList()
        }).ToList();

        var analysis = await _ai.AnalyzeEvidenceAsync(new EvidenceAnalysisRequest
        {
            ActivityTitle = submission.Activity.Title,
            Prompt = submission.Activity.Prompt,
            ProblemDescription = submission.ProblemDescription,
            SolutionDescription = submission.SolutionDescription,
            TextResponse = submission.TextResponse,
            GitHubUrl = submission.GitHubUrl,
            AttachmentNames = submission.Attachments.Select(a => a.OriginalFileName).ToList(),
            Commits = commitInfos
        }, cancellationToken);

        return Json(new
        {
            summary = analysis.Summary,
            feedbackDraft = analysis.FeedbackDraft,
            usedLlm = analysis.UsedLlm,
            checklist = analysis.Checklist.Select(c => new
            {
                item = c.Item,
                status = c.Status,
                evidenceNote = c.EvidenceNote
            }),
            commits = commits.Select(c => new
            {
                sha = c.Sha,
                message = c.Message,
                author = c.Author,
                date = c.Date == DateTimeOffset.MinValue
                    ? "—"
                    : c.Date.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
                url = c.Url,
                files = c.Files.Select(f => new
                {
                    filename = f.Filename,
                    status = f.Status,
                    additions = f.Additions,
                    deletions = f.Deletions
                })
            })
        });
    }

    public async Task<IActionResult> Student(string id)
    {
        var teacher = await _access.GetCurrentUserAsync(User);
        if (teacher is null) return Challenge();
        if (!await _access.CanViewStudentAsync(teacher, id))
        {
            return Forbid();
        }

        var student = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
        if (student is null) return NotFound();

        var courseIds = await _access.GetAccessibleCourseIdsAsync(teacher);
        var journals = await _db.StudentJournalEntries
            .AsNoTracking()
            .Include(j => j.Lesson).ThenInclude(l => l.Module)
            .Include(j => j.Questions)
            .Include(j => j.ConceptMarks).ThenInclude(m => m.Concept)
            .Where(j => j.UserId == id && courseIds.Contains(j.Lesson.Module.CourseId))
            .OrderBy(j => j.Lesson.SortOrder)
            .ToListAsync();

        var lessonIds = journals.Select(j => j.LessonId).ToList();
        var submissions = await _db.ActivitySubmissions
            .AsNoTracking()
            .Include(s => s.Activity)
            .Where(s => s.UserId == id
                        && lessonIds.Contains(s.Activity.LessonId)
                        && s.Status >= ActivityStatus.Submitted)
            .ToListAsync();

        var records = journals.Select(j =>
        {
            var submission = submissions
                .Where(s => s.Activity.LessonId == j.LessonId)
                .OrderByDescending(s => s.UpdatedAtUtc)
                .FirstOrDefault();

            return new StudentLessonRecordViewModel
            {
                LessonId = j.LessonId,
                LessonTitle = j.Lesson.Title,
                Note = j.Note,
                Reflection = j.Reflection,
                NeedsReview = j.NeedsReview,
                SubmissionId = submission?.Id,
                ActivityStatus = submission?.Status,
                Questions = j.Questions.Select(q => q.Text).ToList(),
                Concepts = j.ConceptMarks.Select(m => m.Concept.Name + (m.Understood ? " ✓" : " ?")).ToList()
            };
        }).ToList();

        return View(new StudentDetailViewModel
        {
            FullName = student.FullName,
            Email = student.Email ?? string.Empty,
            Records = records
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResolveQuestion(Guid id)
    {
        var teacher = await _access.GetCurrentUserAsync(User);
        if (teacher is null) return Challenge();

        var question = await _db.JournalQuestions
            .Include(q => q.JournalEntry)
            .FirstOrDefaultAsync(q => q.Id == id);

        if (question is null) return NotFound();
        if (!await _access.CanViewStudentAsync(teacher, question.JournalEntry.UserId))
        {
            return Forbid();
        }

        question.Status = QuestionStatus.Resolved;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Dúvida marcada como resolvida.";
        return RedirectToAction(nameof(Dashboard));
    }

    private async Task<ActivitySubmission?> LoadSubmissionAsync(Guid id)
    {
        return await _db.ActivitySubmissions
            .Include(s => s.User)
            .Include(s => s.Attachments)
            .Include(s => s.Activity).ThenInclude(a => a.Lesson)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    private SubmissionReviewViewModel ToReviewVm(ActivitySubmission s) => new()
    {
        SubmissionId = s.Id,
        StudentName = s.User.FullName,
        StudentEmail = s.User.Email ?? string.Empty,
        LessonTitle = s.Activity.Lesson.Title,
        LessonId = s.Activity.LessonId,
        ActivityTitle = s.Activity.Title,
        Prompt = s.Activity.Prompt,
        PromptHtml = _markdown.ToSafeHtml(s.Activity.Prompt),
        ProblemDescription = s.ProblemDescription,
        SolutionDescription = s.SolutionDescription,
        TextResponse = s.TextResponse,
        GitHubUrl = s.GitHubUrl,
        GitHubPrUrl = s.GitHubPrUrl,
        DeliveryNote = s.DeliveryNote,
        RequiresGitHubDelivery = s.Activity.RequiresGitHubDelivery,
        Status = s.Status,
        TeacherFeedback = s.TeacherFeedback,
        Attachments = s.Attachments
            .OrderByDescending(a => a.UploadedAtUtc)
            .Select(a => new AttachmentItemViewModel
            {
                Id = a.Id,
                FileName = a.OriginalFileName,
                SizeBytes = a.SizeBytes
            }).ToList()
    };

    private async Task<IReadOnlyList<TeacherClassOptionViewModel>> LoadClassOptionsAsync(ApplicationUser user)
    {
        if (user.TenantId is null)
        {
            return [];
        }

        var isAdmin = User.IsInRole(AppRoles.Admin);
        var query = _db.ClassGroups
            .AsNoTracking()
            .Where(c => c.TenantId == user.TenantId.Value);

        if (!isAdmin)
        {
            query = query.Where(c => c.Course.TeacherUserId == user.Id);
        }

        return await query
            .OrderBy(c => c.Name)
            .Select(c => new TeacherClassOptionViewModel
            {
                Id = c.Id,
                Name = c.Name,
                Code = c.Code
            })
            .ToListAsync();
    }
}
