using Blue4Learn.Web.Data;
using Blue4Learn.Web.Domain;
using Blue4Learn.Web.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Blue4Learn.Web.Services;

public interface ILearningProgressService
{
    LessonProgressSnapshot ComputeLessonProgress(
        StudentJournalEntry? journal,
        Activity? activity,
        ActivitySubmission? submission);

    IReadOnlyList<LearningRiskReason> BuildLessonRiskReasons(
        Lesson lesson,
        StudentJournalEntry? journal,
        Activity? activity,
        ActivitySubmission? submission,
        DateTime utcNow);

    Task<TeacherAlertsViewModel> GetTeacherAlertsAsync(
        ClassGroup classGroup,
        IReadOnlyList<string> studentIds);

    Task<StudentRiskBannerViewModel> GetStudentRiskBannerAsync(
        ApplicationUser student,
        IReadOnlyList<Guid> classGroupIds);

    Task<IReadOnlyDictionary<string, StudentLearningSummary>> GetStudentLearningSummariesAsync(
        ClassGroup classGroup,
        IReadOnlyList<string> studentIds);

    int ComputeAverageLessonPercent(
        IReadOnlyList<Lesson> lessons,
        string userId,
        IReadOnlyList<StudentJournalEntry> journals,
        IReadOnlyList<ActivitySubmission> submissions);
}

public sealed class LessonProgressSnapshot
{
    public int TotalComponents { get; init; }
    public int CompletedComponents { get; init; }

    public int Percent => TotalComponents == 0
        ? 0
        : (int)Math.Round(100.0 * CompletedComponents / TotalComponents);
}

public sealed class LearningProgressService : ILearningProgressService
{
    private readonly ApplicationDbContext _db;

    public LearningProgressService(ApplicationDbContext db)
    {
        _db = db;
    }

    public LessonProgressSnapshot ComputeLessonProgress(
        StudentJournalEntry? journal,
        Activity? activity,
        ActivitySubmission? submission)
    {
        var total = 0;
        var completed = 0;

        total++;
        if (journal is not null)
        {
            completed++;
        }

        if (activity is not null)
        {
            total++;
            if (IsActivityDone(submission))
            {
                completed++;
            }

            if (activity.RequiresGitHubDelivery)
            {
                total++;
                if (!string.IsNullOrWhiteSpace(submission?.GitHubUrl))
                {
                    completed++;
                }
            }
        }

        return new LessonProgressSnapshot
        {
            TotalComponents = total,
            CompletedComponents = completed
        };
    }

    public IReadOnlyList<LearningRiskReason> BuildLessonRiskReasons(
        Lesson lesson,
        StudentJournalEntry? journal,
        Activity? activity,
        ActivitySubmission? submission,
        DateTime utcNow)
    {
        var reasons = new List<LearningRiskReason>();

        if (journal is null)
        {
            reasons.Add(new LearningRiskReason
            {
                Code = "missing-journal",
                LessonId = lesson.Id,
                LessonTitle = lesson.Title,
                Message = $"Diário não registrado em \"{lesson.Title}\""
            });
        }
        else
        {
            if (journal.NeedsReview)
            {
                reasons.Add(new LearningRiskReason
                {
                    Code = "needs-review",
                    LessonId = lesson.Id,
                    LessonTitle = lesson.Title,
                    Message = $"Marcou revisão em \"{lesson.Title}\""
                });
            }

            var openQuestion = journal.Questions.FirstOrDefault(q => q.Status == QuestionStatus.Open);
            if (openQuestion is not null)
            {
                reasons.Add(new LearningRiskReason
                {
                    Code = "open-question",
                    LessonId = lesson.Id,
                    LessonTitle = lesson.Title,
                    QuestionId = openQuestion.Id,
                    Message = $"Dúvida aberta em \"{lesson.Title}\""
                });
            }
        }

        if (activity is null)
        {
            return reasons;
        }

        var isSubmitted = submission?.Status is ActivityStatus.Submitted or ActivityStatus.Reviewed;
        var activityDone = IsActivityDone(submission);

        if (activity.DueAtUtc is DateTime dueAt
            && dueAt < utcNow
            && !isSubmitted)
        {
            reasons.Add(new LearningRiskReason
            {
                Code = "overdue-activity",
                LessonId = lesson.Id,
                LessonTitle = lesson.Title,
                SubmissionId = submission?.Id,
                Message = $"Atividade atrasada em \"{lesson.Title}\""
            });
        }
        else if (!activityDone && journal is not null)
        {
            reasons.Add(new LearningRiskReason
            {
                Code = "missing-activity",
                LessonId = lesson.Id,
                LessonTitle = lesson.Title,
                SubmissionId = submission?.Id,
                Message = $"Atividade pendente em \"{lesson.Title}\""
            });
        }

        if (activity.RequiresGitHubDelivery)
        {
            var missingGitHub = string.IsNullOrWhiteSpace(submission?.GitHubUrl);
            var hasPartialWork = submission is not null
                && (!string.IsNullOrWhiteSpace(submission.ProblemDescription)
                    || !string.IsNullOrWhiteSpace(submission.SolutionDescription)
                    || submission.Attachments.Count > 0
                    || !string.IsNullOrWhiteSpace(submission.TextResponse));

            if (missingGitHub && (journal is not null || hasPartialWork || isSubmitted))
            {
                reasons.Add(new LearningRiskReason
                {
                    Code = "missing-github",
                    LessonId = lesson.Id,
                    LessonTitle = lesson.Title,
                    SubmissionId = submission?.Id,
                    Message = $"Repositório GitHub ausente em \"{lesson.Title}\""
                });
            }
        }

        return reasons;
    }

    public async Task<TeacherAlertsViewModel> GetTeacherAlertsAsync(
        ClassGroup classGroup,
        IReadOnlyList<string> studentIds)
    {
        if (studentIds.Count == 0)
        {
            return new TeacherAlertsViewModel
            {
                ClassId = classGroup.Id,
                ClassName = classGroup.Name,
                ClassCode = classGroup.Code,
                CourseTitle = classGroup.Course.Title
            };
        }

        var lessons = await LoadPublishedLessonsAsync(classGroup.Id);
        var lessonIds = lessons.Select(l => l.Id).ToList();
        var activityIds = lessons.SelectMany(l => l.Activities).Select(a => a.Id).ToList();

        var journals = await _db.StudentJournalEntries
            .AsNoTracking()
            .Include(j => j.Questions)
            .Where(j => studentIds.Contains(j.UserId) && lessonIds.Contains(j.LessonId))
            .ToListAsync();

        var submissions = activityIds.Count == 0
            ? []
            : await _db.ActivitySubmissions
                .AsNoTracking()
                .Include(s => s.Attachments)
                .Where(s => studentIds.Contains(s.UserId) && activityIds.Contains(s.ActivityId))
                .ToListAsync();

        var students = await _db.Users
            .AsNoTracking()
            .Where(u => studentIds.Contains(u.Id))
            .OrderBy(u => u.FullName)
            .ToListAsync();

        var hasPublishedQuiz = await _db.Quizzes
            .AsNoTracking()
            .AnyAsync(q => q.CourseId == classGroup.CourseId && q.IsPublished);

        var studentsWithQuiz = hasPublishedQuiz
            ? await _db.QuizAttempts
                .AsNoTracking()
                .Where(a => studentIds.Contains(a.UserId) && a.Quiz.CourseId == classGroup.CourseId)
                .Select(a => a.UserId)
                .Distinct()
                .ToListAsync()
            : [];

        var studentsWithQuizSet = studentsWithQuiz.ToHashSet();
        var now = DateTime.UtcNow;
        var items = new List<StudentAlertItemViewModel>();

        foreach (var student in students)
        {
            var reasons = new List<LearningRiskReason>();

            if (hasPublishedQuiz && !studentsWithQuizSet.Contains(student.Id))
            {
                reasons.Add(new LearningRiskReason
                {
                    Code = "pending-quiz",
                    Message = "Quiz da disciplina ainda não realizado"
                });
            }

            foreach (var lesson in lessons)
            {
                var journal = journals.FirstOrDefault(j => j.UserId == student.Id && j.LessonId == lesson.Id);
                var activity = lesson.Activities.OrderBy(a => a.Title).FirstOrDefault();
                var submission = activity is null
                    ? null
                    : submissions.FirstOrDefault(s => s.UserId == student.Id && s.ActivityId == activity.Id);

                reasons.AddRange(BuildLessonRiskReasons(lesson, journal, activity, submission, now));
            }

            if (reasons.Count == 0)
            {
                continue;
            }

            items.Add(new StudentAlertItemViewModel
            {
                UserId = student.Id,
                FullName = student.FullName,
                LearningProgressPercent = ComputeAverageLessonPercent(lessons, student.Id, journals, submissions),
                Reasons = reasons
                    .OrderBy(r => r.LessonTitle ?? "zzz")
                    .ThenBy(r => r.Code)
                    .ToList()
            });
        }

        return new TeacherAlertsViewModel
        {
            ClassId = classGroup.Id,
            ClassName = classGroup.Name,
            ClassCode = classGroup.Code,
            CourseTitle = classGroup.Course.Title,
            StudentCount = students.Count,
            AtRiskCount = items.Count,
            Items = items
                .OrderBy(i => i.LearningProgressPercent)
                .ThenBy(i => i.FullName)
                .ToList()
        };
    }

    public async Task<StudentRiskBannerViewModel> GetStudentRiskBannerAsync(
        ApplicationUser student,
        IReadOnlyList<Guid> classGroupIds)
    {
        if (classGroupIds.Count == 0)
        {
            return new StudentRiskBannerViewModel();
        }

        var lessons = await _db.Lessons
            .AsNoTracking()
            .Include(l => l.Activities)
            .Where(l => l.Status == ContentStatus.Published && classGroupIds.Contains(l.ClassGroupId))
            .OrderBy(l => l.SortOrder)
            .ToListAsync();

        if (lessons.Count == 0)
        {
            return new StudentRiskBannerViewModel();
        }

        var lessonIds = lessons.Select(l => l.Id).ToList();

        var journals = await _db.StudentJournalEntries
            .AsNoTracking()
            .Include(j => j.Questions)
            .Where(j => j.UserId == student.Id && lessonIds.Contains(j.LessonId))
            .ToListAsync();

        var activityIds = lessons.SelectMany(l => l.Activities).Select(a => a.Id).ToList();
        var submissions = activityIds.Count == 0
            ? []
            : await _db.ActivitySubmissions
                .AsNoTracking()
                .Include(s => s.Attachments)
                .Where(s => s.UserId == student.Id && activityIds.Contains(s.ActivityId))
                .ToListAsync();

        var courseIdList = await _db.Lessons
            .AsNoTracking()
            .Where(l => lessonIds.Contains(l.Id))
            .Select(l => l.Module.CourseId)
            .Distinct()
            .ToListAsync();

        var hasPublishedQuiz = await _db.Quizzes
            .AsNoTracking()
            .AnyAsync(q => courseIdList.Contains(q.CourseId) && q.IsPublished);

        var hasQuizAttempt = !hasPublishedQuiz || await _db.QuizAttempts
            .AsNoTracking()
            .AnyAsync(a => a.UserId == student.Id && courseIdList.Contains(a.Quiz.CourseId));

        var now = DateTime.UtcNow;
        var reasons = new List<LearningRiskReason>();

        if (hasPublishedQuiz && !hasQuizAttempt)
        {
            reasons.Add(new LearningRiskReason
            {
                Code = "pending-quiz",
                Message = "Responder o quiz da disciplina"
            });
        }

        foreach (var lesson in lessons)
        {
            var journal = journals.FirstOrDefault(j => j.LessonId == lesson.Id);
            var activity = lesson.Activities.OrderBy(a => a.Title).FirstOrDefault();
            var submission = activity is null
                ? null
                : submissions.FirstOrDefault(s => s.ActivityId == activity.Id);

            reasons.AddRange(BuildLessonRiskReasons(lesson, journal, activity, submission, now));
        }

        var messages = reasons
            .Select(r => r.Message)
            .Distinct()
            .Take(5)
            .ToList();

        return new StudentRiskBannerViewModel
        {
            Items = messages,
            LearningProgressPercent = ComputeAverageLessonPercent(lessons, student.Id, journals, submissions)
        };
    }

    public async Task<IReadOnlyDictionary<string, StudentLearningSummary>> GetStudentLearningSummariesAsync(
        ClassGroup classGroup,
        IReadOnlyList<string> studentIds)
    {
        if (studentIds.Count == 0)
        {
            return new Dictionary<string, StudentLearningSummary>();
        }

        var lessons = await LoadPublishedLessonsAsync(classGroup.Id);
        var lessonIds = lessons.Select(l => l.Id).ToList();
        var activityIds = lessons.SelectMany(l => l.Activities).Select(a => a.Id).ToList();

        var journals = await _db.StudentJournalEntries
            .AsNoTracking()
            .Include(j => j.Questions)
            .Where(j => studentIds.Contains(j.UserId) && lessonIds.Contains(j.LessonId))
            .ToListAsync();

        var submissions = activityIds.Count == 0
            ? []
            : await _db.ActivitySubmissions
                .AsNoTracking()
                .Include(s => s.Attachments)
                .Where(s => studentIds.Contains(s.UserId) && activityIds.Contains(s.ActivityId))
                .ToListAsync();

        var hasPublishedQuiz = await _db.Quizzes
            .AsNoTracking()
            .AnyAsync(q => q.CourseId == classGroup.CourseId && q.IsPublished);

        var studentsWithQuiz = hasPublishedQuiz
            ? await _db.QuizAttempts
                .AsNoTracking()
                .Where(a => studentIds.Contains(a.UserId) && a.Quiz.CourseId == classGroup.CourseId)
                .Select(a => a.UserId)
                .Distinct()
                .ToListAsync()
            : [];

        var studentsWithQuizSet = studentsWithQuiz.ToHashSet();
        var now = DateTime.UtcNow;
        var result = new Dictionary<string, StudentLearningSummary>();

        foreach (var studentId in studentIds)
        {
            var reasons = new List<LearningRiskReason>();

            if (hasPublishedQuiz && !studentsWithQuizSet.Contains(studentId))
            {
                reasons.Add(new LearningRiskReason
                {
                    Code = "pending-quiz",
                    Message = "Quiz da disciplina ainda não realizado"
                });
            }

            foreach (var lesson in lessons)
            {
                var journal = journals.FirstOrDefault(j => j.UserId == studentId && j.LessonId == lesson.Id);
                var activity = lesson.Activities.OrderBy(a => a.Title).FirstOrDefault();
                var submission = activity is null
                    ? null
                    : submissions.FirstOrDefault(s => s.UserId == studentId && s.ActivityId == activity.Id);

                reasons.AddRange(BuildLessonRiskReasons(lesson, journal, activity, submission, now));
            }

            result[studentId] = new StudentLearningSummary
            {
                LearningProgressPercent = ComputeAverageLessonPercent(lessons, studentId, journals, submissions),
                RiskReasonCount = reasons.Count
            };
        }

        return result;
    }

    public int ComputeAverageLessonPercent(
        IReadOnlyList<Lesson> lessons,
        string userId,
        IReadOnlyList<StudentJournalEntry> journals,
        IReadOnlyList<ActivitySubmission> submissions)
    {
        if (lessons.Count == 0)
        {
            return 0;
        }

        var total = 0;
        foreach (var lesson in lessons)
        {
            var journal = journals.FirstOrDefault(j => j.UserId == userId && j.LessonId == lesson.Id);
            var activity = lesson.Activities.OrderBy(a => a.Title).FirstOrDefault();
            var submission = activity is null
                ? null
                : submissions.FirstOrDefault(s => s.UserId == userId && s.ActivityId == activity.Id);

            total += ComputeLessonProgress(journal, activity, submission).Percent;
        }

        return (int)Math.Round(total / (double)lessons.Count);
    }

    private async Task<List<Lesson>> LoadPublishedLessonsAsync(Guid classGroupId)
    {
        return await _db.Lessons
            .AsNoTracking()
            .Include(l => l.Activities)
            .Where(l => l.ClassGroupId == classGroupId && l.Status == ContentStatus.Published)
            .OrderBy(l => l.SortOrder)
            .ToListAsync();
    }

    private static bool IsActivityDone(ActivitySubmission? submission)
    {
        if (submission is null)
        {
            return false;
        }

        return submission.Status is ActivityStatus.Submitted or ActivityStatus.Reviewed
            || (!string.IsNullOrWhiteSpace(submission.ProblemDescription)
                && !string.IsNullOrWhiteSpace(submission.SolutionDescription));
    }
}
