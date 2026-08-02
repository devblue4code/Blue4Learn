using Blue4Learn.Web.Data;
using Blue4Learn.Web.Domain;
using Blue4Learn.Web.Services;
using Blue4Learn.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Blue4Learn.Web.Controllers;

[Authorize]
public class ProgressController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IAccessService _access;

    public ProgressController(ApplicationDbContext db, IAccessService access)
    {
        _db = db;
        _access = access;
    }

    public async Task<IActionResult> Index(string? filter)
    {
        var user = await _access.GetCurrentUserAsync(User);
        if (user is null) return Challenge();

        filter = (filter ?? "all").ToLowerInvariant();
        if (filter is not ("all" or "review" or "missing" or "done"))
        {
            filter = "all";
        }

        var courseIds = await _access.GetAccessibleCourseIdsAsync(user);
        var lessons = await _db.Lessons
            .AsNoTracking()
            .Include(l => l.Module)
            .Include(l => l.Activities)
            .Where(l => l.Status == ContentStatus.Published && courseIds.Contains(l.Module.CourseId))
            .OrderBy(l => l.Module.SortOrder)
            .ThenBy(l => l.SortOrder)
            .ToListAsync();

        var lessonIds = lessons.Select(l => l.Id).ToList();
        var journals = await _db.StudentJournalEntries
            .AsNoTracking()
            .Include(j => j.Questions)
            .Where(j => j.UserId == user.Id && lessonIds.Contains(j.LessonId))
            .ToListAsync();

        var activityIds = lessons.SelectMany(l => l.Activities).Select(a => a.Id).ToList();
        var submissions = await _db.ActivitySubmissions
            .AsNoTracking()
            .Where(s => s.UserId == user.Id && activityIds.Contains(s.ActivityId))
            .ToListAsync();

        var items = lessons.Select(lesson =>
        {
            var journal = journals.FirstOrDefault(j => j.LessonId == lesson.Id);
            var activity = lesson.Activities.OrderBy(a => a.Title).FirstOrDefault();
            var submission = activity is null
                ? null
                : submissions.FirstOrDefault(s => s.ActivityId == activity.Id);

            return new MyProgressItemViewModel
            {
                LessonId = lesson.Id,
                Title = lesson.Title,
                ModuleTitle = lesson.Module.Title,
                SortOrder = lesson.SortOrder,
                HasJournal = journal is not null,
                NeedsReview = journal?.NeedsReview == true,
                HasOpenQuestion = journal?.Questions.Any(q => q.Status == QuestionStatus.Open) == true,
                ActivityStatus = submission?.Status,
                LastJournalUpdateUtc = journal?.UpdatedAtUtc
            };
        }).ToList();

        var nextAction = items.FirstOrDefault(i => !i.HasJournal)
            ?? items.FirstOrDefault(i => i.NeedsReview || i.HasOpenQuestion);

        if (nextAction is not null)
        {
            nextAction.IsNext = true;
        }

        var registered = items.Count(i => i.HasJournal);
        var reviewCount = items.Count(i => i.NeedsReview || i.HasOpenQuestion);
        var missingCount = items.Count(i => !i.HasJournal);
        var doneCount = items.Count(i => i.HasJournal && !i.NeedsReview && !i.HasOpenQuestion);
        var total = items.Count;

        var filtered = filter switch
        {
            "review" => items.Where(i => i.NeedsReview || i.HasOpenQuestion).ToList(),
            "missing" => items.Where(i => !i.HasJournal).ToList(),
            "done" => items.Where(i => i.HasJournal && !i.NeedsReview && !i.HasOpenQuestion).ToList(),
            _ => items
        };

        var firstName = string.IsNullOrWhiteSpace(user.FullName)
            ? (user.Email?.Split('@').FirstOrDefault() ?? "você")
            : user.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? user.FullName;

        return View(new MyProgressViewModel
        {
            FirstName = firstName,
            Filter = filter,
            TotalLessons = total,
            RegisteredCount = registered,
            NeedsReviewCount = reviewCount,
            MissingCount = missingCount,
            DoneCount = doneCount,
            ProgressPercent = total == 0 ? 0 : (int)Math.Round(100.0 * registered / total),
            NextAction = nextAction,
            Items = filtered
        });
    }
}
