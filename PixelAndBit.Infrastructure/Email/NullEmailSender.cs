using Microsoft.Extensions.Logging;
using PixelAndBit.Application.Interfaces;

namespace PixelAndBit.Infrastructure.Email;

public sealed class NullEmailSender : IEmailSender
{
    private readonly ILogger<NullEmailSender> _logger;

    public NullEmailSender(ILogger<NullEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string toEmail, string subject, string htmlBody)
    {
        _logger.LogDebug("Email not sent (SMTP not configured). To={To} Subject={Subject}", toEmail, subject);
        return Task.CompletedTask;
    }
}
