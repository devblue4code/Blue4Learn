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

    public HomeController(ApplicationDbContext db, IAccessService access)
    {
        _db = db;
        _access = access;
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

        var lessons = await _db.Lessons
            .AsNoTracking()
            .Where(l => l.Status == ContentStatus.Published && courseIds.Contains(l.Module.CourseId))
            .OrderBy(l => l.SortOrder)
            .Select(l => new LessonSummaryViewModel
            {
                Id = l.Id,
                Title = l.Title,
                ModuleTitle = l.Module.Title,
                Objective = l.Objective,
                SortOrder = l.SortOrder,
                HasJournal = l.JournalEntries.Any(j => j.UserId == user.Id),
                NeedsReview = l.JournalEntries.Any(j => j.UserId == user.Id && j.NeedsReview)
            })
            .ToListAsync();

        var next = lessons.FirstOrDefault(l => !l.HasJournal)
                   ?? lessons.FirstOrDefault(l => l.NeedsReview)
                   ?? lessons.LastOrDefault();

        if (next is not null)
        {
            next.IsNext = true;
        }

        var registered = lessons.Count(l => l.HasJournal);
        var total = lessons.Count;
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
            ProgressPercent = total == 0 ? 0 : (int)Math.Round(100.0 * registered / total),
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
