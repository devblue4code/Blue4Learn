using System.ComponentModel.DataAnnotations;
using Blue4Learn.Web.Domain;

namespace Blue4Learn.Web.ViewModels;

public class SyllabusViewModel
{
    public Guid CourseId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SyllabusHtml { get; set; } = string.Empty;
    public string MethodologiesHtml { get; set; } = string.Empty;
    public string? ClassName { get; set; }
    public string? ClassCode { get; set; }
    public List<SyllabusModuleViewModel> Modules { get; set; } = [];
}

public class SyllabusModuleViewModel
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public List<SyllabusLessonViewModel> Lessons { get; set; } = [];
}

public class SyllabusLessonViewModel
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Objective { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public ContentStatus Status { get; set; }
}

public class CourseComponentsViewModel
{
    public Guid CourseId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public List<ModuleEditorItemViewModel> Modules { get; set; } = [];
    public ModuleCreateViewModel NewModule { get; set; } = new();
}

public class ModuleEditorItemViewModel
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public int LessonCount { get; set; }
    public List<SyllabusLessonViewModel> Lessons { get; set; } = [];
}

public class ModuleCreateViewModel
{
    public Guid CourseId { get; set; }

    [Required(ErrorMessage = "Informe o título do módulo.")]
    [MaxLength(200)]
    [Display(Name = "Título do módulo")]
    public string Title { get; set; } = string.Empty;
}

public class ClassJournalsViewModel
{
    public string ClassName { get; set; } = string.Empty;
    public string CourseTitle { get; set; } = string.Empty;
    public List<ClassJournalItemViewModel> Entries { get; set; } = [];
}

public class ClassJournalItemViewModel
{
    public Guid Id { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string StudentUserId { get; set; } = string.Empty;
    public Guid LessonId { get; set; }
    public string LessonTitle { get; set; } = string.Empty;
    public string Reflection { get; set; } = string.Empty;
    public bool NeedsReview { get; set; }
    public bool UnderstoodObjective { get; set; }
    public int OpenQuestions { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
