namespace Blue4Learn.Web.Services.Ai;

public interface IAiTutorService
{
    bool IsEnabled { get; }

    Task<IReadOnlyList<string>> SuggestConceptsAsync(
        string? markdown,
        string? objective,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> SuggestGuidingQuestionsAsync(
        GuidingQuestionContext context,
        CancellationToken cancellationToken = default);

    Task<EvidenceAnalysisResult> AnalyzeEvidenceAsync(
        EvidenceAnalysisRequest request,
        CancellationToken cancellationToken = default);
}

public class GuidingQuestionContext
{
    public string LessonTitle { get; set; } = string.Empty;
    public string Objective { get; set; } = string.Empty;
    public IReadOnlyList<string> AllConcepts { get; set; } = [];
    public IReadOnlyList<string> MarkedConcepts { get; set; } = [];
    public IReadOnlyList<string> UnderstoodConcepts { get; set; } = [];
    public IReadOnlyList<string> OpenQuestions { get; set; } = [];
    public bool NeedsReview { get; set; }
    public bool UnderstoodObjective { get; set; }
    public string? NoteExcerpt { get; set; }
}

public class EvidenceAnalysisRequest
{
    public string ActivityTitle { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public string ProblemDescription { get; set; } = string.Empty;
    public string SolutionDescription { get; set; } = string.Empty;
    public string TextResponse { get; set; } = string.Empty;
    public string? GitHubUrl { get; set; }
    public IReadOnlyList<string> AttachmentNames { get; set; } = [];
    public IReadOnlyList<EvidenceCommitInfo> Commits { get; set; } = [];
}

public class EvidenceCommitInfo
{
    public string Sha { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public IReadOnlyList<EvidenceCommitFileInfo> Files { get; set; } = [];
}

public class EvidenceCommitFileInfo
{
    public string Filename { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Additions { get; set; }
    public int Deletions { get; set; }
    public string? PatchExcerpt { get; set; }
}

public class EvidenceAnalysisResult
{
    public string Summary { get; set; } = string.Empty;
    public List<EvidenceChecklistItem> Checklist { get; set; } = [];
    public string FeedbackDraft { get; set; } = string.Empty;
    public bool UsedLlm { get; set; }
}

public class EvidenceChecklistItem
{
    public string Item { get; set; } = string.Empty;
    /// <summary>met | partial | missing</summary>
    public string Status { get; set; } = "missing";
    public string EvidenceNote { get; set; } = string.Empty;
}
