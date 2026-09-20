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

    public async Task<IActionResult> Index(Guid? lessonId = null)
    {
        var user = await _access.GetCurrentUserAsync(User);
        if (user is null) return Challenge();

        var isTeacher = await _access.IsTeacherOrAdminAsync(user);
        var lessonIds = await GetAccessibleLessonIdsAsync(user);

        var query = _db.CourseForms
            .AsNoTracking()
            .Include(f => f.Lesson).ThenInclude(l => l.Module).ThenInclude(m => m.Course)
            .Include(f => f.Questions)
            .Where(f => lessonIds.Contains(f.LessonId));

        if (lessonId is Guid filterLesson && lessonIds.Contains(filterLesson))
        {
            query = query.Where(f => f.LessonId == filterLesson);
        }

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

        string? filterTitle = null;
        if (lessonId is Guid lid && lessonIds.Contains(lid))
        {
            filterTitle = await _db.Lessons.AsNoTracking()
                .Where(l => l.Id == lid)
                .Select(l => l.Title)
                .FirstOrDefaultAsync();
        }

        return View(new FormListViewModel
        {
            IsTeacher = isTeacher,
            FilterLessonId = lessonId,
            FilterLessonTitle = filterTitle,
            Forms = forms.Select(f => new FormListItemViewModel
            {
                Id = f.Id,
                Title = f.Title,
                Description = f.Description,
                LessonTitle = f.Lesson.Title,
                LessonContext = LessonContext(f.Lesson),
                LessonId = f.LessonId,
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
    public async Task<IActionResult> Create(Guid? lessonId = null)
    {
        var user = await _access.GetCurrentUserAsync(User);
        if (user is null) return Challenge();

        var lessons = await LoadLessonOptionsAsync(user);
        return View(new FormCreateViewModel
        {
            LessonId = lessonId is Guid id && lessons.Any(l => l.Id == id) ? id : null,
            Lessons = lessons
        });
    }

    [Authorize(Roles = $"{AppRoles.Teacher},{AppRoles.Admin}")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(FormCreateViewModel model)
    {
        var user = await _access.GetCurrentUserAsync(User);
        if (user is null) return Challenge();

        model.Lessons = await LoadLessonOptionsAsync(user);
        if (model.LessonId is not Guid lessonId || !model.Lessons.Any(l => l.Id == lessonId))
        {
            ModelState.AddModelError(nameof(model.LessonId), "Aula inválida.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var form = new CourseForm
        {
            LessonId = model.LessonId!.Value,
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
                ? FormQuestionEditViewModel.ToOptionsJson(["", "", "", ""])
                : "[]",
            CorrectOptionsJson = "[]",
            FeedbackCorrect = "Isso mesmo!",
            FeedbackIncorrect = "Revise a resposta e tente de novo."
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

        if (NeedsOptions(model.Type))
        {
            // Garante 4 slots a partir do form (OptionSlots[0..3]).
            model.OptionSlots = FormQuestionEditViewModel.NormalizeSlots(model.OptionSlots);
            var filled = model.Options;
            if (filled.Count < 2)
            {
                TempData["Error"] = "Preencha pelo menos as alternativas A e B.";
                return RedirectToAction(nameof(Edit), new { id = form.Id });
            }

            if (filled.Count > 4)
            {
                TempData["Error"] = "Use no máximo 4 alternativas (A, B, C e D).";
                return RedirectToAction(nameof(Edit), new { id = form.Id });
            }
        }

        var correct = ResolveCorrectOptions(model);
        if (NeedsOptions(model.Type))
        {
            if (correct.Count == 0)
            {
                TempData["Error"] = "Marque a resposta correta (A, B, C ou D).";
                return RedirectToAction(nameof(Edit), new { id = form.Id });
            }

            if (model.Type == FormQuestionType.SingleChoice && correct.Count > 1)
            {
                correct = correct.Take(1).ToList();
            }
        }
        else if (IsTextType(model.Type) && correct.Count == 0)
        {
            TempData["Error"] = "Informe o gabarito (resposta correta) da pergunta.";
            return RedirectToAction(nameof(Edit), new { id = form.Id });
        }

        question.Prompt = model.Prompt.Trim();
        question.Type = model.Type;
        question.IsRequired = model.IsRequired;
        question.OptionsJson = NeedsOptions(model.Type)
            ? FormQuestionEditViewModel.ToOptionsJson(model.Options)
            : "[]";
        question.CorrectOptionsJson = FormQuestionEditViewModel.ToOptionsJson(correct);
        question.FeedbackCorrect = string.IsNullOrWhiteSpace(model.FeedbackCorrect)
            ? "Isso mesmo!"
            : model.FeedbackCorrect.Trim();
        question.FeedbackIncorrect = string.IsNullOrWhiteSpace(model.FeedbackIncorrect)
            ? "Revise a resposta e tente de novo."
            : model.FeedbackIncorrect.Trim();
        form.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Reavalia respostas já enviadas desta pergunta (gabarito novo/atualizado).
        await RegradeQuestionAnswersAsync(question);

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
            LessonTitle = form.Lesson.Title,
            LessonContext = LessonContext(form.Lesson),
            QuestionCount = form.Questions.Count,
            Responses = responses.Select(r =>
            {
                var graded = r.Answers.Count(a => a.IsCorrect.HasValue);
                var correct = r.Answers.Count(a => a.IsCorrect == true);
                return new FormResponseRowViewModel
                {
                    ResponseId = r.Id,
                    StudentName = r.User.FullName,
                    SubmittedAtUtc = r.SubmittedAtUtc,
                    AnsweredCount = r.Answers.Count,
                    GradedCount = graded > 0 ? graded : null,
                    CorrectCount = graded > 0 ? correct : null
                };
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
            .Include(r => r.Form).ThenInclude(f => f.Lesson).ThenInclude(l => l.Module).ThenInclude(m => m.Course)
            .Include(r => r.Form).ThenInclude(f => f.Questions)
            .Include(r => r.Answers)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (response is null) return NotFound();

        var lessonIds = await GetAccessibleLessonIdsAsync(user);
        if (!lessonIds.Contains(response.Form.LessonId))
        {
            return Forbid();
        }

        var answersByQuestion = response.Answers.ToDictionary(a => a.QuestionId);
        var details = response.Form.Questions
            .OrderBy(q => q.SortOrder)
            .Select(q =>
            {
                answersByQuestion.TryGetValue(q.Id, out var answer);
                var correct = FormQuestionEditViewModel.ParseOptionsJson(q.CorrectOptionsJson);
                var selected = FormQuestionEditViewModel.ParseOptionsJson(answer?.SelectedOptionsJson);
                var isCorrect = answer?.IsCorrect;
                string? feedback = null;
                if (isCorrect == true) feedback = q.FeedbackCorrect;
                else if (isCorrect == false) feedback = q.FeedbackIncorrect;

                return new FormAnswerDetailViewModel
                {
                    Prompt = q.Prompt,
                    Type = q.Type,
                    DisplayValue = FormatAnswer(q.Type, answer),
                    IsCorrect = isCorrect,
                    Options = FormQuestionEditViewModel.ParseOptionsJson(q.OptionsJson),
                    SelectedOptions = selected,
                    CorrectOptions = correct,
                    FeedbackShown = feedback
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
            return RedirectToAction(nameof(Preview), new { id });
        }

        var form = await LoadPublishedFormForStudentAsync(user, id);
        if (form is null) return NotFound();

        var existing = await _db.FormResponses
            .AsNoTracking()
            .Include(r => r.Answers)
            .FirstOrDefaultAsync(r => r.FormId == id && r.UserId == user.Id);

        if (existing is not null)
        {
            await RegradeResponseAsync(existing.Id);
            existing = await _db.FormResponses
                .AsNoTracking()
                .Include(r => r.Answers)
                .FirstOrDefaultAsync(r => r.Id == existing.Id);
        }

        return View(ToFillVm(form, existing));
    }

    /// <summary>Preview do formulário como o estudante vê (professora/admin).</summary>
    [Authorize(Roles = $"{AppRoles.Teacher},{AppRoles.Admin}")]
    [HttpGet]
    public async Task<IActionResult> Preview(Guid id)
    {
        var user = await _access.GetCurrentUserAsync(User);
        if (user is null) return Challenge();

        var form = await LoadFormForTeacherAsync(user, id);
        if (form is null) return NotFound();

        var vm = ToFillVm(form, existing: null);
        vm.IsTeacherPreview = true;
        return View("Fill", vm);
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

        if (id == Guid.Empty && model.FormId != Guid.Empty)
        {
            id = model.FormId;
        }

        var form = await LoadPublishedFormForStudentAsync(user, id);
        if (form is null) return NotFound();

        var existing = await _db.FormResponses
            .Include(r => r.Answers)
            .FirstOrDefaultAsync(r => r.FormId == id && r.UserId == user.Id);

        if (existing is not null)
        {
            TempData["Error"] = "Você já enviou este questionário. Só é permitida uma tentativa.";
            return RedirectToAction(nameof(Fill), new { id });
        }

        var posted = (model.Questions ?? [])
            .Where(q => q.QuestionId != Guid.Empty)
            .ToList();
        var postedById = posted
            .GroupBy(q => q.QuestionId)
            .ToDictionary(g => g.Key, g => g.First());

        // Com timer, pergunta sem resposta (tempo esgotado) é permitida — conta como erro/em branco.
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Não foi possível enviar. Tente novamente.";
            return View(ToFillVm(form, existing: null));
        }

        var response = new FormResponse
        {
            FormId = form.Id,
            UserId = user.Id
        };
        _db.FormResponses.Add(response);
        await _db.SaveChangesAsync();

        var answeredIds = new HashSet<Guid>();
        foreach (var question in form.Questions)
        {
            postedById.TryGetValue(question.Id, out var answer);
            if (!HasAnswer(question, answer))
            {
                continue;
            }

            answeredIds.Add(question.Id);
            var selected = question.Type is FormQuestionType.SingleChoice or FormQuestionType.MultiChoice
                ? NormalizeSelections(question, answer!)
                : [];
            bool? isCorrect = EvaluateAnswer(question, answer, selected);

            _db.FormAnswers.Add(new FormAnswer
            {
                ResponseId = response.Id,
                QuestionId = question.Id,
                TextValue = question.Type is FormQuestionType.ShortText or FormQuestionType.Paragraph
                    ? answer!.TextValue?.Trim()
                    : null,
                SelectedOptionsJson = question.Type is FormQuestionType.SingleChoice or FormQuestionType.MultiChoice
                    ? JsonSerializer.Serialize(selected)
                    : null,
                IsCorrect = isCorrect
            });
        }

        // Perguntas sem resposta mas com gabarito → registradas como erradas.
        foreach (var question in form.Questions)
        {
            if (answeredIds.Contains(question.Id)) continue;
            var correct = FormQuestionEditViewModel.ParseOptionsJson(question.CorrectOptionsJson);
            if (correct.Count == 0) continue;

            _db.FormAnswers.Add(new FormAnswer
            {
                ResponseId = response.Id,
                QuestionId = question.Id,
                IsCorrect = false
            });
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = answeredIds.Count == 0
            ? "Tempo esgotado sem respostas. Confira o resultado abaixo."
            : "Questionário enviado! Confira seu resultado — só uma tentativa.";
        return RedirectToAction(nameof(Fill), new { id = form.Id });
    }

    private async Task<IReadOnlyList<Guid>> GetAccessibleLessonIdsAsync(ApplicationUser user)
    {
        var classGroupIds = await _access.GetAccessibleClassGroupIdsAsync(user);
        var canPreviewDrafts = await _access.IsTeacherOrAdminAsync(user);

        return await _db.Lessons
            .AsNoTracking()
            .Where(l => classGroupIds.Contains(l.ClassGroupId)
                        && (l.Status == ContentStatus.Published
                            || (canPreviewDrafts && l.Status != ContentStatus.Archived)))
            .Select(l => l.Id)
            .ToListAsync();
    }

    private async Task<IReadOnlyList<FormLessonOptionViewModel>> LoadLessonOptionsAsync(ApplicationUser user)
    {
        var classGroupIds = await _access.GetAccessibleClassGroupIdsAsync(user);
        return await _db.Lessons
            .AsNoTracking()
            .Where(l => classGroupIds.Contains(l.ClassGroupId) && l.Status != ContentStatus.Archived)
            .OrderBy(l => l.Module.Course.Title)
            .ThenBy(l => l.Module.SortOrder)
            .ThenBy(l => l.SortOrder)
            .Select(l => new FormLessonOptionViewModel
            {
                Id = l.Id,
                Label = l.Module.Course.Title + " · " + l.Module.Title + " · " + l.Title
            })
            .ToListAsync();
    }

    private async Task<CourseForm?> LoadFormForTeacherAsync(ApplicationUser user, Guid id)
    {
        var lessonIds = await GetAccessibleLessonIdsAsync(user);
        return await _db.CourseForms
            .Include(f => f.Lesson).ThenInclude(l => l.Module).ThenInclude(m => m.Course)
            .Include(f => f.Questions)
            .Include(f => f.Responses)
            .FirstOrDefaultAsync(f => f.Id == id && lessonIds.Contains(f.LessonId));
    }

    private async Task<CourseForm?> LoadPublishedFormForStudentAsync(ApplicationUser user, Guid id)
    {
        var lessonIds = await GetAccessibleLessonIdsAsync(user);
        return await _db.CourseForms
            .AsNoTracking()
            .Include(f => f.Lesson).ThenInclude(l => l.Module).ThenInclude(m => m.Course)
            .Include(f => f.Questions)
            .FirstOrDefaultAsync(f => f.Id == id && f.IsPublished && lessonIds.Contains(f.LessonId));
    }

    private static string LessonContext(Lesson lesson) =>
        $"{lesson.Module.Course.Title} · {lesson.Module.Title}";

    private static FormBuilderViewModel ToBuilderVm(CourseForm form) => new()
    {
        FormId = form.Id,
        LessonId = form.LessonId,
        LessonTitle = form.Lesson.Title,
        LessonContext = LessonContext(form.Lesson),
        Title = form.Title,
        Description = form.Description,
        IsPublished = form.IsPublished,
        ResponseCount = form.Responses.Count,
        Questions = form.Questions
            .OrderBy(q => q.SortOrder)
            .Select(q =>
            {
                var options = FormQuestionEditViewModel.ParseOptionsJson(q.OptionsJson);
                var correct = FormQuestionEditViewModel.ParseOptionsJson(q.CorrectOptionsJson);
                var slots = FormQuestionEditViewModel.NormalizeSlots(options);
                var correctLetter = FormQuestionEditViewModel.LetterForOption(options.ToList(), correct.FirstOrDefault());
                var correctLetters = correct
                    .Select(c => FormQuestionEditViewModel.LetterForOption(options.ToList(), c))
                    .Where(l => l is not null)
                    .Cast<string>()
                    .ToList();

                return new FormQuestionEditViewModel
                {
                    Id = q.Id,
                    Prompt = q.Prompt,
                    Type = q.Type,
                    IsRequired = q.IsRequired,
                    SortOrder = q.SortOrder,
                    OptionSlots = slots,
                    CorrectOptions = correct.ToList(),
                    CorrectLetter = correctLetter,
                    CorrectLetters = correctLetters,
                    CorrectOptionSingle = correct.FirstOrDefault(),
                    CorrectAnswerText = string.Join('\n', correct),
                    FeedbackCorrect = q.FeedbackCorrect,
                    FeedbackIncorrect = q.FeedbackIncorrect
                };
            }).ToList()
    };

    private static FormFillViewModel ToFillVm(CourseForm form, FormResponse? existing)
    {
        var answers = existing?.Answers.ToDictionary(a => a.QuestionId) ?? [];
        var questions = form.Questions.OrderBy(q => q.SortOrder).Select(q =>
        {
            answers.TryGetValue(q.Id, out var answer);
            var correct = FormQuestionEditViewModel.ParseOptionsJson(q.CorrectOptionsJson);
            var selected = FormQuestionEditViewModel.ParseOptionsJson(answer?.SelectedOptionsJson).ToList();
            bool? isCorrect = answer?.IsCorrect;
            if (isCorrect is null && correct.Count > 0 && answer is not null)
            {
                isCorrect = EvaluateStoredAnswer(q, answer, selected);
            }

            return new FormFillQuestionViewModel
            {
                QuestionId = q.Id,
                Prompt = q.Prompt,
                Type = q.Type,
                IsRequired = q.IsRequired,
                Options = FormQuestionEditViewModel.ParseOptionsJson(q.OptionsJson),
                CorrectOptions = correct,
                TextValue = answer?.TextValue,
                SelectedOptions = selected,
                IsCorrect = isCorrect,
                FeedbackCorrect = string.IsNullOrWhiteSpace(q.FeedbackCorrect) ? "Isso mesmo!" : q.FeedbackCorrect,
                FeedbackIncorrect = string.IsNullOrWhiteSpace(q.FeedbackIncorrect)
                    ? "Revise a resposta e tente de novo."
                    : q.FeedbackIncorrect
            };
        }).ToList();

        var graded = questions.Where(q => q.HasGrading).ToList();
        var correctCount = graded.Count(q => q.IsCorrect == true);
        var incorrectCount = graded.Count(q => q.IsCorrect == false);

        return new FormFillViewModel
        {
            FormId = form.Id,
            LessonId = form.LessonId,
            Title = form.Title,
            Description = form.Description,
            LessonTitle = form.Lesson.Title,
            LessonContext = LessonContext(form.Lesson),
            AlreadyResponded = existing is not null,
            ShowResults = existing is not null,
            ScoreGraded = graded.Count > 0 ? graded.Count : null,
            ScoreCorrect = graded.Count > 0 ? correctCount : null,
            ScoreIncorrect = graded.Count > 0 ? incorrectCount : null,
            ScoreBlank = graded.Count(q => !q.WasAnswered),
            Questions = questions
        };
    }

    private async Task RegradeQuestionAnswersAsync(FormQuestion question)
    {
        var answers = await _db.FormAnswers
            .Where(a => a.QuestionId == question.Id)
            .ToListAsync();
        if (answers.Count == 0) return;

        foreach (var answer in answers)
        {
            var selected = FormQuestionEditViewModel.ParseOptionsJson(answer.SelectedOptionsJson);
            answer.IsCorrect = EvaluateStoredAnswer(question, answer, selected);
        }

        await _db.SaveChangesAsync();
    }

    private async Task RegradeResponseAsync(Guid responseId)
    {
        var response = await _db.FormResponses
            .Include(r => r.Answers)
            .Include(r => r.Form).ThenInclude(f => f.Questions)
            .FirstOrDefaultAsync(r => r.Id == responseId);
        if (response is null) return;

        var questions = response.Form.Questions.ToDictionary(q => q.Id);
        var changed = false;
        foreach (var answer in response.Answers)
        {
            if (!questions.TryGetValue(answer.QuestionId, out var question)) continue;
            var selected = FormQuestionEditViewModel.ParseOptionsJson(answer.SelectedOptionsJson);
            var next = EvaluateStoredAnswer(question, answer, selected);
            if (answer.IsCorrect != next)
            {
                answer.IsCorrect = next;
                changed = true;
            }
        }

        // Cria respostas “em branco” para perguntas com gabarito sem FormAnswer.
        foreach (var question in response.Form.Questions)
        {
            if (response.Answers.Any(a => a.QuestionId == question.Id)) continue;
            var correct = FormQuestionEditViewModel.ParseOptionsJson(question.CorrectOptionsJson);
            if (correct.Count == 0) continue;
            _db.FormAnswers.Add(new FormAnswer
            {
                ResponseId = response.Id,
                QuestionId = question.Id,
                IsCorrect = false
            });
            changed = true;
        }

        if (changed) await _db.SaveChangesAsync();
    }

    private static bool? EvaluateStoredAnswer(
        FormQuestion question,
        FormAnswer answer,
        IReadOnlyList<string> selected)
    {
        var correct = FormQuestionEditViewModel.ParseOptionsJson(question.CorrectOptionsJson);
        if (correct.Count == 0) return null;

        if (NeedsOptions(question.Type))
        {
            if (selected.Count == 0) return false;
            var selectedSet = selected.ToHashSet(StringComparer.Ordinal);
            var correctSet = correct.ToHashSet(StringComparer.Ordinal);
            return selectedSet.SetEquals(correctSet);
        }

        if (IsTextType(question.Type))
        {
            var text = answer.TextValue?.Trim() ?? string.Empty;
            if (text.Length == 0) return false;
            return correct.Any(c => string.Equals(c.Trim(), text, StringComparison.OrdinalIgnoreCase));
        }

        return null;
    }

    private static List<string> ResolveCorrectOptions(FormQuestionEditViewModel model)
    {
        var options = model.Options.ToList();

        if (model.Type == FormQuestionType.SingleChoice)
        {
            var letter = model.CorrectLetter ?? model.CorrectOptionSingle;
            var byLetter = FormQuestionEditViewModel.OptionForLetter(options, letter);
            if (!string.IsNullOrWhiteSpace(byLetter)) return [byLetter];

            // Compat: valor antigo com texto completo da opção
            if (!string.IsNullOrWhiteSpace(model.CorrectOptionSingle)
                && options.Contains(model.CorrectOptionSingle.Trim(), StringComparer.Ordinal))
            {
                return [model.CorrectOptionSingle.Trim()];
            }

            return [];
        }

        if (model.Type == FormQuestionType.MultiChoice)
        {
            var fromLetters = (model.CorrectLetters ?? [])
                .Select(l => FormQuestionEditViewModel.OptionForLetter(options, l))
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t!)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (fromLetters.Count > 0) return fromLetters;

            return (model.CorrectOptions ?? [])
                .Where(c => !string.IsNullOrWhiteSpace(c) && options.Contains(c.Trim(), StringComparer.Ordinal))
                .Select(c => c.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        // ShortText / Paragraph: one accepted answer per line
        return FormQuestionEditViewModel.ParseOptions(model.CorrectAnswerText)
            .Select(c => c.Trim())
            .Where(c => c.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool? EvaluateAnswer(
        FormQuestion question,
        FormFillQuestionViewModel? answer,
        IReadOnlyList<string> selected)
    {
        var correct = FormQuestionEditViewModel.ParseOptionsJson(question.CorrectOptionsJson);
        if (correct.Count == 0) return null;

        if (NeedsOptions(question.Type))
        {
            var selectedSet = selected.ToHashSet(StringComparer.Ordinal);
            var correctSet = correct.ToHashSet(StringComparer.Ordinal);
            return selectedSet.SetEquals(correctSet);
        }

        if (IsTextType(question.Type))
        {
            var text = answer?.TextValue?.Trim() ?? string.Empty;
            if (text.Length == 0) return false;
            return correct.Any(c => string.Equals(c.Trim(), text, StringComparison.OrdinalIgnoreCase));
        }

        return null;
    }

    private static bool NeedsOptions(FormQuestionType type) =>
        type is FormQuestionType.SingleChoice or FormQuestionType.MultiChoice;

    private static bool IsTextType(FormQuestionType type) =>
        type is FormQuestionType.ShortText or FormQuestionType.Paragraph;

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
