using System.Net.Mail;
using FieldOps.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace FieldOps.Infrastructure.Email;

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    /// <summary>Leave empty to log emails instead of sending them.</summary>
    public string? Host { get; set; }
    public int Port { get; set; } = 25;
    public string From { get; set; } = "FieldOps <no-reply@fieldops.local>";
}

/// <summary>Plain SMTP; in development this is Mailpit (docker-compose), whose inbox is at http://localhost:8025.</summary>
public sealed class SmtpEmailSender(SmtpOptions options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(options.Host))
        {
            logger.LogInformation("Email to {To} not sent (no SMTP host): {Subject}", message.To, message.Subject);
            return;
        }

        using var client = new SmtpClient(options.Host, options.Port);
        using var mail = new MailMessage(new MailAddress(options.From), new MailAddress(message.To))
        {
            Subject = message.Subject,
            Body = message.Body,
        };
        await client.SendMailAsync(mail, ct);
    }
}
