using Ganss.Xss;
using Markdig;

namespace Blue4Learn.Web.Services;

public interface IMarkdownService
{
    string ToSafeHtml(string? markdown);
}

public class MarkdownService : IMarkdownService
{
    private readonly MarkdownPipeline _pipeline;
    private readonly HtmlSanitizer _sanitizer;

    public MarkdownService()
    {
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseAutoLinks()
            .UseAutoIdentifiers()
            .Build();

        _sanitizer = new HtmlSanitizer();
        _sanitizer.AllowedAttributes.Add("class");
        _sanitizer.AllowedAttributes.Add("id");
        _sanitizer.AllowedTags.Add("video");
        _sanitizer.AllowedTags.Add("source");
        _sanitizer.AllowedAttributes.Add("controls");
        _sanitizer.AllowedAttributes.Add("src");
        _sanitizer.AllowedAttributes.Add("type");
    }

    public string ToSafeHtml(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }

        var html = Markdown.ToHtml(markdown, _pipeline);
        return _sanitizer.Sanitize(html);
    }
}
