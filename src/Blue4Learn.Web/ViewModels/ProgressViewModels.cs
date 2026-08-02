using Blue4Learn.Web.Domain;

namespace Blue4Learn.Web.ViewModels;

public class MyProgressViewModel
{
    public string FirstName { get; set; } = string.Empty;
    public string Filter { get; set; } = "all";
    public int TotalLessons { get; set; }
    public int RegisteredCount { get; set; }
    public int NeedsReviewCount { get; set; }
    public int MissingCount { get; set; }
    public int DoneCount { get; set; }
    public int ProgressPercent { get; set; }
    public MyProgressItemViewModel? NextAction { get; set; }
    public IReadOnlyList<MyProgressItemViewModel> Items { get; set; } = [];
}

public class MyProgressItemViewModel
{
    public Guid LessonId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ModuleTitle { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool HasJournal { get; set; }
    public bool NeedsReview { get; set; }
    public bool HasOpenQuestion { get; set; }
    public bool IsNext { get; set; }
    public ActivityStatus? ActivityStatus { get; set; }
    public DateTime? LastJournalUpdateUtc { get; set; }

    public bool RequiresAttention => !HasJournal || NeedsReview || HasOpenQuestion;
}

public class SubmissionListViewModel
{
    public string ClassName { get; set; } = string.Empty;
    public IReadOnlyList<SubmissionListItemViewModel> Items { get; set; } = [];
}

public class SubmissionListItemViewModel
{
    public Guid SubmissionId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string LessonTitle { get; set; } = string.Empty;
    public string ActivityTitle { get; set; } = string.Empty;
    public ActivityStatus Status { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public int AttachmentCount { get; set; }
    public bool HasFeedback { get; set; }
}

public class SubmissionReviewViewModel
{
    public Guid SubmissionId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string StudentEmail { get; set; } = string.Empty;
    public string LessonTitle { get; set; } = string.Empty;
    public Guid LessonId { get; set; }
    public string ActivityTitle { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public string ProblemDescription { get; set; } = string.Empty;
    public string SolutionDescription { get; set; } = string.Empty;
    public string TextResponse { get; set; } = string.Empty;
    public string? GitHubUrl { get; set; }
    public ActivityStatus Status { get; set; }
    public string? TeacherFeedback { get; set; }
    public IReadOnlyList<AttachmentItemViewModel> Attachments { get; set; } = [];
}
