using System.ComponentModel.DataAnnotations;
using Blue4Learn.Web.Domain;

namespace Blue4Learn.Web.ViewModels;

public class ClassListViewModel
{
    public IReadOnlyList<ClassListItemViewModel> Classes { get; set; } = [];
}

public class ClassListItemViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string CourseTitle { get; set; } = string.Empty;
    public int MemberCount { get; set; }
    public int StudentCount { get; set; }
    public bool IsMember { get; set; }
}

public class ClassFormViewModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Informe o nome da turma.")]
    [StringLength(160)]
    [Display(Name = "Nome da turma")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o código.")]
    [StringLength(40)]
    [Display(Name = "Código")]
    [RegularExpression(@"^[A-Za-z0-9\-_.]+$", ErrorMessage = "Use letras, números, hífen, ponto ou underscore.")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Selecione a disciplina.")]
    [Display(Name = "Disciplina")]
    public Guid CourseId { get; set; }

    public IReadOnlyList<CourseOptionViewModel> Courses { get; set; } = [];
    public bool IsEdit => Id.HasValue;
}

public class CourseOptionViewModel
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
}

public class ClassDetailsViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string CourseTitle { get; set; } = string.Empty;
    public Guid CourseId { get; set; }
    public IReadOnlyList<ClassMemberViewModel> Members { get; set; } = [];
    public EnrollMemberFormViewModel EnrollForm { get; set; } = new();
}

public class ClassMemberViewModel
{
    public Guid EnrollmentId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string RoleLabel { get; set; } = string.Empty;
    public DateTime EnrolledAtUtc { get; set; }
    public bool CanRemove { get; set; }
}

public class EnrollMemberFormViewModel
{
    public Guid ClassGroupId { get; set; }

    [Required(ErrorMessage = "Informe o e-mail.")]
    [EmailAddress]
    [Display(Name = "E-mail do participante")]
    public string Email { get; set; } = string.Empty;

    [StringLength(120)]
    [Display(Name = "Nome completo (se for criar conta)")]
    public string? FullName { get; set; }

    [Display(Name = "Papel")]
    public string Role { get; set; } = AppRoles.Student;
}

public class JoinClassViewModel
{
    [Required(ErrorMessage = "Informe o código da turma.")]
    [StringLength(40)]
    [Display(Name = "Código da turma")]
    public string Code { get; set; } = string.Empty;
}
