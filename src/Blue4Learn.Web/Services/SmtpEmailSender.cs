using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;

namespace Blue4Learn.Web.Services;

public class SmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        if (!_options.IsConfigured)
        {
            throw new InvalidOperationException("SMTP não configurado (Smtp:Host / UserName / Password).");
        }

        var fromAddress = string.IsNullOrWhiteSpace(_options.FromEmail)
            ? _options.UserName
            : _options.FromEmail;

        using var message = new MailMessage
        {
            From = new MailAddress(fromAddress, _options.FromName),
            Subject = subject,
            Body = htmlMessage,
            IsBodyHtml = true
        };
        message.To.Add(email);

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(_options.UserName, _options.Password)
        };

        try
        {
            await client.SendMailAsync(message);
            _logger.LogInformation("E-mail enviado para {Email}: {Subject}", email, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao enviar e-mail para {Email}: {Subject}", email, subject);
            throw;
        }
    }
}
