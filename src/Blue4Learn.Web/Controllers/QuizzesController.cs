using Blue4Learn.Web.Data;
using Blue4Learn.Web.Domain;
using Blue4Learn.Web.Services;
using Blue4Learn.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Blue4Learn.Web.Controllers;

[Authorize]
public class QuizzesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IAccessService _access;

    public QuizzesController(ApplicationDbContext db, IAccessService access)
    {
        _db = db;
        _access = access;
    }

    [Authorize(Roles = $"{AppRoles.Teacher},{AppRoles.Admin}")]
    public async Task<IActionResult> Results()
    {
        var user = await _access.GetCurrentUserAsync(User);
        if (user is null) return Challenge();

        var courseIds = await _access.GetAccessibleCourseIdsAsync(user);
        var quiz = await _db.Quizzes
            .AsNoTracking()
            .Include(q => q.Course)
            .Include(q => q.Questions)
            .Include(q => q.Attempts).ThenInclude(a => a.User)
            .Where(q => courseIds.Contains(q.CourseId) && q.IsPublished)
            .OrderBy(q => q.Title)
            .FirstOrDefaultAsync();

        if (quiz is null)
        {
            return View(new QuizResultsViewModel
            {
                CourseTitle = "Disciplina",
                QuizTitle = "Nenhum quiz publicado"
            });
        }

        var attempts = quiz.Attempts
            .OrderByDescending(a => a.SubmittedAtUtc)
            .Select(a => new QuizAttemptRowViewModel
            {
                StudentName = a.User.FullName,
                Score = a.Score,
                MaxScore = a.MaxScore,
                Percent = a.MaxScore == 0 ? 0 : (int)Math.Round(100.0 * a.Score / a.MaxScore),
                SubmittedAtUtc = a.SubmittedAtUtc
            })
            .ToList();

        return View(new QuizResultsViewModel
        {
            CourseTitle = quiz.Course.Title,
            QuizTitle = quiz.Title,
            QuizId = quiz.Id,
            QuestionCount = quiz.Questions.Count,
            AttemptCount = attempts.Count,
            AveragePercent = attempts.Count == 0 ? 0 : attempts.Average(a => a.Percent),
            Attempts = attempts
        });
    }

    public async Task<IActionResult> Index()
    {
        var user = await _access.GetCurrentUserAsync(User);
        if (user is null) return Challenge();

        if (await _access.IsTeacherOrAdminAsync(user))
        {
            return RedirectToAction(nameof(Results));
        }

        var courseIds = await _access.GetAccessibleCourseIdsAsync(user);
        var quizzes = await _db.Quizzes
            .AsNoTracking()
            .Where(q => courseIds.Contains(q.CourseId) && q.IsPublished)
            .OrderBy(q => q.Title)
            .Select(q => new QuizListItemViewModel
            {
                Id = q.Id,
                Title = q.Title,
                Description = q.Description,
                QuestionCount = q.Questions.Count,
                AlreadyAttempted = q.Attempts.Any(a => a.UserId == user.Id),
                LastScore = q.Attempts.Where(a => a.UserId == user.Id).OrderByDescending(a => a.SubmittedAtUtc).Select(a => (int?)a.Score).FirstOrDefault(),
                LastMaxScore = q.Attempts.Where(a => a.UserId == user.Id).OrderByDescending(a => a.SubmittedAtUtc).Select(a => (int?)a.MaxScore).FirstOrDefault()
            })
            .ToListAsync();

        return View(new QuizListViewModel { Quizzes = quizzes });
    }

    [HttpGet]
    public async Task<IActionResult> Take(Guid id)
    {
        var user = await _access.GetCurrentUserAsync(User);
        if (user is null) return Challenge();
        if (await _access.IsTeacherOrAdminAsync(user))
        {
            return RedirectToAction(nameof(Results));
        }

        var quiz = await _db.Quizzes
            .AsNoTracking()
            .Include(q => q.Questions)
            .FirstOrDefaultAsync(q => q.Id == id && q.IsPublished);

        if (quiz is null) return NotFound();
        if (!await _access.CanAccessCourseAsync(user, quiz.CourseId)) return Forbid();

        return View(new QuizTakeViewModel
        {
            QuizId = quiz.Id,
            Title = quiz.Title,
            Description = quiz.Description,
            Questions = quiz.Questions
                .OrderBy(q => q.SortOrder)
                .Select(q => new QuizQuestionTakeViewModel
                {
                    Id = q.Id,
                    Prompt = q.Prompt,
                    OptionA = q.OptionA,
                    OptionB = q.OptionB,
                    OptionC = q.OptionC,
                    OptionD = q.OptionD,
                    SortOrder = q.SortOrder
                })
                .ToList()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Take(QuizTakeViewModel model)
    {
        var user = await _access.GetCurrentUserAsync(User);
        if (user is null) return Challenge();
        if (await _access.IsTeacherOrAdminAsync(user))
        {
            return RedirectToAction(nameof(Results));
        }

        var quiz = await _db.Quizzes
            .Include(q => q.Questions)
            .FirstOrDefaultAsync(q => q.Id == model.QuizId && q.IsPublished);

        if (quiz is null) return NotFound();
        if (!await _access.CanAccessCourseAsync(user, quiz.CourseId)) return Forbid();

        var answers = model.Questions.ToDictionary(q => q.Id, q => (q.SelectedOption ?? "").Trim().ToUpperInvariant());
        var score = 0;
        foreach (var question in quiz.Questions)
        {
            if (answers.TryGetValue(question.Id, out var selected)
                && string.Equals(selected, question.CorrectOption, StringComparison.OrdinalIgnoreCase))
            {
                score++;
            }
        }

        _db.QuizAttempts.Add(new QuizAttempt
        {
            QuizId = quiz.Id,
            UserId = user.Id,
            Score = score,
            MaxScore = quiz.Questions.Count,
            SubmittedAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Quiz enviado: {score}/{quiz.Questions.Count} acertos.";
        return RedirectToAction(nameof(Index));
    }
}
