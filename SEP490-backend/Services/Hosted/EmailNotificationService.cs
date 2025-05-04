using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Sep490_Backend.Services.EmailService;

namespace Sep490_Backend.Services.Hosted
{
    public class EmailNotificationService : CustomBackgroundService<EmailNotificationService>
    {
        private readonly ConcurrentQueue<EmailNotification> _emailQueue = new ConcurrentQueue<EmailNotification>();
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public EmailNotificationService(
            ILogger<EmailNotificationService> logger, 
            IServiceProvider serviceProvider,
            IServiceScopeFactory serviceScopeFactory) : base(logger, serviceProvider)
        {
            _serviceScopeFactory = serviceScopeFactory;
            Logger.LogInformation("EmailNotificationService initialized. Processing interval: {Interval} seconds", 
                TimeSpanInSecond.TotalSeconds);
        }

        protected override TimeSpan TimeSpanInSecond { get; set; } = TimeSpan.FromSeconds(10); // Process emails every 10 seconds

        public void QueueEmail(string to, string subject, string body)
        {
            _emailQueue.Enqueue(new EmailNotification
            {
                To = to,
                Subject = subject,
                Body = body,
                EnqueuedAt = DateTime.UtcNow
            });
            
            Logger.LogInformation("Email queued for {Recipient} with subject: {Subject}. Queue size: {QueueSize}", 
                to, subject, _emailQueue.Count);
        }

        public void QueueEmails(IEnumerable<string> recipients, string subject, string body)
        {
            int count = 0;
            foreach (var recipient in recipients)
            {
                QueueEmail(recipient, subject, body);
                count++;
            }
            
            Logger.LogInformation("Queued {Count} emails with subject: {Subject}", count, subject);
        }

        protected override void InternalDoJob()
        {
            Logger.LogInformation("Starting email processing job. Queue size: {QueueSize}", _emailQueue.Count);
            
            if (_emailQueue.IsEmpty)
            {
                Logger.LogInformation("Email queue is empty. Nothing to process.");
                return;
            }
            
            const int batchSize = 5; // Process up to 5 emails per batch
            int processedCount = 0;

            while (processedCount < batchSize && _emailQueue.TryDequeue(out var notification))
            {
                try
                {
                    Logger.LogInformation("Processing email {Count}/{BatchSize} to {Recipient}", 
                        processedCount + 1, batchSize, notification.To);
                    
                    // Create a new scope to resolve scoped services
                    using (var scope = _serviceScopeFactory.CreateScope())
                    {
                        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                        
                        // Send email asynchronously but don't await it
                        emailService.SendEmailAsync(notification.To, notification.Subject, notification.Body)
                            .ContinueWith(task =>
                            {
                                if (task.IsFaulted)
                                {
                                    Logger.LogError(task.Exception, "Failed to send email notification to {Recipient}", notification.To);
                                    
                                    // If email is less than 1 hour old, requeue it for another attempt
                                    if (DateTime.UtcNow.Subtract(notification.EnqueuedAt).TotalHours < 1)
                                    {
                                        Logger.LogInformation("Requeuing email to {Recipient} for another attempt", notification.To);
                                        _emailQueue.Enqueue(notification);
                                    }
                                    else
                                    {
                                        Logger.LogWarning("Email to {Recipient} has expired (over 1 hour old) and won't be retried", 
                                            notification.To);
                                    }
                                }
                                else if (task.IsCompletedSuccessfully)
                                {
                                    Logger.LogInformation("Successfully sent email to {Recipient}", notification.To);
                                }
                            });
                    }

                    processedCount++;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error processing email notification for {Recipient}", notification.To);
                    
                    // If email is less than 1 hour old, requeue it for another attempt
                    if (DateTime.UtcNow.Subtract(notification.EnqueuedAt).TotalHours < 1)
                    {
                        Logger.LogInformation("Requeuing email to {Recipient} after error", notification.To);
                        _emailQueue.Enqueue(notification);
                    }
                    else
                    {
                        Logger.LogWarning("Email to {Recipient} has expired (over 1 hour old) and won't be retried", 
                            notification.To);
                    }
                }
            }
            
            Logger.LogInformation("Completed email processing job. Processed {Count} emails. Remaining in queue: {RemainingCount}", 
                processedCount, _emailQueue.Count);
        }
    }

    public class EmailNotification
    {
        public string To { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public DateTime EnqueuedAt { get; set; }
    }
} 