namespace Blue4Learn.Web.Services;

public interface IFileStorageService
{
    IReadOnlyCollection<string> AllowedExtensions { get; }
    long MaxBytes { get; }
    Task<(bool Ok, string? Error, SubmissionFileResult? File)> SaveSubmissionAttachmentAsync(
        Guid tenantId,
        Guid submissionId,
        IFormFile file,
        CancellationToken cancellationToken = default);
    string GetPhysicalPath(Guid tenantId, Guid submissionId, string storedFileName);
    bool IsAllowed(IFormFile file);
}

public record SubmissionFileResult(
    string OriginalFileName,
    string StoredFileName,
    string ContentType,
    long SizeBytes);

public class FileStorageService : IFileStorageService
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".png", ".jpg", ".jpeg", ".gif", ".webp", ".txt", ".md", ".zip"
    };

    private readonly IWebHostEnvironment _env;

    public FileStorageService(IWebHostEnvironment env)
    {
        _env = env;
    }

    public IReadOnlyCollection<string> AllowedExtensions => Extensions;
    public long MaxBytes => 5 * 1024 * 1024;

    public bool IsAllowed(IFormFile file)
    {
        if (file.Length <= 0 || file.Length > MaxBytes)
        {
            return false;
        }

        var ext = Path.GetExtension(file.FileName);
        return !string.IsNullOrWhiteSpace(ext) && Extensions.Contains(ext);
    }

    public async Task<(bool Ok, string? Error, SubmissionFileResult? File)> SaveSubmissionAttachmentAsync(
        Guid tenantId,
        Guid submissionId,
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        if (file.Length <= 0)
        {
            return (false, "Arquivo vazio.", null);
        }

        if (file.Length > MaxBytes)
        {
            return (false, "Arquivo acima de 5 MB.", null);
        }

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext) || !Extensions.Contains(ext))
        {
            return (false, $"Extensão não permitida. Use: {string.Join(", ", Extensions)}", null);
        }

        var safeOriginal = Path.GetFileName(file.FileName);
        var stored = $"{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
        var dir = GetDirectory(tenantId, submissionId);
        Directory.CreateDirectory(dir);

        var path = Path.Combine(dir, stored);
        await using (var stream = File.Create(path))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        return (true, null, new SubmissionFileResult(
            safeOriginal,
            stored,
            string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            file.Length));
    }

    public string GetPhysicalPath(Guid tenantId, Guid submissionId, string storedFileName)
    {
        var safeName = Path.GetFileName(storedFileName);
        return Path.Combine(GetDirectory(tenantId, submissionId), safeName);
    }

    private string GetDirectory(Guid tenantId, Guid submissionId)
        => Path.Combine(_env.ContentRootPath, "App_Data", "uploads", tenantId.ToString("N"), submissionId.ToString("N"));
}
