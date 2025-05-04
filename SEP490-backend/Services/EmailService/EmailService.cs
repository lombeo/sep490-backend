using MailKit.Net.Smtp;
using MimeKit;
using Microsoft.Extensions.Logging;

namespace Sep490_Backend.Services.EmailService
{
    public interface IEmailService
    {
        Task SendEmailAsync(string to, string subject, string body);
    }

    public class EmailService : IEmailService
    {
        private readonly string _smtpServer = "smtp.gmail.com";
        private readonly int _smtpPort = 587;
        private readonly string _emailFrom = "longrpk200313@gmail.com";
        private readonly string _emailPassword;
        private readonly ILogger<EmailService> _logger;

        public EmailService(ILogger<EmailService> logger)
        {
            _logger = logger;
            
            // Try multiple approaches to get the password
            _emailPassword = GetEmailPassword();
            
            _logger.LogInformation("EmailService initialized with email: {EmailFrom}, password length: {PasswordLength}", 
                _emailFrom, 
                string.IsNullOrEmpty(_emailPassword) ? 0 : _emailPassword.Length);
        }
        
        private string GetEmailPassword()
        {
            // Try to get the password from environment variables
            string password = Environment.GetEnvironmentVariable("MAIL_PASSWORD");
            
            if (!string.IsNullOrEmpty(password))
            {
                _logger.LogInformation("Found MAIL_PASSWORD in environment variables");
                return password;
            }
            
            // Hardcoded password as a fallback (normally not recommended, but useful for debugging this specific issue)
            _logger.LogWarning("MAIL_PASSWORD not found in environment variables, using hardcoded fallback");
            return "zoen whrc geph qeqb";
        }

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            try
            {
                _logger.LogInformation("Attempting to send email to {Recipient} with subject: {Subject}", to, subject);
                
                if (string.IsNullOrEmpty(_emailPassword))
                {
                    _logger.LogError("Email password is empty or null. Cannot send email.");
                    return;
                }

                var emailMessage = new MimeMessage();
                emailMessage.From.Add(new MailboxAddress("Sender", _emailFrom));
                emailMessage.To.Add(new MailboxAddress("Recipient", to));
                emailMessage.Subject = subject;

                emailMessage.Body = new TextPart("html") { Text = body };

                using (var client = new SmtpClient())
                {
                    _logger.LogInformation("Connecting to SMTP server: {Server}:{Port}", _smtpServer, _smtpPort);
                    await client.ConnectAsync(_smtpServer, _smtpPort, false);
                    
                    _logger.LogInformation("Authenticating with email: {Email}", _emailFrom);
                    await client.AuthenticateAsync(_emailFrom, _emailPassword);
                    
                    _logger.LogInformation("Sending email...");
                    await client.SendAsync(emailMessage);
                    
                    _logger.LogInformation("Disconnecting from SMTP server");
                    await client.DisconnectAsync(true);
                    
                    _logger.LogInformation("Email sent successfully to {Recipient}", to);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {Recipient} with subject: {Subject}", to, subject);
                throw;
            }
        }
    }
}
