using System.ComponentModel.DataAnnotations;
using Blue4Learn.Web.Domain;

namespace Blue4Learn.Web.ViewModels;

public class HomeDashboardViewModel
{
    public string FullName { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public bool IsTeacher { get; set; }
    public int RegisteredCount { get; set; }
    public int PendingCount { get; set; }
    public int NeedsReviewCount { get; set; }
    public int ProgressPercent { get; set; }
    public int LearningProgressPercent { get; set; }
    public LessonSummaryViewModel? NextLesson { get; set; }
    public StudentRiskBannerViewModel? RiskBanner { get; set; }
    public IReadOnlyList<ClassSummaryViewModel> Classes { get; set; } = [];
    public IReadOnlyList<LessonSummaryViewModel> RecentLessons { get; set; } = [];
}

public class ClassSummaryViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string CourseTitle { get; set; } = string.Empty;
    public int StudentCount { get; set; }
}

public class LessonSummaryViewModel
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ModuleTitle { get; set; } = string.Empty;
    public string Objective { get; set; } = string.Empty;
    public bool HasJournal { get; set; }
    public bool NeedsReview { get; set; }
    public int LearningProgressPercent { get; set; }
    public int SortOrder { get; set; }
    public bool IsNext { get; set; }
}

public class LessonWorkspaceViewModel
{
    public Guid LessonId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Objective { get; set; } = string.Empty;
    public string ModuleTitle { get; set; } = string.Empty;
    public string CourseTitle { get; set; } = string.Empty;
    public string ContentHtml { get; set; } = string.Empty;
    public Guid? PreviousLessonId { get; set; }
    public Guid? NextLessonId { get; set; }
    public JournalFormViewModel Journal { get; set; } = new();
    public ActivityFormViewModel? Activity { get; set; }
    public IReadOnlyList<ConceptOptionViewModel> Concepts { get; set; } = [];
    public int ProgressPercent { get; set; }
    public int LessonLearningPercent { get; set; }
    public int ModuleLessonCount { get; set; }
    public int RegisteredInModule { get; set; }
    public bool HasJournal { get; set; }
    public int SortOrder { get; set; }

    public IReadOnlyList<string> RiskItems { get; set; } = [];

    public bool JournalDone => HasJournal;
    public bool JournalNeedsAttention => HasJournal && Journal.NeedsReview;

    public bool ActivityDone =>
        Activity is not null
        && (Activity.Status is ActivityStatus.Submitted or ActivityStatus.Reviewed
            || (!string.IsNullOrWhiteSpace(Activity.ProblemDescription)
                && !string.IsNullOrWhiteSpace(Activity.SolutionDescription)));

    public bool ActivityStarted =>
        Activity is not null
        && (Activity.Status >= ActivityStatus.InProgress
            || !string.IsNullOrWhiteSpace(Activity.ProblemDescription)
            || !string.IsNullOrWhiteSpace(Activity.SolutionDescription));

    public bool EvidenceDone =>
        Activity is not null
        && (Activity.Attachments.Count > 0
            || !string.IsNullOrWhiteSpace(Activity.TextResponse)
            || !string.IsNullOrWhiteSpace(Activity.GitHubUrl));

    /// <summary>journal | activity | evidence | next | progress</summary>
    public string NextStepKey { get; set; } = "journal";
    public string NextStepLabel { get; set; } = "Registrar o que aprendi";
}

public class ConceptOptionViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Marked { get; set; }
    public bool Understood { get; set; }
}

public class JournalFormViewModel
{
    public Guid LessonId { get; set; }

    [Display(Name = "Anotação privada (opcional)")]
    [StringLength(4000)]
    public string? Note { get; set; }

    [Display(Name = "Reflexão final (opcional)")]
    [StringLength(4000)]
    public string? Reflection { get; set; }

    [Display(Name = "Compreendi o objetivo da aula")]
    public bool UnderstoodObjective { get; set; }

    [Display(Name = "Pratiquei o conceito principal")]
    public bool PracticedConcept { get; set; }

    [Display(Name = "Preciso revisar este conteúdo")]
    public bool NeedsReview { get; set; }

    [Display(Name = "Nova dúvida (opcional)")]
    [StringLength(1000)]
    public string? NewQuestion { get; set; }

    public List<Guid> MarkedConceptIds { get; set; } = [];
    public List<Guid> UnderstoodConceptIds { get; set; } = [];
    public IReadOnlyList<QuestionItemViewModel> Questions { get; set; } = [];
}

public class QuestionItemViewModel
{
    public Guid Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public QuestionStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class ActivityFormViewModel
{
    public Guid ActivityId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public string PromptHtml { get; set; } = string.Empty;
    public DateTime? DueAtUtc { get; set; }
    public bool RequiresGitHubDelivery { get; set; }

    [Display(Name = "Descrição do problema")]
    [StringLength(2000)]
    public string ProblemDescription { get; set; } = string.Empty;

    [Display(Name = "Descrição da solução")]
    [StringLength(2000)]
    public string SolutionDescription { get; set; } = string.Empty;

    [Display(Name = "Resposta / evidência textual")]
    [StringLength(4000)]
    public string TextResponse { get; set; } = string.Empty;

    [Display(Name = "Link do GitHub")]
    [Url]
    [StringLength(500)]
    public string? GitHubUrl { get; set; }

    [Display(Name = "URL do Pull Request")]
    [Url]
    [StringLength(500)]
    public string? GitHubPrUrl { get; set; }

    [Display(Name = "Recado para a professora (opcional)")]
    [StringLength(500)]
    public string? DeliveryNote { get; set; }

    public ActivityStatus Status { get; set; }
    public string? TeacherFeedback { get; set; }

    [Display(Name = "Anexo (opcional)")]
    public IFormFile? Attachment { get; set; }

    public IReadOnlyList<AttachmentItemViewModel> Attachments { get; set; } = [];
}

public class AttachmentItemViewModel
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
}

public class TeacherDashboardViewModel
{
    public string FirstName { get; set; } = string.Empty;
    public bool HasClass { get; set; }
    public Guid? ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public string ClassCode { get; set; } = string.Empty;
    public string CourseTitle { get; set; } = string.Empty;
    public int StudentCount { get; set; }
    public int PublishedLessons { get; set; }
    public int OpenQuestions { get; set; }
    public int PendingJournals { get; set; }
    public int NeedsReviewJournals { get; set; }
    public int SubmittedActivities { get; set; }
    public int AwaitingFeedback { get; set; }
    public int CoveragePercent { get; set; }
    public IReadOnlyList<TeacherClassOptionViewModel> ClassOptions { get; set; } = [];
    public IReadOnlyList<ConceptStatViewModel> TopConcepts { get; set; } = [];
    public IReadOnlyList<StudentProgressViewModel> Students { get; set; } = [];
    public IReadOnlyList<QuestionFeedItemViewModel> RecentQuestions { get; set; } = [];
}

public class TeacherClassOptionViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

public class ConceptStatViewModel
{
    public string Name { get; set; } = string.Empty;
    public int Marks { get; set; }
    public int Understood { get; set; }
    public int UnderstoodPercent => Marks == 0 ? 0 : (int)Math.Round(100.0 * Understood / Marks);
}

public class StudentProgressViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int JournalCount { get; set; }
    public int ExpectedJournals { get; set; }
    public int ProgressPercent { get; set; }
    public int LearningProgressPercent { get; set; }
    public int OpenQuestions { get; set; }
    public int NeedsReviewCount { get; set; }
    public int SubmittedActivities { get; set; }
    public int AwaitingFeedback { get; set; }
    public Guid? FocusLessonId { get; set; }
    public Guid? FocusSubmissionId { get; set; }
    public int RiskReasonCount { get; set; }
    public bool NeedsAttention => OpenQuestions > 0 || NeedsReviewCount > 0 || AwaitingFeedback > 0
        || RiskReasonCount > 0
        || (ExpectedJournals > 0 && JournalCount < ExpectedJournals);
}

public class StudentRiskBannerViewModel
{
    public IReadOnlyList<string> Items { get; set; } = [];
    public int LearningProgressPercent { get; set; }
    public bool HasItems => Items.Count > 0;
}

public class LearningRiskReason
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid? LessonId { get; set; }
    public string? LessonTitle { get; set; }
    public Guid? SubmissionId { get; set; }
    public Guid? QuestionId { get; set; }
}

public class StudentLearningSummary
{
    public int LearningProgressPercent { get; set; }
    public int RiskReasonCount { get; set; }
}

public class TeacherAlertsViewModel
{
    public Guid ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public string ClassCode { get; set; } = string.Empty;
    public string CourseTitle { get; set; } = string.Empty;
    public int StudentCount { get; set; }
    public int AtRiskCount { get; set; }
    public IReadOnlyList<TeacherClassOptionViewModel> ClassOptions { get; set; } = [];
    public IReadOnlyList<StudentAlertItemViewModel> Items { get; set; } = [];
}

public class StudentAlertItemViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int LearningProgressPercent { get; set; }
    public IReadOnlyList<LearningRiskReason> Reasons { get; set; } = [];
}

public class QuestionFeedItemViewModel
{
    public Guid Id { get; set; }
    public string StudentUserId { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public Guid LessonId { get; set; }
    public string LessonTitle { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}

public class StudentDetailViewModel
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public IReadOnlyList<StudentLessonRecordViewModel> Records { get; set; } = [];
}

public class StudentLessonRecordViewModel
{
    public Guid LessonId { get; set; }
    public string LessonTitle { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public string Reflection { get; set; } = string.Empty;
    public bool NeedsReview { get; set; }
    public Guid? SubmissionId { get; set; }
    public ActivityStatus? ActivityStatus { get; set; }
    public int LearningProgressPercent { get; set; }
    public IReadOnlyList<string> Questions { get; set; } = [];
    public IReadOnlyList<string> Concepts { get; set; } = [];
}
