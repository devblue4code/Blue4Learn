using System.Text;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace Blue4Learn.Web.Services;

/// <summary>
/// Em desenvolvimento, grava e-mails em App_Data/email-outbox.txt e no log —
/// suficiente para testar "Esqueci minha senha" sem SMTP.
/// </summary>
public class DevelopmentEmailSender : IEmailSender
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<DevelopmentEmailSender> _logger;
    private readonly object _sync = new();

    public DevelopmentEmailSender(IWebHostEnvironment env, ILogger<DevelopmentEmailSender> logger)
    {
        _env = env;
        _logger = logger;
    }

    public Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        var dir = Path.Combine(_env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "email-outbox.txt");

        var entry = new StringBuilder()
            .AppendLine("==========")
            .AppendLine($"UTC: {DateTime.UtcNow:O}")
            .AppendLine($"To: {email}")
            .AppendLine($"Subject: {subject}")
            .AppendLine(htmlMessage)
            .AppendLine()
            .ToString();

        lock (_sync)
        {
            File.AppendAllText(path, entry);
        }

        _logger.LogInformation("E-mail de desenvolvimento para {Email}: {Subject}. Outbox: {Path}", email, subject, path);
        return Task.CompletedTask;
    }
}
