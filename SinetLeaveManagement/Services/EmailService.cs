using MailKit.Security;
using MimeKit;

namespace SinetLeaveManagement.Services
{
    public class EmailService : IEmailService
    {
        private readonly string _smtpServer; private readonly int _smtpPort; private readonly string _smtpUsername; private readonly string _smtpPassword; private readonly string _fromEmail; private readonly ILogger _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _smtpServer = configuration["Smtp:Server"] ?? throw new ArgumentNullException("Smtp:Server is not configured.");
            _smtpPort = int.TryParse(configuration["Smtp:Port"], out int port) ? port : throw new ArgumentNullException("Smtp:Port is not configured or invalid.");
            _smtpUsername = configuration["Smtp:Username"] ?? throw new ArgumentNullException("Smtp:Username is not configured.");
            _smtpPassword = configuration["Smtp:Password"] ?? throw new ArgumentNullException("Smtp:Password is not configured.");
            _fromEmail = configuration["Smtp:FromEmail"] ?? throw new ArgumentNullException("Smtp:FromEmail is not configured.");
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
                throw new ArgumentException("To email address is required.", nameof(toEmail));

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("Sinet Leave Management", _fromEmail));
            message.To.Add(new MailboxAddress("", toEmail));
            message.Subject = subject;
            message.Body = new TextPart("plain") { Text = body };

            try
            {
                using var client = new MailKit.Net.Smtp.SmtpClient(); // Explicitly qualified
                _logger.LogInformation("Connecting to SMTP server: {SmtpServer} on port {SmtpPort}", _smtpServer, _smtpPort);
                await client.ConnectAsync(_smtpServer, _smtpPort, SecureSocketOptions.StartTls);
                _logger.LogInformation("Authenticating with SMTP server...");
                await client.AuthenticateAsync(_smtpUsername, _smtpPassword);
                _logger.LogInformation("Sending email to {ToEmail}...", toEmail);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);
                _logger.LogInformation("Email sent successfully to {ToEmail}", toEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {ToEmail}. Error: {ErrorMessage}", toEmail, ex.Message);
                throw;
            }
        }
    }
}



