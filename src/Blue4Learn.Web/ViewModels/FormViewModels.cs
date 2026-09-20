using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Blue4Learn.Web.Domain;

namespace Blue4Learn.Web.ViewModels;

public class FormListViewModel
{
    public bool IsTeacher { get; set; }
    public Guid? FilterLessonId { get; set; }
    public string? FilterLessonTitle { get; set; }
    public IReadOnlyList<FormListItemViewModel> Forms { get; set; } = [];
}

public class FormListItemViewModel
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string LessonTitle { get; set; } = string.Empty;
    public string LessonContext { get; set; } = string.Empty;
    public Guid LessonId { get; set; }
    public bool IsPublished { get; set; }
    public int QuestionCount { get; set; }
    public int ResponseCount { get; set; }
    public bool AlreadyResponded { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public class FormCreateViewModel
{
    [Required(ErrorMessage = "Escolha a aula.")]
    public Guid? LessonId { get; set; }

    [Required(ErrorMessage = "Informe o título.")]
    [StringLength(200)]
    [Display(Name = "Título")]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    [Display(Name = "Descrição")]
    public string Description { get; set; } = string.Empty;

    public IReadOnlyList<FormLessonOptionViewModel> Lessons { get; set; } = [];
}

public class FormLessonOptionViewModel
{
    public Guid Id { get; set; }
    public string Label { get; set; } = string.Empty;
}

public class FormBuilderViewModel
{
    public Guid FormId { get; set; }
    public Guid LessonId { get; set; }
    public string LessonTitle { get; set; } = string.Empty;
    public string LessonContext { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    public bool IsPublished { get; set; }
    public int ResponseCount { get; set; }
    public List<FormQuestionEditViewModel> Questions { get; set; } = [];
}

public class FormQuestionEditViewModel
{
    public static readonly string[] ChoiceLetters = ["A", "B", "C", "D"];

    public Guid Id { get; set; }

    [Required(ErrorMessage = "Informe o enunciado da pergunta.")]
    [StringLength(1000)]
    public string Prompt { get; set; } = string.Empty;

    public FormQuestionType Type { get; set; } = FormQuestionType.ShortText;
    public bool IsRequired { get; set; } = true;
    public int SortOrder { get; set; }

    /// <summary>Textos das alternativas A–D (índice 0 = A).</summary>
    public List<string> OptionSlots { get; set; } = ["", "", "", ""];

    [Display(Name = "Opções (uma por linha)")]
    public string OptionsText
    {
        get => string.Join('\n', OptionSlots.Where(o => !string.IsNullOrWhiteSpace(o)));
        set => OptionSlots = NormalizeSlots(ParseOptions(value));
    }

    /// <summary>Correct option labels (full text).</summary>
    public List<string> CorrectOptions { get; set; } = [];

    /// <summary>Letter A–D for single-choice gabarito.</summary>
    [Display(Name = "Resposta correta")]
    public string? CorrectLetter { get; set; }

    /// <summary>Letters A–D for multi-choice gabarito.</summary>
    public List<string> CorrectLetters { get; set; } = [];

    [Display(Name = "Resposta correta (texto exato de uma opção)")]
    public string? CorrectOptionSingle { get; set; }

    /// <summary>Gabarito para texto curto/parágrafo (uma resposta aceita por linha).</summary>
    [Display(Name = "Resposta(s) correta(s)")]
    public string? CorrectAnswerText { get; set; }

    [StringLength(1000)]
    [Display(Name = "Feedback se acertar")]
    public string? FeedbackCorrect { get; set; }

    [StringLength(1000)]
    [Display(Name = "Feedback se errar")]
    public string? FeedbackIncorrect { get; set; }

    public IReadOnlyList<string> Options =>
        OptionSlots.Where(o => !string.IsNullOrWhiteSpace(o)).Select(o => o.Trim()).ToList();

    public static List<string> NormalizeSlots(IEnumerable<string>? options)
    {
        var list = (options ?? [])
            .Where(o => !string.IsNullOrWhiteSpace(o))
            .Select(o => o.Trim())
            .Take(4)
            .ToList();
        while (list.Count < 4) list.Add(string.Empty);
        return list;
    }

    public static string OptionsToText(string? optionsJson)
    {
        var list = ParseOptionsJson(optionsJson);
        return list.Count == 0 ? "" : string.Join('\n', list);
    }

    public static IReadOnlyList<string> ParseOptions(string? text) =>
        (text ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Take(4)
            .ToList();

    public static IReadOnlyList<string> ParseOptionsJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static string ToOptionsJson(IEnumerable<string> options) =>
        JsonSerializer.Serialize(options.Where(o => !string.IsNullOrWhiteSpace(o)).Select(o => o.Trim()).Take(4).ToList());

    public static string? LetterForOption(IReadOnlyList<string> options, string? optionText)
    {
        if (string.IsNullOrWhiteSpace(optionText) || options.Count == 0) return null;
        for (var i = 0; i < options.Count && i < 4; i++)
        {
            if (string.Equals(options[i], optionText, StringComparison.Ordinal))
            {
                return ChoiceLetters[i];
            }
        }

        // Already a letter?
        var letter = optionText.Trim().ToUpperInvariant();
        if (ChoiceLetters.Contains(letter)) return letter;
        return null;
    }

    public static string? OptionForLetter(IReadOnlyList<string> options, string? letter)
    {
        if (string.IsNullOrWhiteSpace(letter)) return null;
        var idx = Array.IndexOf(ChoiceLetters, letter.Trim().ToUpperInvariant());
        if (idx < 0 || idx >= options.Count) return null;
        var text = options[idx];
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}

public class FormFillViewModel
{
    public Guid FormId { get; set; }
    public Guid LessonId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string LessonTitle { get; set; } = string.Empty;
    public string LessonContext { get; set; } = string.Empty;
    public bool AlreadyResponded { get; set; }
    public bool IsTeacherPreview { get; set; }
    public bool ShowResults { get; set; }
    /// <summary>Após enviar, o aluno só vê o resultado (sem nova tentativa).</summary>
    public bool IsLocked => AlreadyResponded && !IsTeacherPreview;
    public int SecondsPerQuestion { get; set; } = 40;
    public int? ScoreCorrect { get; set; }
    public int? ScoreIncorrect { get; set; }
    public int? ScoreGraded { get; set; }
    public int ScoreBlank { get; set; }
    public List<FormFillQuestionViewModel> Questions { get; set; } = [];
}

public class FormFillQuestionViewModel
{
    public Guid QuestionId { get; set; }
    public string Prompt { get; set; } = string.Empty;
    public FormQuestionType Type { get; set; }
    public bool IsRequired { get; set; }
    public IReadOnlyList<string> Options { get; set; } = [];
    public IReadOnlyList<string> CorrectOptions { get; set; } = [];
    public string? TextValue { get; set; }
    public List<string> SelectedOptions { get; set; } = [];
    public bool? IsCorrect { get; set; }
    public string? FeedbackCorrect { get; set; }
    public string? FeedbackIncorrect { get; set; }
    public bool HasGrading => CorrectOptions.Count > 0;
    public bool WasAnswered =>
        Type is FormQuestionType.ShortText or FormQuestionType.Paragraph
            ? !string.IsNullOrWhiteSpace(TextValue)
            : SelectedOptions.Count > 0;
    public string? FeedbackShown =>
        IsCorrect == true ? FeedbackCorrect
        : IsCorrect == false ? FeedbackIncorrect
        : null;
}

public class FormResponsesViewModel
{
    public Guid FormId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string LessonTitle { get; set; } = string.Empty;
    public string LessonContext { get; set; } = string.Empty;
    public int QuestionCount { get; set; }
    public IReadOnlyList<FormResponseRowViewModel> Responses { get; set; } = [];
}

public class FormResponseRowViewModel
{
    public Guid ResponseId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public DateTime SubmittedAtUtc { get; set; }
    public int AnsweredCount { get; set; }
    public int? CorrectCount { get; set; }
    public int? GradedCount { get; set; }
}

public class FormResponseDetailViewModel
{
    public Guid FormId { get; set; }
    public string FormTitle { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string StudentEmail { get; set; } = string.Empty;
    public DateTime SubmittedAtUtc { get; set; }
    public IReadOnlyList<FormAnswerDetailViewModel> Answers { get; set; } = [];
}

public class FormAnswerDetailViewModel
{
    public string Prompt { get; set; } = string.Empty;
    public FormQuestionType Type { get; set; }
    public string DisplayValue { get; set; } = string.Empty;
    public bool? IsCorrect { get; set; }
    public IReadOnlyList<string> Options { get; set; } = [];
    public IReadOnlyList<string> SelectedOptions { get; set; } = [];
    public IReadOnlyList<string> CorrectOptions { get; set; } = [];
    public string? FeedbackShown { get; set; }
}
