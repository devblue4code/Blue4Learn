using Blue4Learn.Web.Domain;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

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

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

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
            e.HasIndex(x => new { x.ModuleId, x.Slug }).IsUnique();
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Slug).HasMaxLength(100);
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

        builder.Entity<ApplicationUser>(e =>
        {
            e.HasOne(x => x.Tenant)
                .WithMany(x => x.Users)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
