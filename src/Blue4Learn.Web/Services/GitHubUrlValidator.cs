namespace Blue4Learn.Web.Services;

public static class GitHubUrlValidator
{
    public static bool TryValidateRepositoryUrl(string? url, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(url))
        {
            error = "Informe a URL do repositório no GitHub.";
            return false;
        }

        if (!TryParseGitHubUri(url.Trim(), out var uri))
        {
            error = "Use uma URL válida do GitHub (ex.: https://github.com/usuario/repositorio).";
            return false;
        }

        var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
        {
            error = "A URL do repositório deve incluir usuário/organização e nome do repositório.";
            return false;
        }

        return true;
    }

    public static bool TryValidatePullRequestUrl(string? url, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(url))
        {
            return true;
        }

        if (!TryParseGitHubUri(url.Trim(), out var uri))
        {
            error = "Use uma URL válida do GitHub para o Pull Request.";
            return false;
        }

        if (!uri.AbsolutePath.Contains("/pull/", StringComparison.OrdinalIgnoreCase))
        {
            error = "A URL do Pull Request deve apontar para github.com/.../pull/...";
            return false;
        }

        return true;
    }

    private static bool TryParseGitHubUri(string url, out Uri uri)
    {
        uri = null!;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed))
        {
            return false;
        }

        if (!string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(parsed.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var host = parsed.Host;
        if (!host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
            && !host.Equals("www.github.com", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        uri = parsed;
        return true;
    }
}
