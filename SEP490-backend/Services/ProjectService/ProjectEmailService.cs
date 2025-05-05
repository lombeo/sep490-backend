using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sep490_Backend.Infra;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Infra.Enums;
using Sep490_Backend.Services.Hosted;
using System.Linq;
using System.Text.Json;

namespace Sep490_Backend.Services.ProjectService
{
    public interface IProjectEmailService
    {
        Task SendProjectStatusChangeNotification(int projectId, ProjectStatusEnum newStatus, int actionBy);
    }

    public class ProjectEmailService : IProjectEmailService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IHostedService _emailNotificationService;
        private readonly ILogger<ProjectEmailService> _logger;

        public ProjectEmailService(
            IServiceScopeFactory serviceScopeFactory,
            IEnumerable<IHostedService> hostedServices,
            ILogger<ProjectEmailService> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            // Find the EmailNotificationService from the hosted services
            _emailNotificationService = hostedServices.FirstOrDefault(s => s is EmailNotificationService) 
                ?? throw new InvalidOperationException("EmailNotificationService not found in hosted services");
            _logger = logger;
        }

        public async Task SendProjectStatusChangeNotification(int projectId, ProjectStatusEnum newStatus, int actionBy)
        {
            try
            {
                // Create a new scope to resolve scoped services
                using (var scope = _serviceScopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<BackendContext>();
                    
                    // Get the project with all required data
                    var project = await context.Set<Project>()
                        .Include(p => p.Customer)
                        .FirstOrDefaultAsync(p => p.Id == projectId && !p.Deleted);

                    if (project == null)
                    {
                        _logger.LogWarning("Project with ID {ProjectId} not found", projectId);
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
                        .Where(pu => pu.ProjectId == projectId && !pu.Deleted)
                        .Include(pu => pu.User)
                        .ToListAsync();

                    if (!projectUsers.Any())
                    {
                        _logger.LogWarning("No users found for project with ID {ProjectId}", projectId);
                        return;
                    }

                    // Prepare the email subject and body
                    string subject = $"Project Status Changed: {project.ProjectCode} - {project.ProjectName}";

                    string body = GenerateEmailBody(project, actionPerformer, newStatus);

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
                        "Queued {Count} emails for Project {ProjectId} status change to {Status}",
                        recipients.Count,
                        projectId,
                        newStatus);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending project status notification for project ID {ProjectId}", projectId);
            }
        }

        private string GenerateEmailBody(Project project, User actionPerformer, ProjectStatusEnum newStatus)
        {
            // Get status name for display
            string statusText = GetStatusDisplayName(newStatus);
            string statusColor = GetStatusColor(newStatus);

            // Format dates
            string startDate = project.StartDate.ToString("yyyy-MM-dd");
            string endDate = project.EndDate.ToString("yyyy-MM-dd");
            string createdDate = project.CreatedAt.HasValue 
                ? project.CreatedAt.Value.ToString("yyyy-MM-dd HH:mm:ss")
                : "N/A";
            string updatedDate = project.UpdatedAt.HasValue
                ? project.UpdatedAt.Value.ToString("yyyy-MM-dd HH:mm:ss")
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
                            <h2>Project Status Update</h2>
                        </div>
                        <p>Hello,</p>
                        <p>This is to inform you that the project <strong>{project.ProjectName}</strong> (Code: {project.ProjectCode}) has been updated to status <span class='status'>{statusText}</span> by {actionPerformer.FullName}.</p>
                        
                        <h3>Project Details:</h3>
                        <ul>
                            <li><strong>Project Code:</strong> {project.ProjectCode}</li>
                            <li><strong>Project Name:</strong> {project.ProjectName}</li>
                            <li><strong>Customer:</strong> {project.Customer?.CustomerName ?? "Unknown"}</li>
                            <li><strong>Location:</strong> {project.Location ?? "Not specified"}</li>
                            <li><strong>Area:</strong> {project.Area ?? "Not specified"}</li>
                            <li><strong>Construction Type:</strong> {project.ConstructType ?? "Not specified"}</li>
                            <li><strong>Project Period:</strong> {startDate} to {endDate}</li>
                            <li><strong>Budget:</strong> {project.Budget.ToString("N2")}</li>
                            <li><strong>Status:</strong> <span class='status'>{statusText}</span></li>
                            <li><strong>Created At:</strong> {createdDate}</li>
                            <li><strong>Updated At:</strong> {updatedDate}</li>
                        </ul>
                        
                        <p>You can view the full details of this project in the system.</p>
                        
                        <div class='footer'>
                            <p>This is an automated message. Please do not reply directly to this email.</p>
                        </div>
                    </div>
                </body>
                </html>";
        }

        private string GetStatusDisplayName(ProjectStatusEnum status)
        {
            return status switch
            {
                ProjectStatusEnum.ReceiveRequest => "Receive Request",
                ProjectStatusEnum.Planning => "Planning",
                ProjectStatusEnum.InProgress => "In Progress",
                ProjectStatusEnum.WaitingApproveCompleted => "Waiting for Completion Approval",
                ProjectStatusEnum.Completed => "Completed",
                ProjectStatusEnum.Paused => "Paused",
                ProjectStatusEnum.Closed => "Closed",
                _ => status.ToString()
            };
        }

        private string GetStatusColor(ProjectStatusEnum status)
        {
            return status switch
            {
                ProjectStatusEnum.ReceiveRequest => "#17a2b8",     // Info blue
                ProjectStatusEnum.Planning => "#6c757d",           // Secondary gray
                ProjectStatusEnum.InProgress => "#007bff",         // Primary blue
                ProjectStatusEnum.WaitingApproveCompleted => "#ffc107", // Warning yellow
                ProjectStatusEnum.Completed => "#28a745",          // Success green
                ProjectStatusEnum.Paused => "#fd7e14",             // Orange
                ProjectStatusEnum.Closed => "#6c757d",             // Secondary gray
                _ => "#6c757d"                                     // Default gray
            };
        }
    }
} 