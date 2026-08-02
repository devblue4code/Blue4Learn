using System.ComponentModel.DataAnnotations;

namespace Blue4Learn.Web.Domain;

public enum ContentStatus
{
    [Display(Name = "Rascunho")]
    Draft = 0,

    [Display(Name = "Publicado")]
    Published = 1,

    [Display(Name = "Arquivado")]
    Archived = 2
}

public enum ActivityStatus
{
    NotStarted = 0,
    InProgress = 1,
    Submitted = 2,
    Reviewed = 3
}

public enum QuestionStatus
{
    Open = 0,
    Resolved = 1
}

public static class AppRoles
{
    public const string Student = "Estudante";
    public const string Teacher = "Professora";
    public const string Admin = "Administrador";

    public static readonly string[] All = [Student, Teacher, Admin];
}
