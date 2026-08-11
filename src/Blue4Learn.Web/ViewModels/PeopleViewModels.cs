using System.ComponentModel.DataAnnotations;
using Blue4Learn.Web.Domain;

namespace Blue4Learn.Web.ViewModels;

public class PeopleListViewModel
{
    public IReadOnlyList<PersonListItemViewModel> People { get; set; } = [];
}

public class PersonListItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string RoleLabel { get; set; } = string.Empty;
    public string? CourseTitle { get; set; }
    public int ClassCount { get; set; }
}

public class PersonFormViewModel
{
    public string? Id { get; set; }

    [Required(ErrorMessage = "Informe o nome completo.")]
    [StringLength(120)]
    [Display(Name = "Nome completo")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o e-mail.")]
    [EmailAddress]
    [Display(Name = "E-mail")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Selecione o perfil.")]
    [Display(Name = "Perfil")]
    public string Role { get; set; } = AppRoles.Student;

    [Display(Name = "Disciplina (professora)")]
    public Guid? CourseId { get; set; }

    public IReadOnlyList<CourseOptionViewModel> Courses { get; set; } = [];
    public IReadOnlyList<PersonEnrollmentViewModel> Enrollments { get; set; } = [];
    public IReadOnlyList<ClassOptionViewModel> AvailableClasses { get; set; } = [];

    [Display(Name = "Matricular em turma")]
    public Guid? EnrollClassGroupId { get; set; }

    public bool IsEdit => !string.IsNullOrEmpty(Id);
}

public class PersonEnrollmentViewModel
{
    public Guid EnrollmentId { get; set; }
    public Guid ClassGroupId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public string ClassCode { get; set; } = string.Empty;
    public string CourseTitle { get; set; } = string.Empty;
}

public class ClassOptionViewModel
{
    public Guid Id { get; set; }
    public string Label { get; set; } = string.Empty;
}
