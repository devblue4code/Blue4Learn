using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Blue4Learn.Web.Services;

public static partial class SlugHelper
{
    public static string FromTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return "aula";
        }

        var normalized = title.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);

        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(category == UnicodeCategory.SpaceSeparator || ch is '_' or '/' ? '-' : ch);
            }
        }

        var slug = sb.ToString().Normalize(NormalizationForm.FormC);
        slug = InvalidSlugChars().Replace(slug, string.Empty);
        slug = MultiHyphen().Replace(slug, "-").Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "aula" : slug;
    }

    [GeneratedRegex(@"[^a-z0-9\-]")]
    private static partial Regex InvalidSlugChars();

    [GeneratedRegex(@"-{2,}")]
    private static partial Regex MultiHyphen();
}
