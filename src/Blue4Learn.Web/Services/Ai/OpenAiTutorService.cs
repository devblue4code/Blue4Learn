using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Blue4Learn.Web.Services.Ai;

public class OpenAiTutorService : IAiTutorService
{
    private readonly AiTutorOptions _options;
    private readonly HeuristicAiTutorService _fallback;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OpenAiTutorService> _logger;

    public OpenAiTutorService(
        IOptions<AiTutorOptions> options,
        HeuristicAiTutorService fallback,
        IHttpClientFactory httpClientFactory,
        ILogger<OpenAiTutorService> logger)
    {
        _options = options.Value;
        _fallback = fallback;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public bool IsEnabled => _options.Enabled;

    private bool HasApiKey => !string.IsNullOrWhiteSpace(_options.ApiKey);

    public async Task<IReadOnlyList<string>> SuggestConceptsAsync(
        string? markdown,
        string? objective,
        CancellationToken cancellationToken = default)
    {
        if (!HasApiKey)
        {
            return await _fallback.SuggestConceptsAsync(markdown, objective, cancellationToken);
        }

        try
        {
            var md = markdown ?? string.Empty;
            if (md.Length > 4000)
            {
                md = md[..4000];
            }

            var prompt = $$"""
                Extraia de 3 a 8 conceitos técnicos curtos (1 a 4 palavras) desta aula.
                Responda APENAS com JSON: {"concepts":["..."]}
                Objetivo: {{objective}}
                Markdown:
                {{md}}
                """;

            var concepts = await CompleteJsonListAsync(prompt, "concepts", cancellationToken);
            return concepts.Count > 0
                ? concepts
                : await _fallback.SuggestConceptsAsync(markdown, objective, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao sugerir conceitos via LLM; usando heurística.");
            return await _fallback.SuggestConceptsAsync(markdown, objective, cancellationToken);
        }
    }

    public async Task<IReadOnlyList<string>> SuggestGuidingQuestionsAsync(
        GuidingQuestionContext context,
        CancellationToken cancellationToken = default)
    {
        if (!HasApiKey)
        {
            return await _fallback.SuggestGuidingQuestionsAsync(context, cancellationToken);
        }

        try
        {
            var concepts = string.Join(", ", context.AllConcepts);
            var understood = string.Join(", ", context.UnderstoodConcepts);
            var open = string.Join(" | ", context.OpenQuestions);
            var prompt = $$"""
                Você é uma tutora pedagógica responsável. Gere 2 a 4 perguntas orientadoras
                (não dê a resposta pronta) para o estudante refletir.
                Responda APENAS com JSON: {"questions":["..."]}
                Aula: {{context.LessonTitle}}
                Objetivo: {{context.Objective}}
                Conceitos: {{concepts}}
                Compreendidos: {{understood}}
                Precisa revisar: {{context.NeedsReview}}
                Dúvidas abertas: {{open}}
                """;

            var questions = await CompleteJsonListAsync(prompt, "questions", cancellationToken);
            return questions.Count > 0
                ? questions
                : await _fallback.SuggestGuidingQuestionsAsync(context, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao gerar perguntas via LLM; usando heurística.");
            return await _fallback.SuggestGuidingQuestionsAsync(context, cancellationToken);
        }
    }

    public async Task<EvidenceAnalysisResult> AnalyzeEvidenceAsync(
        EvidenceAnalysisRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!HasApiKey)
        {
            return await _fallback.AnalyzeEvidenceAsync(request, cancellationToken);
        }

        try
        {
            var prompt = BuildEvidencePrompt(request);
            var client = _httpClientFactory.CreateClient("AiTutor");
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _options.ApiKey);

            var payload = new
            {
                model = _options.Model,
                temperature = 0.35,
                response_format = new { type = "json_object" },
                messages = new object[]
                {
                    new
                    {
                        role = "system",
                        content = "Você é uma professora de programação. Avalie entregas de alunos em português do Brasil. Responda somente JSON válido."
                    },
                    new { role = "user", content = prompt }
                }
            };

            using var content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            using var response = await client.PostAsync("chat/completions", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var message = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrWhiteSpace(message))
            {
                return await _fallback.AnalyzeEvidenceAsync(request, cancellationToken);
            }

            var parsed = ParseEvidenceResult(message);
            if (parsed.Checklist.Count == 0 && string.IsNullOrWhiteSpace(parsed.FeedbackDraft))
            {
                return await _fallback.AnalyzeEvidenceAsync(request, cancellationToken);
            }

            parsed.UsedLlm = true;
            return parsed;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao analisar evidência via LLM; usando heurística.");
            return await _fallback.AnalyzeEvidenceAsync(request, cancellationToken);
        }
    }

    private static string BuildEvidencePrompt(EvidenceAnalysisRequest request)
    {
        var attachments = request.AttachmentNames.Count == 0
            ? "(nenhum)"
            : string.Join(", ", request.AttachmentNames);

        var commits = request.Commits.Count == 0
            ? "(nenhum commit listado)"
            : string.Join("\n\n", request.Commits.Select(FormatCommitForPrompt));

        if (commits.Length > 12000)
        {
            commits = commits[..12000] + "\n…(diffs truncados)";
        }

        var activityPrompt = request.Prompt ?? "";
        if (activityPrompt.Length > 3500)
        {
            activityPrompt = activityPrompt[..3500];
        }

        var problem = Trim(request.ProblemDescription, 1500);
        var solution = Trim(request.SolutionDescription, 1500);
        var text = Trim(request.TextResponse, 1500);
        var github = request.GitHubUrl ?? "(não informado)";

        return $$"""
            Compare o que foi solicitado no enunciado com o que o estudante entregou.
            Use também os DIFFS dos commits (ficheiros e trechos +/-) para julgar se a entrega cobre o pedido.
            Produza:
            1) summary: parágrafo curto (relatório da entrega, cite ficheiros/commits relevantes)
            2) checklist: 4 a 10 itens do que o enunciado pede; status = met | partial | missing; evidenceNote breve (mencione ficheiro/commit quando possível)
            3) feedbackDraft: texto de feedback orientador em PT-BR (2 a 5 frases), pronto para a professora editar

            Responda APENAS com JSON:
            {"summary":"...","checklist":[{"item":"...","status":"met|partial|missing","evidenceNote":"..."}],"feedbackDraft":"..."}

            Atividade: {{request.ActivityTitle}}
            Enunciado:
            {{activityPrompt}}

            Problema do aluno:
            {{problem}}

            Solução do aluno:
            {{solution}}

            Evidência textual:
            {{text}}

            GitHub: {{github}}
            Anexos: {{attachments}}

            Commits com alterações (diffs):
            {{commits}}
            """;
    }

    private static string FormatCommitForPrompt(EvidenceCommitInfo c)
    {
        var header = $"- {c.Date} | {c.Sha} | {c.Author}: {c.Message}";
        if (c.Files.Count == 0)
        {
            return header + "\n  (sem diff disponível)";
        }

        var files = string.Join("\n", c.Files.Select(f =>
        {
            var meta = $"  * {f.Filename} [{f.Status}] +{f.Additions}/-{f.Deletions}";
            if (string.IsNullOrWhiteSpace(f.PatchExcerpt))
            {
                return meta;
            }

            return meta + "\n```\n" + f.PatchExcerpt + "\n```";
        }));

        return header + "\n" + files;
    }

    private static EvidenceAnalysisResult ParseEvidenceResult(string message)
    {
        using var inner = JsonDocument.Parse(message);
        var root = inner.RootElement;
        var result = new EvidenceAnalysisResult
        {
            Summary = root.TryGetProperty("summary", out var s) ? s.GetString()?.Trim() ?? "" : "",
            FeedbackDraft = root.TryGetProperty("feedbackDraft", out var f) ? f.GetString()?.Trim() ?? "" : ""
        };

        if (root.TryGetProperty("checklist", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in arr.EnumerateArray())
            {
                var status = el.TryGetProperty("status", out var st) ? st.GetString()?.Trim().ToLowerInvariant() : "missing";
                if (status is not ("met" or "partial" or "missing"))
                {
                    status = "missing";
                }

                result.Checklist.Add(new EvidenceChecklistItem
                {
                    Item = el.TryGetProperty("item", out var item) ? item.GetString()?.Trim() ?? "" : "",
                    Status = status!,
                    EvidenceNote = el.TryGetProperty("evidenceNote", out var note)
                        ? note.GetString()?.Trim() ?? ""
                        : ""
                });
            }
        }

        result.Checklist = result.Checklist.Where(c => !string.IsNullOrWhiteSpace(c.Item)).Take(12).ToList();
        return result;
    }

    private static string Trim(string? value, int max)
    {
        var v = value ?? "";
        return v.Length <= max ? v : v[..max] + "…";
    }

    private async Task<IReadOnlyList<string>> CompleteJsonListAsync(
        string userPrompt,
        string arrayProperty,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("AiTutor");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        var payload = new
        {
            model = _options.Model,
            temperature = 0.3,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = "Responda somente JSON válido." },
                new { role = "user", content = userPrompt }
            }
        };

        using var content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        using var response = await client.PostAsync("chat/completions", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var message = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        if (string.IsNullOrWhiteSpace(message))
        {
            return [];
        }

        using var inner = JsonDocument.Parse(message);
        if (!inner.RootElement.TryGetProperty(arrayProperty, out var arr) ||
            arr.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return arr.EnumerateArray()
            .Select(x => x.GetString()?.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();
    }
}
