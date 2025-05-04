using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sep490_Backend.Infra;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Infra.Enums;
using Sep490_Backend.Services.Hosted;
using System.Linq;

namespace Sep490_Backend.Services.ConstructionPlanService
{
    public interface IConstructionPlanEmailService
    {
        Task SendConstructionPlanStatusNotification(int constructionPlanId, bool isApproved, int actionBy);
    }

    public class ConstructionPlanEmailService : IConstructionPlanEmailService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IHostedService _emailNotificationService;
        private readonly ILogger<ConstructionPlanEmailService> _logger;

        public ConstructionPlanEmailService(
            IServiceScopeFactory serviceScopeFactory,
            IEnumerable<IHostedService> hostedServices,
            ILogger<ConstructionPlanEmailService> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            // Find the EmailNotificationService from the hosted services
            _emailNotificationService = hostedServices.FirstOrDefault(s => s is EmailNotificationService) 
                ?? throw new InvalidOperationException("EmailNotificationService not found in hosted services");
            _logger = logger;
        }

        public async Task SendConstructionPlanStatusNotification(int constructionPlanId, bool isApproved, int actionBy)
        {
            try
            {
                // Create a new scope to resolve scoped services
                using (var scope = _serviceScopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<BackendContext>();
                    
                    // Get the construction plan with all required data
                    var constructionPlan = await context.ConstructionPlans
                        .Include(cp => cp.Project)
                        .Include(cp => cp.Reviewers)
                        .FirstOrDefaultAsync(cp => cp.Id == constructionPlanId && !cp.Deleted);

                    if (constructionPlan == null)
                    {
                        _logger.LogWarning("Construction plan with ID {ConstructionPlanId} not found", constructionPlanId);
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
                        .Where(pu => pu.ProjectId == constructionPlan.ProjectId && !pu.Deleted)
                        .Include(pu => pu.User)
                        .ToListAsync();

                    if (!projectUsers.Any())
                    {
                        _logger.LogWarning("No users found for project with ID {ProjectId}", constructionPlan.ProjectId);
                        return;
                    }

                    // Prepare the email subject and body
                    string actionType = isApproved ? "approved" : "rejected";
                    string subject = $"Construction Plan {actionType}: {constructionPlan.PlanName}";

                    string body = GenerateEmailBody(constructionPlan, actionPerformer, isApproved);

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
                        "Queued {Count} emails for Construction Plan {PlanId} status change to {Status}",
                        recipients.Count,
                        constructionPlanId,
                        isApproved ? "Approved" : "Rejected");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending construction plan status notification for plan ID {ConstructionPlanId}", constructionPlanId);
            }
        }

        private string GenerateEmailBody(ConstructionPlan constructionPlan, User actionPerformer, bool isApproved)
        {
            string statusText = isApproved ? "approved" : "rejected";
            string statusColor = isApproved ? "#4CAF50" : "#F44336";

            // Calculate approval status details
            int totalReviewers = constructionPlan.Reviewer?.Count ?? 0;
            int approvedReviewers = 0;
            int rejectedReviewers = 0;
            
            if (constructionPlan.Reviewer != null)
            {
                approvedReviewers = constructionPlan.Reviewer.Count(r => r.Value == true);
                rejectedReviewers = constructionPlan.Reviewer.Count(r => r.Value == false);
            }

            // Format creation and update dates
            string createdDate = constructionPlan.CreatedAt.HasValue 
                ? constructionPlan.CreatedAt.Value.ToString("yyyy-MM-dd HH:mm:ss") 
                : "N/A";
                
            string updatedDate = constructionPlan.UpdatedAt.HasValue 
                ? constructionPlan.UpdatedAt.Value.ToString("yyyy-MM-dd HH:mm:ss") 
                : "N/A";

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
                            <h2>Construction Plan Status Update</h2>
                        </div>
                        <p>Hello,</p>
                        <p>This is to inform you that the Construction Plan <strong>{constructionPlan.PlanName}</strong> for project <strong>{constructionPlan.Project?.ProjectName ?? "Unknown"}</strong> has been <span class='status'>{statusText}</span> by {actionPerformer.FullName}.</p>
                        
                        <h3>Construction Plan Details:</h3>
                        <ul>
                            <li><strong>Plan Name:</strong> {constructionPlan.PlanName}</li>
                            <li><strong>Project:</strong> {constructionPlan.Project?.ProjectName ?? "Unknown"}</li>
                            <li><strong>Status:</strong> <span class='status'>{statusText}</span></li>
                            <li><strong>Approvals:</strong> {approvedReviewers} of {totalReviewers} reviewers</li>
                            <li><strong>Created By:</strong> {constructionPlan.Creator}</li>
                            <li><strong>Created At:</strong> {createdDate}</li>
                            <li><strong>Updated By:</strong> {actionPerformer.FullName}</li>
                            <li><strong>Updated At:</strong> {updatedDate}</li>
                        </ul>
                        
                        <p>You can view the full details of this construction plan in the system.</p>
                        
                        <div class='footer'>
                            <p>This is an automated message. Please do not reply directly to this email.</p>
                        </div>
                    </div>
                </body>
                </html>";
        }
    }
} 