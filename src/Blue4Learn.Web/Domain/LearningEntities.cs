namespace Blue4Learn.Web.Domain;

public class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public ICollection<Course> Courses { get; set; } = [];
    public ICollection<ClassGroup> Classes { get; set; } = [];
    public ICollection<ApplicationUser> Users { get; set; } = [];
}

public class Course
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    /// <summary>Professora responsável pela disciplina (vê só esta disciplina e suas turmas).</summary>
    public string? TeacherUserId { get; set; }
    public ApplicationUser? Teacher { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    /// <summary>Texto completo da ementa (Markdown).</summary>
    public string Syllabus { get; set; } = string.Empty;
    /// <summary>Metodologias de ensino (Markdown).</summary>
    public string Methodologies { get; set; } = string.Empty;
    public ICollection<Module> Modules { get; set; } = [];
    public ICollection<ClassGroup> Classes { get; set; } = [];
    public ICollection<Quiz> Quizzes { get; set; } = [];
}

public class Module
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public ICollection<Lesson> Lessons { get; set; } = [];
}

public class Lesson
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ModuleId { get; set; }
    public Module Module { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Objective { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public ContentStatus Status { get; set; } = ContentStatus.Draft;
    public ContentDocument? ContentDocument { get; set; }
    public ICollection<Concept> Concepts { get; set; } = [];
    public ICollection<Activity> Activities { get; set; } = [];
    public ICollection<StudentJournalEntry> JournalEntries { get; set; } = [];
}

public class ContentDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LessonId { get; set; }
    public Lesson Lesson { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string Markdown { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class Concept
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LessonId { get; set; }
    public Lesson Lesson { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ICollection<ConceptMark> Marks { get; set; } = [];
}

public class ClassGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public Guid CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public ICollection<Enrollment> Enrollments { get; set; } = [];
}

public class Enrollment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClassGroupId { get; set; }
    public ClassGroup ClassGroup { get; set; } = null!;
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    public DateTime EnrolledAtUtc { get; set; } = DateTime.UtcNow;
}

public class Activity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LessonId { get; set; }
    public Lesson Lesson { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public DateTime? DueAtUtc { get; set; }
    public ICollection<ActivitySubmission> Submissions { get; set; } = [];
}

public class ActivitySubmission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ActivityId { get; set; }
    public Activity Activity { get; set; } = null!;
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    public string TextResponse { get; set; } = string.Empty;
    public string? GitHubUrl { get; set; }
    public string ProblemDescription { get; set; } = string.Empty;
    public string SolutionDescription { get; set; } = string.Empty;
    public string? TeacherFeedback { get; set; }
    public ActivityStatus Status { get; set; } = ActivityStatus.NotStarted;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<SubmissionAttachment> Attachments { get; set; } = [];
}

public class SubmissionAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SubmissionId { get; set; }
    public ActivitySubmission Submission { get; set; } = null!;
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long SizeBytes { get; set; }
    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
}

public class StudentJournalEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LessonId { get; set; }
    public Lesson Lesson { get; set; } = null!;
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    public string Note { get; set; } = string.Empty;
    public string Reflection { get; set; } = string.Empty;
    public bool UnderstoodObjective { get; set; }
    public bool PracticedConcept { get; set; }
    public bool NeedsReview { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<JournalQuestion> Questions { get; set; } = [];
    public ICollection<ConceptMark> ConceptMarks { get; set; } = [];
}

public class JournalQuestion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid JournalEntryId { get; set; }
    public StudentJournalEntry JournalEntry { get; set; } = null!;
    public string Text { get; set; } = string.Empty;
    public QuestionStatus Status { get; set; } = QuestionStatus.Open;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class ConceptMark
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid JournalEntryId { get; set; }
    public StudentJournalEntry JournalEntry { get; set; } = null!;
    public Guid ConceptId { get; set; }
    public Concept Concept { get; set; } = null!;
    public bool Understood { get; set; }
}

public class Quiz
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsPublished { get; set; } = true;
    public ICollection<QuizQuestion> Questions { get; set; } = [];
    public ICollection<QuizAttempt> Attempts { get; set; } = [];
}

public class QuizQuestion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid QuizId { get; set; }
    public Quiz Quiz { get; set; } = null!;
    public string Prompt { get; set; } = string.Empty;
    public string OptionA { get; set; } = string.Empty;
    public string OptionB { get; set; } = string.Empty;
    public string OptionC { get; set; } = string.Empty;
    public string OptionD { get; set; } = string.Empty;
    public string CorrectOption { get; set; } = "A";
    public int SortOrder { get; set; }
}

public class QuizAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid QuizId { get; set; }
    public Quiz Quiz { get; set; } = null!;
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    public int Score { get; set; }
    public int MaxScore { get; set; }
    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;
}
