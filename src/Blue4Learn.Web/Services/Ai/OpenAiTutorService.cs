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
