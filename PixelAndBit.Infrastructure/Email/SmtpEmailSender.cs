using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;
using PixelAndBit.Application.Interfaces;

namespace PixelAndBit.Infrastructure.Email;

public class SmtpEmailOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;

    public string Username { get; set; } = "";
    public string Password { get; set; } = "";

    public string FromEmail { get; set; } = "";
    public string FromName { get; set; } = "Pixel & Bit";
}

public class SmtpEmailSender : IEmailSender
{
    private readonly SmtpEmailOptions _opt;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<SmtpEmailOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _opt = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string subject, string htmlBody)
    {
        if (string.IsNullOrWhiteSpace(_opt.Host) || string.IsNullOrWhiteSpace(_opt.FromEmail))
        {
            _logger.LogWarning("SMTP send skipped — Host or FromEmail is not configured.");
            return;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_opt.FromName, _opt.FromEmail));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;
            message.Body = new TextPart(TextFormat.Html) { Text = htmlBody };

            Console.WriteLine("=== SMTP DEBUG START ===");
            Console.WriteLine($"Host: {_opt.Host}");
            Console.WriteLine($"Port: {_opt.Port}");
            Console.WriteLine($"SSL: {_opt.EnableSsl}");
            Console.WriteLine($"Username: {_opt.Username}");
            Console.WriteLine($"From: {_opt.FromEmail}");

            using var client = new MailKit.Net.Smtp.SmtpClient();
            client.Timeout = 30000;

            await client.ConnectAsync(_opt.Host, _opt.Port, true);
            await client.AuthenticateAsync(_opt.Username, _opt.Password);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            Console.WriteLine("=== EMAIL SENT SUCCESSFULLY ===");
        }
        catch (Exception ex)
        {
            Console.WriteLine("=== SMTP ERROR START ===");
            Console.WriteLine(ex.ToString());
            Console.WriteLine("=== SMTP ERROR END ===");
            throw;
        }
    }
}
