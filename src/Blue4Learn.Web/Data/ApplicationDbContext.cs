using Blue4Learn.Web.Domain;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Blue4Learn.Web.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Module> Modules => Set<Module>();
    public DbSet<Lesson> Lessons => Set<Lesson>();
    public DbSet<ContentDocument> ContentDocuments => Set<ContentDocument>();
    public DbSet<Concept> Concepts => Set<Concept>();
    public DbSet<ClassGroup> ClassGroups => Set<ClassGroup>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<ActivitySubmission> ActivitySubmissions => Set<ActivitySubmission>();
    public DbSet<SubmissionAttachment> SubmissionAttachments => Set<SubmissionAttachment>();
    public DbSet<StudentJournalEntry> StudentJournalEntries => Set<StudentJournalEntry>();
    public DbSet<JournalQuestion> JournalQuestions => Set<JournalQuestion>();
    public DbSet<ConceptMark> ConceptMarks => Set<ConceptMark>();
    public DbSet<Quiz> Quizzes => Set<Quiz>();
    public DbSet<QuizQuestion> QuizQuestions => Set<QuizQuestion>();
    public DbSet<QuizAttempt> QuizAttempts => Set<QuizAttempt>();
    public DbSet<CourseForm> CourseForms => Set<CourseForm>();
    public DbSet<FormQuestion> FormQuestions => Set<FormQuestion>();
    public DbSet<FormResponse> FormResponses => Set<FormResponse>();
    public DbSet<FormAnswer> FormAnswers => Set<FormAnswer>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Existing SQLite rows store Guids as uppercase TEXT; comparisons are case-sensitive.
        var guidToUpperString = new ValueConverter<Guid, string>(
            g => g.ToString("D").ToUpperInvariant(),
            s => Guid.Parse(s));
        var nullableGuidToUpperString = new ValueConverter<Guid?, string?>(
            g => g.HasValue ? g.Value.ToString("D").ToUpperInvariant() : null,
            s => string.IsNullOrWhiteSpace(s) ? null : Guid.Parse(s));

        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(Guid))
                {
                    property.SetValueConverter(guidToUpperString);
                }
                else if (property.ClrType == typeof(Guid?))
                {
                    property.SetValueConverter(nullableGuidToUpperString);
                }
            }
        }

        builder.Entity<Tenant>(e =>
        {
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Name).HasMaxLength(160);
            e.Property(x => x.Slug).HasMaxLength(80);
        });

        builder.Entity<Course>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.Slug }).IsUnique();
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Slug).HasMaxLength(100);
            e.Property(x => x.TeacherUserId).HasMaxLength(450);
            e.HasIndex(x => x.TeacherUserId);
            e.HasOne(x => x.Teacher)
                .WithMany()
                .HasForeignKey(x => x.TeacherUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Lesson>(e =>
        {
            e.HasIndex(x => x.ModuleId);
            e.HasIndex(x => new { x.ClassGroupId, x.ModuleId, x.Slug }).IsUnique();
            e.HasIndex(x => new { x.ClassGroupId, x.SortOrder });
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Slug).HasMaxLength(100);
            e.HasOne(x => x.ClassGroup)
                .WithMany()
                .HasForeignKey(x => x.ClassGroupId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ContentDocument>(e =>
        {
            e.HasOne(x => x.Lesson)
                .WithOne(x => x.ContentDocument)
                .HasForeignKey<ContentDocument>(x => x.LessonId);
        });

        builder.Entity<Enrollment>(e =>
        {
            e.HasIndex(x => new { x.ClassGroupId, x.UserId }).IsUnique();
        });

        builder.Entity<ActivitySubmission>(e =>
        {
            e.HasIndex(x => new { x.ActivityId, x.UserId }).IsUnique();
            e.Property(x => x.GitHubUrl).HasMaxLength(500);
            e.Property(x => x.GitHubPrUrl).HasMaxLength(500);
            e.Property(x => x.DeliveryNote).HasMaxLength(500);
        });

        builder.Entity<SubmissionAttachment>(e =>
        {
            e.Property(x => x.OriginalFileName).HasMaxLength(260);
            e.Property(x => x.StoredFileName).HasMaxLength(80);
            e.Property(x => x.ContentType).HasMaxLength(120);
            e.HasOne(x => x.Submission)
                .WithMany(x => x.Attachments)
                .HasForeignKey(x => x.SubmissionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<StudentJournalEntry>(e =>
        {
            e.HasIndex(x => new { x.LessonId, x.UserId }).IsUnique();
        });

        builder.Entity<ConceptMark>(e =>
        {
            e.HasIndex(x => new { x.JournalEntryId, x.ConceptId }).IsUnique();
        });

        builder.Entity<Quiz>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(200);
        });

        builder.Entity<QuizQuestion>(e =>
        {
            e.Property(x => x.CorrectOption).HasMaxLength(1);
        });

        builder.Entity<QuizAttempt>(e =>
        {
            e.HasIndex(x => new { x.QuizId, x.UserId });
        });

        builder.Entity<CourseForm>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(2000);
            e.HasIndex(x => x.CourseId);
            e.HasOne(x => x.Course)
                .WithMany()
                .HasForeignKey(x => x.CourseId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<FormQuestion>(e =>
        {
            e.Property(x => x.Prompt).HasMaxLength(1000);
            e.Property(x => x.OptionsJson).HasMaxLength(4000);
            e.HasIndex(x => new { x.FormId, x.SortOrder });
            e.HasOne(x => x.Form)
                .WithMany(x => x.Questions)
                .HasForeignKey(x => x.FormId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<FormResponse>(e =>
        {
            e.HasIndex(x => new { x.FormId, x.UserId }).IsUnique();
            e.HasOne(x => x.Form)
                .WithMany(x => x.Responses)
                .HasForeignKey(x => x.FormId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<FormAnswer>(e =>
        {
            e.HasIndex(x => new { x.ResponseId, x.QuestionId }).IsUnique();
            e.Property(x => x.TextValue).HasMaxLength(4000);
            e.Property(x => x.SelectedOptionsJson).HasMaxLength(2000);
            e.HasOne(x => x.Response)
                .WithMany(x => x.Answers)
                .HasForeignKey(x => x.ResponseId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Question)
                .WithMany()
                .HasForeignKey(x => x.QuestionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ApplicationUser>(e =>
        {
            e.HasOne(x => x.Tenant)
                .WithMany(x => x.Users)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
