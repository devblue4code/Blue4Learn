using System.ComponentModel.DataAnnotations;
using Blue4Learn.Web.Data;
using Blue4Learn.Web.Domain;
using Blue4Learn.Web.Services;
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

    public TeacherController(ApplicationDbContext db, IAccessService access)
    {
        _db = db;
        _access = access;
    }

    public async Task<IActionResult> Dashboard()
    {
        var user = await _access.GetCurrentUserAsync(User);
        if (user is null) return Challenge();

        var firstName = string.IsNullOrWhiteSpace(user.FullName)
            ? (user.Email?.Split('@').FirstOrDefault() ?? "Professora")
            : user.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? user.FullName;

        var classGroup = await _access.GetPrimaryClassAsync(user);
        if (classGroup is null)
        {
            return View(new TeacherDashboardViewModel
            {
                FirstName = firstName,
                HasClass = false
            });
        }

        var studentIds = await _access.GetStudentIdsInSharedClassesAsync(user);
        var studentIdSet = studentIds.ToHashSet();
        var courseIds = await _access.GetAccessibleCourseIdsAsync(user);

        var students = await _db.Users
            .AsNoTracking()
            .Where(u => studentIdSet.Contains(u.Id))
            .OrderBy(u => u.FullName)
            .ToListAsync();

        var publishedLessons = await _db.Lessons
            .CountAsync(l => l.Status == ContentStatus.Published && courseIds.Contains(l.Module.CourseId));

        var journals = await _db.StudentJournalEntries
            .AsNoTracking()
            .Include(j => j.Questions)
            .Include(j => j.ConceptMarks).ThenInclude(m => m.Concept)
            .Include(j => j.Lesson).ThenInclude(l => l.Module)
            .Include(j => j.User)
            .Where(j => studentIdSet.Contains(j.UserId) && courseIds.Contains(j.Lesson.Module.CourseId))
            .ToListAsync();

        var submissions = await _db.ActivitySubmissions
            .AsNoTracking()
            .Include(s => s.Activity).ThenInclude(a => a.Lesson).ThenInclude(l => l.Module)
            .Where(s => studentIdSet.Contains(s.UserId)
                        && s.Status >= ActivityStatus.Submitted
                        && courseIds.Contains(s.Activity.Lesson.Module.CourseId))
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
            TopConcepts = topConcepts,
            Students = studentProgress,
            RecentQuestions = recentQuestions
        });
    }

    public async Task<IActionResult> Journals(string? filter = null)
    {
        var user = await _access.GetCurrentUserAsync(User);
        if (user is null) return Challenge();

        var classGroup = await _access.GetPrimaryClassAsync(user);
        var studentIds = await _access.GetStudentIdsInSharedClassesAsync(user);
        var courseIds = await _access.GetAccessibleCourseIdsAsync(user);

        filter = (filter ?? "all").ToLowerInvariant();

        var journals = await _db.StudentJournalEntries
            .AsNoTracking()
            .Include(j => j.User)
            .Include(j => j.Questions)
            .Include(j => j.Lesson).ThenInclude(l => l.Module)
            .Where(j => studentIds.Contains(j.UserId) && courseIds.Contains(j.Lesson.Module.CourseId))
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

    public async Task<IActionResult> Submissions()
    {
        var user = await _access.GetCurrentUserAsync(User);
        if (user is null) return Challenge();

        var classGroup = await _access.GetPrimaryClassAsync(user);
        var studentIds = await _access.GetStudentIdsInSharedClassesAsync(user);
        var courseIds = await _access.GetAccessibleCourseIdsAsync(user);

        var items = await _db.ActivitySubmissions
            .AsNoTracking()
            .Include(s => s.User)
            .Include(s => s.Attachments)
            .Include(s => s.Activity).ThenInclude(a => a.Lesson).ThenInclude(l => l.Module)
            .Where(s => studentIds.Contains(s.UserId)
                        && s.Status >= ActivityStatus.Submitted
                        && courseIds.Contains(s.Activity.Lesson.Module.CourseId))
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
                HasFeedback = s.TeacherFeedback != null && s.TeacherFeedback != ""
            })
            .ToListAsync();

        return View(new SubmissionListViewModel
        {
            ClassName = classGroup?.Name ?? "Turma",
            Items = items
        });
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

    private static SubmissionReviewViewModel ToReviewVm(ActivitySubmission s) => new()
    {
        SubmissionId = s.Id,
        StudentName = s.User.FullName,
        StudentEmail = s.User.Email ?? string.Empty,
        LessonTitle = s.Activity.Lesson.Title,
        LessonId = s.Activity.LessonId,
        ActivityTitle = s.Activity.Title,
        Prompt = s.Activity.Prompt,
        ProblemDescription = s.ProblemDescription,
        SolutionDescription = s.SolutionDescription,
        TextResponse = s.TextResponse,
        GitHubUrl = s.GitHubUrl,
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
}
