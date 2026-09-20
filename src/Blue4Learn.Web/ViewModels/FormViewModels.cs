using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Blue4Learn.Web.Domain;

namespace Blue4Learn.Web.ViewModels;

public class FormListViewModel
{
    public bool IsTeacher { get; set; }
    public IReadOnlyList<FormListItemViewModel> Forms { get; set; } = [];
}

public class FormListItemViewModel
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CourseTitle { get; set; } = string.Empty;
    public Guid CourseId { get; set; }
    public bool IsPublished { get; set; }
    public int QuestionCount { get; set; }
    public int ResponseCount { get; set; }
    public bool AlreadyResponded { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public class FormCreateViewModel
{
    [Required(ErrorMessage = "Escolha a disciplina.")]
    public Guid CourseId { get; set; }

    [Required(ErrorMessage = "Informe o título.")]
    [StringLength(200)]
    [Display(Name = "Título")]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    [Display(Name = "Descrição")]
    public string Description { get; set; } = string.Empty;

    public IReadOnlyList<FormCourseOptionViewModel> Courses { get; set; } = [];
}

public class FormCourseOptionViewModel
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
}

public class FormBuilderViewModel
{
    public Guid FormId { get; set; }
    public Guid CourseId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;

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
    public Guid Id { get; set; }

    [Required(ErrorMessage = "Informe o enunciado da pergunta.")]
    [StringLength(1000)]
    public string Prompt { get; set; } = string.Empty;

    public FormQuestionType Type { get; set; } = FormQuestionType.ShortText;
    public bool IsRequired { get; set; } = true;
    public int SortOrder { get; set; }

    /// <summary>One option per line for choice questions.</summary>
    [Display(Name = "Opções (uma por linha)")]
    public string OptionsText { get; set; } = "Opção 1\nOpção 2";

    public IReadOnlyList<string> Options => ParseOptions(OptionsText);

    public static string OptionsToText(string? optionsJson)
    {
        var list = ParseOptionsJson(optionsJson);
        return list.Count == 0 ? "Opção 1\nOpção 2" : string.Join('\n', list);
    }

    public static IReadOnlyList<string> ParseOptions(string? text) =>
        (text ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Take(12)
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
        JsonSerializer.Serialize(options.Where(o => !string.IsNullOrWhiteSpace(o)).ToList());
}

public class FormFillViewModel
{
    public Guid FormId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CourseTitle { get; set; } = string.Empty;
    public bool AlreadyResponded { get; set; }
    public List<FormFillQuestionViewModel> Questions { get; set; } = [];
}

public class FormFillQuestionViewModel
{
    public Guid QuestionId { get; set; }
    public string Prompt { get; set; } = string.Empty;
    public FormQuestionType Type { get; set; }
    public bool IsRequired { get; set; }
    public IReadOnlyList<string> Options { get; set; } = [];
    public string? TextValue { get; set; }
    public List<string> SelectedOptions { get; set; } = [];
}

public class FormResponsesViewModel
{
    public Guid FormId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string CourseTitle { get; set; } = string.Empty;
    public int QuestionCount { get; set; }
    public IReadOnlyList<FormResponseRowViewModel> Responses { get; set; } = [];
}

public class FormResponseRowViewModel
{
    public Guid ResponseId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public DateTime SubmittedAtUtc { get; set; }
    public int AnsweredCount { get; set; }
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
}
