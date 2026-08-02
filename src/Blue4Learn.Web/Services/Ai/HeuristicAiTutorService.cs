using System.Text.RegularExpressions;

namespace Blue4Learn.Web.Services.Ai;

public partial class HeuristicAiTutorService : IAiTutorService
{
    public bool IsEnabled => true;

    public Task<IReadOnlyList<string>> SuggestConceptsAsync(
        string? markdown,
        string? objective,
        CancellationToken cancellationToken = default)
    {
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(objective))
        {
            foreach (var term in SplitWords(objective))
            {
                if (term.Length >= 4)
                {
                    candidates.Add(Capitalize(term));
                }
            }
        }

        var text = markdown ?? string.Empty;
        foreach (Match match in HeadingRegex().Matches(text))
        {
            var heading = Clean(match.Groups[1].Value);
            if (heading.Length is >= 2 and <= 60)
            {
                candidates.Add(heading);
            }
        }

        foreach (Match match in BoldRegex().Matches(text))
        {
            var bold = Clean(match.Groups[1].Value);
            if (bold.Length is >= 2 and <= 40 && !bold.Contains(' '))
            {
                candidates.Add(bold);
            }
            else if (bold.Length is >= 2 and <= 40)
            {
                candidates.Add(bold);
            }
        }

        foreach (Match match in CodeRegex().Matches(text))
        {
            var code = Clean(match.Groups[1].Value);
            if (code.Length is >= 2 and <= 30 && !code.Contains('\n'))
            {
                candidates.Add(code);
            }
        }

        var stop = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "exemplo", "dica", "introdução", "conclusão", "resumo", "atividade",
            "olá", "nova", "aula", "markdown", "o que", "como", "quando", "porque"
        };

        var result = candidates
            .Where(c => c.Length >= 2 && !stop.Contains(c))
            .OrderBy(c => c.Length)
            .ThenBy(c => c)
            .Take(8)
            .ToList();

        return Task.FromResult<IReadOnlyList<string>>(result);
    }

    public Task<IReadOnlyList<string>> SuggestGuidingQuestionsAsync(
        GuidingQuestionContext context,
        CancellationToken cancellationToken = default)
    {
        var questions = new List<string>();

        var weak = context.AllConcepts
            .Where(c => !context.UnderstoodConcepts.Contains(c, StringComparer.OrdinalIgnoreCase))
            .Take(3)
            .ToList();

        foreach (var concept in weak)
        {
            questions.Add($"Em suas palavras, o que é {concept} e onde você o usaria nesta aula?");
        }

        if (context.NeedsReview)
        {
            questions.Add("Você marcou que precisa revisar: qual trecho ainda ficou confuso e por quê?");
        }

        if (!context.UnderstoodObjective && !string.IsNullOrWhiteSpace(context.Objective))
        {
            questions.Add($"O objetivo da aula é “{Trim(context.Objective, 120)}”. O que você já consegue explicar sobre isso?");
        }

        foreach (var open in context.OpenQuestions.Take(2))
        {
            questions.Add($"Sobre a dúvida “{Trim(open, 80)}”: o que você já tentou para resolvê-la?");
        }

        if (questions.Count == 0)
        {
            questions.Add($"O que mudou na sua compreensão de “{context.LessonTitle}” depois desta prática?");
            questions.Add("Se fosse explicar esta aula a um colega em 2 minutos, por onde começaria?");
        }

        return Task.FromResult<IReadOnlyList<string>>(questions.Distinct().Take(4).ToList());
    }

    private static IEnumerable<string> SplitWords(string text) =>
        WordRegex().Matches(text).Select(m => m.Value);

    private static string Clean(string value) =>
        Regex.Replace(value, @"[#*_`]+", string.Empty).Trim().Trim(':', '.', ',');

    private static string Capitalize(string value) =>
        value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value[1..];

    private static string Trim(string value, int max) =>
        value.Length <= max ? value : value[..max].TrimEnd() + "…";

    [GeneratedRegex(@"^#{1,3}\s+(.+)$", RegexOptions.Multiline)]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"\*\*(.+?)\*\*")]
    private static partial Regex BoldRegex();

    [GeneratedRegex(@"`([^`\n]+)`")]
    private static partial Regex CodeRegex();

    [GeneratedRegex(@"[\p{L}][\p{L}\p{N}-]{3,}", RegexOptions.IgnoreCase)]
    private static partial Regex WordRegex();
}
