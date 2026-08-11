using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace Blue4Learn.Web.Services;

public class GitHubOptions
{
    public const string SectionName = "GitHub";

    /// <summary>Token opcional (repos privados / rate limit).</summary>
    public string? Token { get; set; }
}

public record GitHubFileChange(
    string Filename,
    string Status,
    int Additions,
    int Deletions,
    string? PatchExcerpt);

public record GitHubCommitInfo(
    string Sha,
    string FullSha,
    string Message,
    string Author,
    DateTimeOffset Date,
    string Url,
    IReadOnlyList<GitHubFileChange> Files);

public interface IGitHubCommitService
{
    bool TryParseRepo(string? url, out string owner, out string repo, out string? branch);
    Task<IReadOnlyList<GitHubCommitInfo>> GetRecentCommitsAsync(
        string? githubUrl,
        int take = 10,
        CancellationToken cancellationToken = default);
}

public partial class GitHubCommitService : IGitHubCommitService
{
    private const int MaxCommitsWithDiff = 5;
    private const int MaxFilesPerCommit = 12;
    private const int MaxPatchCharsPerFile = 1200;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GitHubOptions _options;
    private readonly ILogger<GitHubCommitService> _logger;

    public GitHubCommitService(
        IHttpClientFactory httpClientFactory,
        IOptions<GitHubOptions> options,
        ILogger<GitHubCommitService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public bool TryParseRepo(string? url, out string owner, out string repo, out string? branch)
    {
        owner = string.Empty;
        repo = string.Empty;
        branch = null;
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        var match = RepoUrlRegex().Match(url.Trim());
        if (!match.Success)
        {
            return false;
        }

        owner = match.Groups["owner"].Value;
        repo = match.Groups["repo"].Value.TrimEnd('/');
        if (repo.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            repo = repo[..^4];
        }

        if (match.Groups["branch"].Success && !string.IsNullOrWhiteSpace(match.Groups["branch"].Value))
        {
            branch = Uri.UnescapeDataString(match.Groups["branch"].Value.Trim('/'));
        }

        return owner.Length > 0 && repo.Length > 0;
    }

    public async Task<IReadOnlyList<GitHubCommitInfo>> GetRecentCommitsAsync(
        string? githubUrl,
        int take = 10,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseRepo(githubUrl, out var owner, out var repo, out var branch))
        {
            return [];
        }

        take = Math.Clamp(take, 1, 30);

        try
        {
            var client = CreateClient();
            var listUrl = $"repos/{owner}/{repo}/commits?per_page={take}";
            if (!string.IsNullOrWhiteSpace(branch))
            {
                listUrl += $"&sha={Uri.EscapeDataString(branch)}";
            }

            using var response = await client.GetAsync(listUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "GitHub commits unavailable for {Owner}/{Repo}: {Status}",
                    owner, repo, (int)response.StatusCode);
                return [];
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var summaries = new List<(string FullSha, string ShortSha, string Message, string Author, DateTimeOffset Date, string Url)>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var fullSha = item.TryGetProperty("sha", out var shaEl) ? shaEl.GetString() ?? "" : "";
                if (fullSha.Length == 0) continue;

                var htmlUrl = item.TryGetProperty("html_url", out var urlEl) ? urlEl.GetString() ?? "" : "";
                var commit = item.GetProperty("commit");
                var message = commit.TryGetProperty("message", out var msgEl)
                    ? (msgEl.GetString() ?? "").Split('\n')[0].Trim()
                    : "";
                var authorName = "—";
                var date = DateTimeOffset.MinValue;
                if (commit.TryGetProperty("author", out var author) && author.ValueKind == JsonValueKind.Object)
                {
                    authorName = author.TryGetProperty("name", out var n) ? n.GetString() ?? "—" : "—";
                    if (author.TryGetProperty("date", out var d) &&
                        DateTimeOffset.TryParse(d.GetString(), out var parsed))
                    {
                        date = parsed;
                    }
                }

                summaries.Add((
                    fullSha,
                    fullSha[..Math.Min(7, fullSha.Length)],
                    message,
                    authorName,
                    date,
                    htmlUrl));
            }

            var list = new List<GitHubCommitInfo>();
            for (var i = 0; i < summaries.Count; i++)
            {
                var s = summaries[i];
                IReadOnlyList<GitHubFileChange> files = [];
                if (i < MaxCommitsWithDiff)
                {
                    files = await GetCommitFilesAsync(client, owner, repo, s.FullSha, cancellationToken);
                }

                list.Add(new GitHubCommitInfo(
                    Sha: s.ShortSha,
                    FullSha: s.FullSha,
                    Message: s.Message,
                    Author: s.Author,
                    Date: s.Date,
                    Url: s.Url,
                    Files: files));
            }

            return list;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao obter commits de {Url}", githubUrl);
            return [];
        }
    }

    private async Task<IReadOnlyList<GitHubFileChange>> GetCommitFilesAsync(
        HttpClient client,
        string owner,
        string repo,
        string fullSha,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.GetAsync(
                $"repos/{owner}/{repo}/commits/{fullSha}",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "GitHub commit detail unavailable {Owner}/{Repo}@{Sha}: {Status}",
                    owner, repo, fullSha[..Math.Min(7, fullSha.Length)], (int)response.StatusCode);
                return [];
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (!doc.RootElement.TryGetProperty("files", out var filesEl) ||
                filesEl.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var files = new List<GitHubFileChange>();
            foreach (var file in filesEl.EnumerateArray().Take(MaxFilesPerCommit))
            {
                var filename = file.TryGetProperty("filename", out var fn) ? fn.GetString() ?? "" : "";
                if (filename.Length == 0) continue;

                var status = file.TryGetProperty("status", out var st) ? st.GetString() ?? "modified" : "modified";
                var additions = file.TryGetProperty("additions", out var add) && add.TryGetInt32(out var a) ? a : 0;
                var deletions = file.TryGetProperty("deletions", out var del) && del.TryGetInt32(out var d) ? d : 0;
                string? patch = null;
                if (file.TryGetProperty("patch", out var patchEl))
                {
                    patch = TruncatePatch(patchEl.GetString());
                }

                files.Add(new GitHubFileChange(filename, status, additions, deletions, patch));
            }

            return files;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao obter diff do commit {Sha}", fullSha);
            return [];
        }
    }

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient("GitHub");
        if (!string.IsNullOrWhiteSpace(_options.Token))
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _options.Token);
        }

        return client;
    }

    private static string? TruncatePatch(string? patch)
    {
        if (string.IsNullOrWhiteSpace(patch))
        {
            return null;
        }

        // Prefer lines that show actual changes.
        var sb = new StringBuilder();
        foreach (var line in patch.Split('\n'))
        {
            if (sb.Length >= MaxPatchCharsPerFile)
            {
                sb.AppendLine("…");
                break;
            }

            if (line.StartsWith("diff ", StringComparison.Ordinal) ||
                line.StartsWith("index ", StringComparison.Ordinal) ||
                line.StartsWith("--- ", StringComparison.Ordinal) ||
                line.StartsWith("+++ ", StringComparison.Ordinal))
            {
                continue;
            }

            sb.AppendLine(line);
        }

        var text = sb.ToString().Trim();
        if (text.Length == 0)
        {
            return patch.Length <= MaxPatchCharsPerFile
                ? patch
                : patch[..MaxPatchCharsPerFile] + "…";
        }

        return text.Length <= MaxPatchCharsPerFile
            ? text
            : text[..MaxPatchCharsPerFile] + "…";
    }

    // Supports:
    // https://github.com/owner/repo
    // https://github.com/owner/repo.git
    // https://github.com/owner/repo/tree/branch
    // https://github.com/owner/repo/blob/branch/path
    [GeneratedRegex(
        @"^(?:https?://)?(?:www\.)?github\.com/(?<owner>[A-Za-z0-9_.-]+)/(?<repo>[A-Za-z0-9_.-]+)(?:/(?:tree|blob)/(?<branch>[^/?#]+))?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RepoUrlRegex();
}
