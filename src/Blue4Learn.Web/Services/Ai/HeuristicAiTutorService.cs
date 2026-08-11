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

    public Task<EvidenceAnalysisResult> AnalyzeEvidenceAsync(
        EvidenceAnalysisRequest request,
        CancellationToken cancellationToken = default)
    {
        var checklist = BuildHeuristicChecklist(request);
        var met = checklist.Count(c => c.Status == "met");
        var partial = checklist.Count(c => c.Status == "partial");
        var missing = checklist.Count(c => c.Status == "missing");

        var commitLine = request.Commits.Count > 0
            ? $"Repositório com {request.Commits.Count} commit(s); {request.Commits.Sum(c => c.Files.Count)} ficheiro(s) alterado(s) nos diffs; último em {request.Commits[0].Date}."
            : string.IsNullOrWhiteSpace(request.GitHubUrl)
                ? "Sem link de GitHub na entrega."
                : "Link de GitHub informado, mas não foi possível listar commits.";

        var summary =
            $"Entrega de “{request.ActivityTitle}”: {met} atendimento(s) completo(s), {partial} parcial(is), {missing} em falta. {commitLine}";

        var gaps = checklist.Where(c => c.Status != "met").Select(c => c.Item).Take(3).ToList();
        var feedback = gaps.Count == 0
            ? "Bom trabalho — os elementos principais pedidos na atividade aparecem na entrega. Revise os detalhes e confirme se a evidência cobre bem o objetivo da aula."
            : $"Obrigada pela entrega. Para fechar a atividade, priorize: {string.Join("; ", gaps)}. " +
              "Quando atualizar, descreva brevemente o que mudou e (se houver) o commit correspondente.";

        return Task.FromResult(new EvidenceAnalysisResult
        {
            Summary = summary,
            Checklist = checklist,
            FeedbackDraft = feedback,
            UsedLlm = false
        });
    }

    private static List<EvidenceChecklistItem> BuildHeuristicChecklist(EvidenceAnalysisRequest request)
    {
        var items = new List<EvidenceChecklistItem>
        {
            FieldItem("Descrição do problema", request.ProblemDescription),
            FieldItem("Descrição da solução", request.SolutionDescription),
            FieldItem("Evidência textual", request.TextResponse),
            new EvidenceChecklistItem
            {
                Item = "Link do repositório GitHub",
                Status = string.IsNullOrWhiteSpace(request.GitHubUrl) ? "missing" : "met",
                EvidenceNote = string.IsNullOrWhiteSpace(request.GitHubUrl)
                    ? "Não informado"
                    : request.GitHubUrl!
            },
            new EvidenceChecklistItem
            {
                Item = "Anexos de evidência",
                Status = request.AttachmentNames.Count > 0 ? "met" : "missing",
                EvidenceNote = request.AttachmentNames.Count > 0
                    ? string.Join(", ", request.AttachmentNames)
                    : "Nenhum anexo"
            },
            new EvidenceChecklistItem
            {
                Item = "Histórico de commits (datas e alterações)",
                Status = request.Commits.Count == 0
                    ? (string.IsNullOrWhiteSpace(request.GitHubUrl) ? "missing" : "partial")
                    : request.Commits.Any(c => c.Files.Count > 0) ? "met" : "partial",
                EvidenceNote = request.Commits.Count > 0
                    ? string.Join(" · ", request.Commits.Take(3).Select(c =>
                    {
                        var files = c.Files.Count == 0
                            ? "sem diff"
                            : string.Join(", ", c.Files.Take(3).Select(f => f.Filename));
                        return $"{c.Date}: {c.Message} ({files})";
                    }))
                    : "Sem commits listados"
            }
        };

        // Itens derivados de bullets / linhas do enunciado.
        foreach (var req in ExtractPromptRequirements(request.Prompt).Take(4))
        {
            var haystack = string.Join('\n',
                request.ProblemDescription,
                request.SolutionDescription,
                request.TextResponse,
                request.GitHubUrl,
                string.Join(' ', request.AttachmentNames),
                string.Join(' ', request.Commits.SelectMany(c => c.Files.Select(f => f.Filename + " " + (f.PatchExcerpt ?? "")))));
            var hit = req.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(w => w.Length >= 4)
                .Any(w => haystack.Contains(w, StringComparison.OrdinalIgnoreCase));
            items.Add(new EvidenceChecklistItem
            {
                Item = Trim(req, 100),
                Status = hit ? "partial" : "missing",
                EvidenceNote = hit ? "Possível cobertura no texto/evidência" : "Não encontrado claramente na entrega"
            });
        }

        return items;
    }

    private static EvidenceChecklistItem FieldItem(string label, string value)
    {
        var has = !string.IsNullOrWhiteSpace(value);
        return new EvidenceChecklistItem
        {
            Item = label,
            Status = has ? (value.Trim().Length < 40 ? "partial" : "met") : "missing",
            EvidenceNote = has ? Trim(value, 120) : "Em falta"
        };
    }

    private static IEnumerable<string> ExtractPromptRequirements(string? prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            yield break;
        }

        foreach (Match m in BulletRegex().Matches(prompt))
        {
            var text = Clean(m.Groups[1].Value);
            if (text.Length >= 8)
            {
                yield return text;
            }
        }
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

    [GeneratedRegex(@"^\s*[-*•]\s+(.+)$", RegexOptions.Multiline)]
    private static partial Regex BulletRegex();
}
