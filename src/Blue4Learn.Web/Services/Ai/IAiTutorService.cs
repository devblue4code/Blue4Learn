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
