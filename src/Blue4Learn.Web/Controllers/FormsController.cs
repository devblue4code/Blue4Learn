using System.Text.Json;
using Blue4Learn.Web.Data;
using Blue4Learn.Web.Domain;
using Blue4Learn.Web.Services;
using Blue4Learn.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Blue4Learn.Web.Controllers;

[Authorize]
public class FormsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IAccessService _access;

    public FormsController(ApplicationDbContext db, IAccessService access)
    {
        _db = db;
        _access = access;
    }

    public async Task<IActionResult> Index()
    {
        var user = await _access.GetCurrentUserAsync(User);
        if (user is null) return Challenge();

        var isTeacher = await _access.IsTeacherOrAdminAsync(user);
        var courseIds = await _access.GetAccessibleCourseIdsAsync(user);

        var query = _db.CourseForms
            .AsNoTracking()
            .Include(f => f.Course)
            .Include(f => f.Questions)
            .Where(f => courseIds.Contains(f.CourseId));

        if (!isTeacher)
        {
            query = query.Where(f => f.IsPublished);
        }

        var forms = await query
            .OrderByDescending(f => f.UpdatedAtUtc)
            .ToListAsync();

        HashSet<Guid> myResponseFormIds = [];
        Dictionary<Guid, int> responseCounts = [];
        if (isTeacher)
        {
            var formIds = forms.Select(f => f.Id).ToList();
            responseCounts = await _db.FormResponses
                .AsNoTracking()
                .Where(r => formIds.Contains(r.FormId))
                .GroupBy(r => r.FormId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);
        }
        else
        {
            var formIds = forms.Select(f => f.Id).ToList();
            myResponseFormIds = (await _db.FormResponses
                .AsNoTracking()
                .Where(r => r.UserId == user.Id && formIds.Contains(r.FormId))
                .Select(r => r.FormId)
                .ToListAsync()).ToHashSet();
        }

        return View(new FormListViewModel
        {
            IsTeacher = isTeacher,
            Forms = forms.Select(f => new FormListItemViewModel
            {
                Id = f.Id,
                Title = f.Title,
                Description = f.Description,
                CourseTitle = f.Course.Title,
                CourseId = f.CourseId,
                IsPublished = f.IsPublished,
                QuestionCount = f.Questions.Count,
                ResponseCount = responseCounts.GetValueOrDefault(f.Id),
                AlreadyResponded = myResponseFormIds.Contains(f.Id),
                UpdatedAtUtc = f.UpdatedAtUtc
            }).ToList()
        });
    }

    [Authorize(Roles = $"{AppRoles.Teacher},{AppRoles.Admin}")]
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var user = await _access.GetCurrentUserAsync(User);
        if (user is null) return Challenge();

        return View(new FormCreateViewModel
        {
            Courses = await LoadCourseOptionsAsync(user)
        });
    }

    [Authorize(Roles = $"{AppRoles.Teacher},{AppRoles.Admin}")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(FormCreateViewModel model)
    {
        var user = await _access.GetCurrentUserAsync(User);
        if (user is null) return Challenge();

        model.Courses = await LoadCourseOptionsAsync(user);
        if (!model.Courses.Any(c => c.Id == model.CourseId))
        {
            ModelState.AddModelError(nameof(model.CourseId), "Disciplina inválida.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var form = new CourseForm
        {
            CourseId = model.CourseId,
            Title = model.Title.Trim(),
            Description = model.Description?.Trim() ?? string.Empty,
            IsPublished = false
        };
        _db.CourseForms.Add(form);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Atividade criada. Monte as perguntas no construtor.";
        return RedirectToAction(nameof(Edit), new { id = form.Id });
    }

    [Authorize(Roles = $"{AppRoles.Teacher},{AppRoles.Admin}")]
    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        var user = await _access.GetCurrentUserAsync(User);
        if (user is null) return Challenge();

        var form = await LoadFormForTeacherAsync(user, id);
        if (form is null) return NotFound();

        return View(ToBuilderVm(form));
    }

    [Authorize(Roles = $"{AppRoles.Teacher},{AppRoles.Admin}")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveMeta(FormBuilderViewModel model)
    {
        var user = await _access.GetCurrentUserAsync(User);
        if (user is null) return Challenge();

        var form = await LoadFormForTeacherAsync(user, model.FormId);
        if (form is null) return NotFound();

        if (string.IsNullOrWhiteSpace(model.Title))
        {
            TempData["Error"] = "Informe o título da atividade.";
            return RedirectToAction(nameof(Edit), new { id = form.Id });
        }

        form.Title = model.Title.Trim();
        form.Description = model.Description?.Trim() ?? string.Empty;
        form.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        TempData["Success"] = "Dados da atividade salvos.";
        return RedirectToAction(nameof(Edit), new { id = form.Id });
    }

    [Authorize(Roles = $"{AppRoles.Teacher},{AppRoles.Admin}")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddQuestion(Guid formId, FormQuestionType type = FormQuestionType.ShortText)
    {
        var user = await _access.GetCurrentUserAsync(User);
        if (user is null) return Challenge();

        var form = await LoadFormForTeacherAsync(user, formId);
        if (form is null) return NotFound();

        var sort = form.Questions.Count == 0 ? 1 : form.Questions.Max(q => q.SortOrder) + 1;
        var question = new FormQuestion
        {
            FormId = form.Id,
            Prompt = type switch
            {
                FormQuestionType.Paragraph => "Descreva com detalhes",
                FormQuestionType.SingleChoice => "Escolha uma opção",
                FormQuestionType.MultiChoice => "Selecione as opções que se aplicam",
                _ => "Nova pergunta"
            },
            Type = type,
            IsRequired = true,
            SortOrder = sort,
            OptionsJson = NeedsOptions(type)
                ? FormQuestionEditViewModel.ToOptionsJson(["Opção 1", "Opção 2", "Opção 3"])
                : "[]"
        };

        _db.FormQuestions.Add(question);
        form.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        TempData["Success"] = "Pergunta adicionada.";
        return RedirectToAction(nameof(Edit), new { id = form.Id });
    }

    [Authorize(Roles = $"{AppRoles.Teacher},{AppRoles.Admin}")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveQuestion(Guid formId, FormQuestionEditViewModel model)
    {
        var user = await _access.GetCurrentUserAsync(User);
        if (user is null) return Challenge();

        var form = await LoadFormForTeacherAsync(user, formId);
        if (form is null) return NotFound();

        var question = form.Questions.FirstOrDefault(q => q.Id == model.Id);
        if (question is null) return NotFound();

        if (string.IsNullOrWhiteSpace(model.Prompt))
        {
            TempData["Error"] = "O enunciado da pergunta não pode ficar vazio.";
            return RedirectToAction(nameof(Edit), new { id = form.Id });
        }

        if (NeedsOptions(model.Type) && model.Options.Count < 2)
        {
            TempData["Error"] = "Perguntas de escolha precisam de pelo menos 2 opções.";
            return RedirectToAction(nameof(Edit), new { id = form.Id });
        }

        question.Prompt = model.Prompt.Trim();
        question.Type = model.Type;
        question.IsRequired = model.IsRequired;
        question.OptionsJson = NeedsOptions(model.Type)
            ? FormQuestionEditViewModel.ToOptionsJson(model.Options)
            : "[]";
        form.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        TempData["Success"] = "Pergunta atualizada.";
        return RedirectToAction(nameof(Edit), new { id = form.Id });
    }

    [Authorize(Roles = $"{AppRoles.Teacher},{AppRoles.Admin}")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteQuestion(Guid formId, Guid questionId)
    {
        var user = await _access.GetCurrentUserAsync(User);
        if (user is null) return Challenge();

        var form = await LoadFormForTeacherAsync(user, formId);
        if (form is null) return NotFound();

        var question = form.Questions.FirstOrDefault(q => q.Id == questionId);
        if (question is null) return NotFound();

        var hasAnswers = await _db.FormAnswers.AnyAsync(a => a.QuestionId == questionId);
        if (hasAnswers)
        {
            TempData["Error"] = "Não é possível excluir uma pergunta que já tem respostas.";
            return RedirectToAction(nameof(Edit), new { id = form.Id });
        }

        _db.FormQuestions.Remove(question);
        form.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        TempData["Success"] = "Pergunta removida.";
        return RedirectToAction(nameof(Edit), new { id = form.Id });
    }

    [Authorize(Roles = $"{AppRoles.Teacher},{AppRoles.Admin}")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveQuestion(Guid formId, Guid questionId, string direction)
    {
        var user = await _access.GetCurrentUserAsync(User);
        if (user is null) return Challenge();

        var form = await LoadFormForTeacherAsync(user, formId);
        if (form is null) return NotFound();

        var ordered = form.Questions.OrderBy(q => q.SortOrder).ToList();
        var index = ordered.FindIndex(q => q.Id == questionId);
        if (index < 0) return NotFound();

        var swapWith = string.Equals(direction, "up", StringComparison.OrdinalIgnoreCase)
            ? index - 1
            : index + 1;

        if (swapWith < 0 || swapWith >= ordered.Count)
        {
            return RedirectToAction(nameof(Edit), new { id = form.Id });
        }

        (ordered[index].SortOrder, ordered[swapWith].SortOrder) =
            (ordered[swapWith].SortOrder, ordered[index].SortOrder);

        form.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Edit), new { id = form.Id });
    }

    [Authorize(Roles = $"{AppRoles.Teacher},{AppRoles.Admin}")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TogglePublish(Guid id)
    {
        var user = await _access.GetCurrentUserAsync(User);
        if (user is null) return Challenge();

        var form = await LoadFormForTeacherAsync(user, id);
        if (form is null) return NotFound();

        if (!form.IsPublished && form.Questions.Count == 0)
        {
            TempData["Error"] = "Adicione ao menos uma pergunta antes de publicar.";
            return RedirectToAction(nameof(Edit), new { id = form.Id });
        }

        form.IsPublished = !form.IsPublished;
        form.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        TempData["Success"] = form.IsPublished
            ? "Atividade publicada. Os alunos já podem responder."
            : "Atividade despublicada.";
        return RedirectToAction(nameof(Edit), new { id = form.Id });
    }

    [Authorize(Roles = $"{AppRoles.Teacher},{AppRoles.Admin}")]
    public async Task<IActionResult> Responses(Guid id)
    {
        var user = await _access.GetCurrentUserAsync(User);
        if (user is null) return Challenge();

        var form = await LoadFormForTeacherAsync(user, id);
        if (form is null) return NotFound();

        var responses = await _db.FormResponses
            .AsNoTracking()
            .Include(r => r.User)
            .Include(r => r.Answers)
            .Where(r => r.FormId == id)
            .OrderByDescending(r => r.SubmittedAtUtc)
            .ToListAsync();

        return View(new FormResponsesViewModel
        {
            FormId = form.Id,
            Title = form.Title,
            CourseTitle = form.Course.Title,
            QuestionCount = form.Questions.Count,
            Responses = responses.Select(r => new FormResponseRowViewModel
            {
                ResponseId = r.Id,
                StudentName = r.User.FullName,
                SubmittedAtUtc = r.SubmittedAtUtc,
                AnsweredCount = r.Answers.Count
            }).ToList()
        });
    }

    [Authorize(Roles = $"{AppRoles.Teacher},{AppRoles.Admin}")]
    public async Task<IActionResult> ResponseDetail(Guid id)
    {
        var user = await _access.GetCurrentUserAsync(User);
        if (user is null) return Challenge();

        var response = await _db.FormResponses
            .AsNoTracking()
            .Include(r => r.User)
            .Include(r => r.Form).ThenInclude(f => f.Course)
            .Include(r => r.Form).ThenInclude(f => f.Questions)
            .Include(r => r.Answers)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (response is null) return NotFound();

        var courseIds = await _access.GetAccessibleCourseIdsAsync(user);
        if (!courseIds.Contains(response.Form.CourseId))
        {
            return Forbid();
        }

        var answersByQuestion = response.Answers.ToDictionary(a => a.QuestionId);
        var details = response.Form.Questions
            .OrderBy(q => q.SortOrder)
            .Select(q =>
            {
                answersByQuestion.TryGetValue(q.Id, out var answer);
                return new FormAnswerDetailViewModel
                {
                    Prompt = q.Prompt,
                    Type = q.Type,
                    DisplayValue = FormatAnswer(q.Type, answer)
                };
            }).ToList();

        return View(new FormResponseDetailViewModel
        {
            FormId = response.FormId,
            FormTitle = response.Form.Title,
            StudentName = response.User.FullName,
            StudentEmail = response.User.Email ?? string.Empty,
            SubmittedAtUtc = response.SubmittedAtUtc,
            Answers = details
        });
    }

    [HttpGet]
    public async Task<IActionResult> Fill(Guid id)
    {
        var user = await _access.GetCurrentUserAsync(User);
        if (user is null) return Challenge();

        if (await _access.IsTeacherOrAdminAsync(user))
        {
            return RedirectToAction(nameof(Edit), new { id });
        }

        var form = await LoadPublishedFormForStudentAsync(user, id);
        if (form is null) return NotFound();

        var existing = await _db.FormResponses
            .AsNoTracking()
            .Include(r => r.Answers)
            .FirstOrDefaultAsync(r => r.FormId == id && r.UserId == user.Id);

        return View(ToFillVm(form, existing));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Fill(Guid id, FormFillViewModel model)
    {
        var user = await _access.GetCurrentUserAsync(User);
        if (user is null) return Challenge();

        if (await _access.IsTeacherOrAdminAsync(user))
        {
            return Forbid();
        }

        var form = await LoadPublishedFormForStudentAsync(user, id);
        if (form is null) return NotFound();

        var posted = model.Questions ?? [];
        var postedById = posted.ToDictionary(q => q.QuestionId);

        foreach (var question in form.Questions.OrderBy(q => q.SortOrder))
        {
            postedById.TryGetValue(question.Id, out var answer);
            if (question.IsRequired && !HasAnswer(question, answer))
            {
                ModelState.AddModelError(string.Empty, $"Responda: {question.Prompt}");
            }
        }

        if (!ModelState.IsValid)
        {
            var existing = await _db.FormResponses
                .AsNoTracking()
                .Include(r => r.Answers)
                .FirstOrDefaultAsync(r => r.FormId == id && r.UserId == user.Id);
            var vm = ToFillVm(form, existing);
            foreach (var q in vm.Questions)
            {
                if (postedById.TryGetValue(q.QuestionId, out var postedQ))
                {
                    q.TextValue = postedQ.TextValue;
                    q.SelectedOptions = postedQ.SelectedOptions ?? [];
                }
            }

            return View(vm);
        }

        var response = await _db.FormResponses
            .Include(r => r.Answers)
            .FirstOrDefaultAsync(r => r.FormId == id && r.UserId == user.Id);

        if (response is null)
        {
            response = new FormResponse
            {
                FormId = form.Id,
                UserId = user.Id
            };
            _db.FormResponses.Add(response);
            await _db.SaveChangesAsync();
        }
        else
        {
            _db.FormAnswers.RemoveRange(response.Answers);
            response.UpdatedAtUtc = DateTime.UtcNow;
            response.SubmittedAtUtc = DateTime.UtcNow;
        }

        foreach (var question in form.Questions)
        {
            postedById.TryGetValue(question.Id, out var answer);
            if (!HasAnswer(question, answer))
            {
                continue;
            }

            _db.FormAnswers.Add(new FormAnswer
            {
                ResponseId = response.Id,
                QuestionId = question.Id,
                TextValue = question.Type is FormQuestionType.ShortText or FormQuestionType.Paragraph
                    ? answer!.TextValue?.Trim()
                    : null,
                SelectedOptionsJson = question.Type is FormQuestionType.SingleChoice or FormQuestionType.MultiChoice
                    ? JsonSerializer.Serialize(NormalizeSelections(question, answer!))
                    : null
            });
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = "Respostas enviadas. Obrigado!";
        return RedirectToAction(nameof(Index));
    }

    private async Task<IReadOnlyList<FormCourseOptionViewModel>> LoadCourseOptionsAsync(ApplicationUser user)
    {
        var courseIds = await _access.GetAccessibleCourseIdsAsync(user);
        return await _db.Courses
            .AsNoTracking()
            .Where(c => courseIds.Contains(c.Id))
            .OrderBy(c => c.Title)
            .Select(c => new FormCourseOptionViewModel { Id = c.Id, Title = c.Title })
            .ToListAsync();
    }

    private async Task<CourseForm?> LoadFormForTeacherAsync(ApplicationUser user, Guid id)
    {
        var courseIds = await _access.GetAccessibleCourseIdsAsync(user);
        return await _db.CourseForms
            .Include(f => f.Course)
            .Include(f => f.Questions)
            .Include(f => f.Responses)
            .FirstOrDefaultAsync(f => f.Id == id && courseIds.Contains(f.CourseId));
    }

    private async Task<CourseForm?> LoadPublishedFormForStudentAsync(ApplicationUser user, Guid id)
    {
        var courseIds = await _access.GetAccessibleCourseIdsAsync(user);
        return await _db.CourseForms
            .AsNoTracking()
            .Include(f => f.Course)
            .Include(f => f.Questions)
            .FirstOrDefaultAsync(f => f.Id == id && f.IsPublished && courseIds.Contains(f.CourseId));
    }

    private static FormBuilderViewModel ToBuilderVm(CourseForm form) => new()
    {
        FormId = form.Id,
        CourseId = form.CourseId,
        CourseTitle = form.Course.Title,
        Title = form.Title,
        Description = form.Description,
        IsPublished = form.IsPublished,
        ResponseCount = form.Responses.Count,
        Questions = form.Questions
            .OrderBy(q => q.SortOrder)
            .Select(q => new FormQuestionEditViewModel
            {
                Id = q.Id,
                Prompt = q.Prompt,
                Type = q.Type,
                IsRequired = q.IsRequired,
                SortOrder = q.SortOrder,
                OptionsText = FormQuestionEditViewModel.OptionsToText(q.OptionsJson)
            }).ToList()
    };

    private static FormFillViewModel ToFillVm(CourseForm form, FormResponse? existing)
    {
        var answers = existing?.Answers.ToDictionary(a => a.QuestionId) ?? [];
        return new FormFillViewModel
        {
            FormId = form.Id,
            Title = form.Title,
            Description = form.Description,
            CourseTitle = form.Course.Title,
            AlreadyResponded = existing is not null,
            Questions = form.Questions.OrderBy(q => q.SortOrder).Select(q =>
            {
                answers.TryGetValue(q.Id, out var answer);
                return new FormFillQuestionViewModel
                {
                    QuestionId = q.Id,
                    Prompt = q.Prompt,
                    Type = q.Type,
                    IsRequired = q.IsRequired,
                    Options = FormQuestionEditViewModel.ParseOptionsJson(q.OptionsJson),
                    TextValue = answer?.TextValue,
                    SelectedOptions = FormQuestionEditViewModel.ParseOptionsJson(answer?.SelectedOptionsJson).ToList()
                };
            }).ToList()
        };
    }

    private static bool NeedsOptions(FormQuestionType type) =>
        type is FormQuestionType.SingleChoice or FormQuestionType.MultiChoice;

    private static bool HasAnswer(FormQuestion question, FormFillQuestionViewModel? answer)
    {
        if (answer is null) return false;
        return question.Type switch
        {
            FormQuestionType.ShortText or FormQuestionType.Paragraph =>
                !string.IsNullOrWhiteSpace(answer.TextValue),
            FormQuestionType.SingleChoice or FormQuestionType.MultiChoice =>
                answer.SelectedOptions is { Count: > 0 }
                && answer.SelectedOptions.Any(s => !string.IsNullOrWhiteSpace(s)),
            _ => false
        };
    }

    private static List<string> NormalizeSelections(FormQuestion question, FormFillQuestionViewModel answer)
    {
        var allowed = FormQuestionEditViewModel.ParseOptionsJson(question.OptionsJson).ToHashSet(StringComparer.Ordinal);
        var selected = (answer.SelectedOptions ?? [])
            .Where(s => !string.IsNullOrWhiteSpace(s) && allowed.Contains(s.Trim()))
            .Select(s => s.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (question.Type == FormQuestionType.SingleChoice)
        {
            return selected.Take(1).ToList();
        }

        return selected;
    }

    private static string FormatAnswer(FormQuestionType type, FormAnswer? answer)
    {
        if (answer is null) return "—";
        if (type is FormQuestionType.ShortText or FormQuestionType.Paragraph)
        {
            return string.IsNullOrWhiteSpace(answer.TextValue) ? "—" : answer.TextValue!;
        }

        var selected = FormQuestionEditViewModel.ParseOptionsJson(answer.SelectedOptionsJson);
        return selected.Count == 0 ? "—" : string.Join(", ", selected);
    }
}
