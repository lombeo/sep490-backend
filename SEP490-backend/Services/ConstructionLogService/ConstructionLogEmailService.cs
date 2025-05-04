using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sep490_Backend.Infra;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Infra.Enums;
using Sep490_Backend.Services.Hosted;
using System.Linq;

namespace Sep490_Backend.Services.ConstructionLogService
{
    public interface IConstructionLogEmailService
    {
        Task SendConstructionLogStatusNotification(int constructionLogId, ConstructionLogStatus status, int actionBy);
    }

    public class ConstructionLogEmailService : IConstructionLogEmailService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IHostedService _emailNotificationService;
        private readonly ILogger<ConstructionLogEmailService> _logger;

        public ConstructionLogEmailService(
            IServiceScopeFactory serviceScopeFactory,
            IEnumerable<IHostedService> hostedServices,
            ILogger<ConstructionLogEmailService> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            // Find the EmailNotificationService from the hosted services
            _emailNotificationService = hostedServices.FirstOrDefault(s => s is EmailNotificationService) 
                ?? throw new InvalidOperationException("EmailNotificationService not found in hosted services");
            _logger = logger;
        }

        public async Task SendConstructionLogStatusNotification(int constructionLogId, ConstructionLogStatus status, int actionBy)
        {
            try
            {
                // Create a new scope to resolve scoped services
                using (var scope = _serviceScopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<BackendContext>();
                    
                    // Get the construction log
                    var constructionLog = await context.ConstructionLogs
                        .Include(cl => cl.Project)
                        .FirstOrDefaultAsync(cl => cl.Id == constructionLogId && !cl.Deleted);

                    if (constructionLog == null)
                    {
                        _logger.LogWarning("Construction log with ID {ConstructionLogId} not found", constructionLogId);
                        return;
                    }

                    // Get the action performer
                    var actionPerformer = await context.Users
                        .FirstOrDefaultAsync(u => u.Id == actionBy && !u.Deleted);

                    if (actionPerformer == null)
                    {
                        _logger.LogWarning("User with ID {UserId} not found", actionBy);
                        return;
                    }

                    // Get all project users
                    var projectUsers = await context.ProjectUsers
                        .Where(pu => pu.ProjectId == constructionLog.ProjectId && !pu.Deleted)
                        .Include(pu => pu.User)
                        .ToListAsync();

                    if (!projectUsers.Any())
                    {
                        _logger.LogWarning("No users found for project with ID {ProjectId}", constructionLog.ProjectId);
                        return;
                    }

                    // Prepare the email subject and body
                    string actionType = status == ConstructionLogStatus.Approved ? "approved" : "rejected";
                    string subject = $"Construction Log {actionType}: {constructionLog.LogCode} - {constructionLog.LogName}";

                    string body = GenerateEmailBody(constructionLog, actionPerformer, status);

                    // Get recipients' email addresses
                    var recipients = projectUsers
                        .Where(pu => pu.User != null && !string.IsNullOrEmpty(pu.User.Email))
                        .Select(pu => pu.User.Email)
                        .Distinct()
                        .ToList();

                    // Queue the emails
                    var emailService = (EmailNotificationService)_emailNotificationService;
                    emailService.QueueEmails(recipients, subject, body);
                    
                    _logger.LogInformation(
                        "Queued {Count} emails for Construction Log {LogId} status change to {Status}",
                        recipients.Count,
                        constructionLogId,
                        status);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending construction log status notification for log ID {ConstructionLogId}", constructionLogId);
            }
        }

        private string GenerateEmailBody(ConstructionLog constructionLog, User actionPerformer, ConstructionLogStatus status)
        {
            string statusText = status == ConstructionLogStatus.Approved ? "approved" : "rejected";
            string statusColor = status == ConstructionLogStatus.Approved ? "#4CAF50" : "#F44336";

            return $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <style>
                        body {{
                            font-family: Arial, sans-serif;
                            line-height: 1.6;
                            color: #333;
                        }}
                        .container {{
                            max-width: 600px;
                            margin: 0 auto;
                            padding: 20px;
                            border: 1px solid #ddd;
                            border-radius: 5px;
                        }}
                        .header {{
                            background-color: #f8f9fa;
                            padding: 10px;
                            border-radius: 5px;
                            margin-bottom: 20px;
                        }}
                        .status {{
                            font-weight: bold;
                            color: {statusColor};
                            text-transform: uppercase;
                        }}
                        .footer {{
                            margin-top: 30px;
                            font-size: 12px;
                            color: #777;
                            border-top: 1px solid #ddd;
                            padding-top: 10px;
                        }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h2>Construction Log Status Update</h2>
                        </div>
                        <p>Hello,</p>
                        <p>This is to inform you that the Construction Log <strong>{constructionLog.LogCode}</strong> for project <strong>{constructionLog.Project?.ProjectName ?? "Unknown"}</strong> has been <span class='status'>{statusText}</span> by {actionPerformer.FullName}.</p>
                        
                        <h3>Construction Log Details:</h3>
                        <ul>
                            <li><strong>Log Name:</strong> {constructionLog.LogName}</li>
                            <li><strong>Log Date:</strong> {constructionLog.LogDate:yyyy-MM-dd}</li>
                            <li><strong>Status:</strong> <span class='status'>{statusText}</span></li>
                            <li><strong>Updated By:</strong> {actionPerformer.FullName}</li>
                            <li><strong>Updated At:</strong> {constructionLog.UpdatedAt:yyyy-MM-dd HH:mm:ss}</li>
                        </ul>
                        
                        <p>You can view the full details of this construction log in the system.</p>
                        
                        <div class='footer'>
                            <p>This is an automated message. Please do not reply directly to this email.</p>
                        </div>
                    </div>
                </body>
                </html>";
        }
    }
} 