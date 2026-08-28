using Blue4Learn.Web.Data;
using Blue4Learn.Web.Domain;
using Blue4Learn.Web.Models;
using Blue4Learn.Web.Services;
using Blue4Learn.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Blue4Learn.Web.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IAccessService _access;
    private readonly ILearningProgressService _progress;

    public HomeController(ApplicationDbContext db, IAccessService access, ILearningProgressService progress)
    {
        _db = db;
        _access = access;
        _progress = progress;
    }

    [AllowAnonymous]
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction(nameof(Dashboard));
        }

        // Entrada do protótipo: login primeiro (landing segue em /Home/Welcome).
        return Redirect("/Identity/Account/Login");
    }

    [AllowAnonymous]
    public IActionResult Welcome() => View("Index");

    [Authorize]
    public async Task<IActionResult> Dashboard()
    {
        var user = await _access.GetCurrentUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var isTeacher = await _access.IsTeacherOrAdminAsync(user);
        var courseIds = await _access.GetAccessibleCourseIdsAsync(user);

        List<ClassSummaryViewModel> classes;
        if (isTeacher)
        {
            classes = await _db.ClassGroups
                .AsNoTracking()
                .Where(c => courseIds.Contains(c.CourseId))
                .OrderBy(c => c.Name)
                .Select(c => new ClassSummaryViewModel
                {
                    Id = c.Id,
                    Name = c.Name,
                    Code = c.Code,
                    CourseTitle = c.Course.Title,
                    StudentCount = c.Enrollments.Count
                })
                .ToListAsync();
        }
        else
        {
            var classIds = await _db.Enrollments
                .Where(e => e.UserId == user.Id)
                .Select(e => e.ClassGroupId)
                .ToListAsync();

            classes = await _db.ClassGroups
                .AsNoTracking()
                .Where(c => classIds.Contains(c.Id))
                .Select(c => new ClassSummaryViewModel
                {
                    Id = c.Id,
                    Name = c.Name,
                    Code = c.Code,
                    CourseTitle = c.Course.Title,
                    StudentCount = c.Enrollments.Count
                })
                .ToListAsync();
        }

        var classGroupIds = await _access.GetAccessibleClassGroupIdsAsync(user);
        var lessonsQuery = _db.Lessons
            .AsNoTracking()
            .Include(l => l.Module)
            .Include(l => l.Activities)
            .Where(l => l.Status == ContentStatus.Published && classGroupIds.Contains(l.ClassGroupId));

        var lessonsData = await lessonsQuery
            .OrderBy(l => l.SortOrder)
            .ToListAsync();

        var lessonIds = lessonsData.Select(l => l.Id).ToList();
        var journals = await _db.StudentJournalEntries
            .AsNoTracking()
            .Include(j => j.Questions)
            .Where(j => j.UserId == user.Id && lessonIds.Contains(j.LessonId))
            .ToListAsync();

        var activityIds = lessonsData.SelectMany(l => l.Activities).Select(a => a.Id).ToList();
        var submissions = activityIds.Count == 0
            ? []
            : await _db.ActivitySubmissions
                .AsNoTracking()
                .Include(s => s.Attachments)
                .Where(s => s.UserId == user.Id && activityIds.Contains(s.ActivityId))
                .ToListAsync();

        var lessons = lessonsData.Select(l =>
        {
            var journal = journals.FirstOrDefault(j => j.LessonId == l.Id);
            var activity = l.Activities.OrderBy(a => a.Title).FirstOrDefault();
            var submission = activity is null
                ? null
                : submissions.FirstOrDefault(s => s.ActivityId == activity.Id);
            var progress = _progress.ComputeLessonProgress(journal, activity, submission);

            return new LessonSummaryViewModel
            {
                Id = l.Id,
                Title = l.Title,
                ModuleTitle = l.Module.Title,
                Objective = l.Objective,
                SortOrder = l.SortOrder,
                HasJournal = journal is not null,
                NeedsReview = journal?.NeedsReview == true,
                LearningProgressPercent = progress.Percent
            };
        }).ToList();

        var next = lessons.FirstOrDefault(l => !l.HasJournal)
                   ?? lessons.FirstOrDefault(l => l.NeedsReview)
                   ?? lessons.LastOrDefault();

        if (next is not null)
        {
            next.IsNext = true;
        }

        var registered = lessons.Count(l => l.HasJournal);
        var total = lessons.Count;
        var journalPercent = total == 0 ? 0 : (int)Math.Round(100.0 * registered / total);
        var learningPercent = total == 0
            ? 0
            : (int)Math.Round(lessons.Average(l => l.LearningProgressPercent));

        StudentRiskBannerViewModel? riskBanner = null;
        if (!isTeacher)
        {
            riskBanner = await _progress.GetStudentRiskBannerAsync(user, classGroupIds);
        }

        var firstName = user.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
                        ?? user.FullName;

        var vm = new HomeDashboardViewModel
        {
            FullName = user.FullName,
            FirstName = firstName,
            IsTeacher = isTeacher,
            Classes = classes,
            RecentLessons = lessons,
            RegisteredCount = registered,
            PendingCount = Math.Max(0, total - registered),
            NeedsReviewCount = lessons.Count(l => l.NeedsReview),
            ProgressPercent = isTeacher ? journalPercent : learningPercent,
            LearningProgressPercent = learningPercent,
            RiskBanner = riskBanner,
            NextLesson = next
        };

        return View(vm);
    }

    [AllowAnonymous]
    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
