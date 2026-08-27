using System.ComponentModel.DataAnnotations;
using Blue4Learn.Web.Domain;

namespace Blue4Learn.Web.ViewModels;

public class ContentLibraryViewModel
{
    public string CourseTitle { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public IReadOnlyList<ContentLessonItemViewModel> Lessons { get; set; } = [];
}

public class ContentLessonItemViewModel
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string ModuleTitle { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public ContentStatus Status { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public int ConceptCount { get; set; }
    public IReadOnlyList<string> ConceptNames { get; set; } = [];
}

public class LessonEditorViewModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Selecione o módulo.")]
    [Display(Name = "Módulo")]
    public Guid ModuleId { get; set; }

    [Required(ErrorMessage = "Informe o título.")]
    [StringLength(200)]
    [Display(Name = "Título")]
    public string Title { get; set; } = string.Empty;

    [StringLength(100)]
    [Display(Name = "Slug")]
    [RegularExpression(@"^[a-z0-9]+(?:-[a-z0-9]+)*$", ErrorMessage = "Use apenas minúsculas, números e hífens.")]
    public string? Slug { get; set; }

    [Required(ErrorMessage = "Informe o objetivo da aula.")]
    [StringLength(500)]
    [Display(Name = "Objetivo")]
    public string Objective { get; set; } = string.Empty;

    [Range(1, 999)]
    [Display(Name = "Ordem")]
    public int SortOrder { get; set; } = 1;

    [Display(Name = "Status")]
    public ContentStatus Status { get; set; } = ContentStatus.Draft;

    [Required(ErrorMessage = "Escreva o conteúdo em Markdown.")]
    [Display(Name = "Conteúdo Markdown")]
    public string Markdown { get; set; } = string.Empty;

    [Display(Name = "Conceitos")]
    public string ConceptsText { get; set; } = string.Empty;

    [Display(Name = "Enunciado da atividade")]
    [StringLength(2000)]
    public string? ActivityPrompt { get; set; }

    [Display(Name = "Importar arquivo .md")]
    public IFormFile? MarkdownFile { get; set; }

    public IReadOnlyList<ModuleOptionViewModel> Modules { get; set; } = [];
    public string? PreviewHtml { get; set; }
    public bool IsEdit => Id.HasValue;
    public bool AiEnabled { get; set; } = true;
}

public class ModuleOptionViewModel
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
}

public class MarkdownPreviewRequest
{
    public string? Markdown { get; set; }
}

public class SuggestConceptsRequest
{
    public string? Markdown { get; set; }
    public string? Objective { get; set; }
}

public class GuidingQuestionsRequest
{
    public Guid LessonId { get; set; }
}
