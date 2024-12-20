using MailKit.Net.Smtp;
using MimeKit;

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
        private readonly string _emailPassword = Environment.GetEnvironmentVariable("MAIL_PASSWORD");

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            var emailMessage = new MimeMessage();
            emailMessage.From.Add(new MailboxAddress("Sender", _emailFrom));
            emailMessage.To.Add(new MailboxAddress("Recipient", to));
            emailMessage.Subject = subject;

            emailMessage.Body = new TextPart("html") { Text = body };

            using (var client = new SmtpClient())
            {
                await client.ConnectAsync(_smtpServer, _smtpPort, false);
                await client.AuthenticateAsync(_emailFrom, _emailPassword);
                await client.SendAsync(emailMessage);
                await client.DisconnectAsync(true);
            }
        }
    }
}
