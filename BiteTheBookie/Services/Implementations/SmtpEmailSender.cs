using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;
using MimeKit;

namespace BiteTheBookie.Services.Implementations
{
    /// <summary>
    /// Sends transactional email over SMTP. In Development (or when SMTP is not
    /// configured) it logs the message instead of sending, so account confirmation
    /// works without live credentials.
    /// </summary>
    public class SmtpEmailSender : IEmailSender
    {
        private readonly EmailSettings _settings;
        private readonly ILogger<SmtpEmailSender> _logger;

        public SmtpEmailSender(IOptions<EmailSettings> settings, ILogger<SmtpEmailSender> logger)
        {
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            // No-op / dev mode: log the email (including any links) instead of sending.
            if (_settings.UseDevNoOp || string.IsNullOrWhiteSpace(_settings.SmtpHost))
            {
                _logger.LogInformation(
                    "[DEV EMAIL] To: {Email} | Subject: {Subject}\n{Body}",
                    email, subject, htmlMessage);
                return;
            }

            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
                message.To.Add(MailboxAddress.Parse(email));
                message.Subject = subject;
                message.Body = new BodyBuilder { HtmlBody = htmlMessage }.ToMessageBody();

                // Port 465 uses implicit SSL; port 587 (and others) use STARTTLS.
                var socketOptions = _settings.SmtpPort == 465
                    ? SecureSocketOptions.SslOnConnect
                    : (_settings.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None);

                using var client = new SmtpClient();
                await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, socketOptions);
                await client.AuthenticateAsync(_settings.UserName, _settings.Password);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);
                _logger.LogInformation("Sent email to {Email} with subject '{Subject}'.", email, subject);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {Email}.", email);
                throw;
            }
        }
    }
}
