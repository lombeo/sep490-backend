using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sep490_Backend.Infra;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Infra.Enums;
using Sep490_Backend.Services.Hosted;
using System.Text.Json;

namespace Sep490_Backend.Services.ResourceReqService
{
    public interface IResourceMobilizationEmailService
    {
        Task SendMobilizationStatusChangeNotification(int mobilizationId, RequestStatus newStatus, int actionBy);
    }

    public class ResourceMobilizationEmailService : IResourceMobilizationEmailService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IHostedService _emailNotificationService;
        private readonly ILogger<ResourceMobilizationEmailService> _logger;

        public ResourceMobilizationEmailService(
            IServiceScopeFactory serviceScopeFactory,
            IEnumerable<IHostedService> hostedServices,
            ILogger<ResourceMobilizationEmailService> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            // Find the EmailNotificationService from the hosted services
            _emailNotificationService = hostedServices.FirstOrDefault(s => s is EmailNotificationService) 
                ?? throw new InvalidOperationException("EmailNotificationService not found in hosted services");
            _logger = logger;
        }

        public async Task SendMobilizationStatusChangeNotification(int mobilizationId, RequestStatus newStatus, int actionBy)
        {
            try
            {
                // Create a new scope to resolve scoped services
                using (var scope = _serviceScopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<BackendContext>();
                    
                    // Get the mobilization request with all required data
                    var mobilization = await context.ResourceMobilizationReqs
                        .Include(m => m.Project)
                        .FirstOrDefaultAsync(m => m.Id == mobilizationId && !m.Deleted);

                    if (mobilization == null)
                    {
                        _logger.LogWarning("Resource Mobilization Request with ID {MobilizationId} not found", mobilizationId);
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

                    // Get project users who should receive the notification
                    var projectUsers = await context.ProjectUsers
                        .Where(pu => pu.ProjectId == mobilization.ProjectId && !pu.Deleted)
                        .Include(pu => pu.User)
                        .ToListAsync();

                    if (!projectUsers.Any())
                    {
                        _logger.LogWarning("No users found for project with ID {ProjectId}", mobilization.ProjectId);
                        return;
                    }

                    // Get resource manager users who should also receive the notification
                    var resourceManagers = await context.Users
                        .Where(u => u.Role == RoleConstValue.RESOURCE_MANAGER && !u.Deleted)
                        .ToListAsync();

                    // Prepare the email subject and body
                    string subject = $"Resource Mobilization Request Status Changed: {mobilization.RequestCode}";
                    string body = GenerateEmailBody(mobilization, actionPerformer, newStatus);

                    // Get recipients' email addresses - both project users and resource managers
                    var projectUserEmails = projectUsers
                        .Where(pu => pu.User != null && !string.IsNullOrEmpty(pu.User.Email))
                        .Select(pu => pu.User.Email)
                        .Distinct()
                        .ToList();

                    var resourceManagerEmails = resourceManagers
                        .Where(u => !string.IsNullOrEmpty(u.Email))
                        .Select(u => u.Email)
                        .Distinct()
                        .ToList();

                    // Merge and deduplicate the email lists
                    var recipients = projectUserEmails.Union(resourceManagerEmails).Distinct().ToList();

                    // Queue the emails
                    var emailService = (EmailNotificationService)_emailNotificationService;
                    emailService.QueueEmails(recipients, subject, body);
                    
                    _logger.LogInformation(
                        "Queued {Count} emails for Resource Mobilization Request {MobilizationId} status change to {Status}",
                        recipients.Count,
                        mobilizationId,
                        newStatus);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending resource mobilization status notification for request ID {MobilizationId}", mobilizationId);
            }
        }

        private string GenerateEmailBody(ResourceMobilizationReqs mobilization, User actionPerformer, RequestStatus newStatus)
        {
            // Get status name for display
            string statusText = GetStatusDisplayName(newStatus);
            string statusColor = GetStatusColor(newStatus);

            // Format the resource mobilization details
            var resourceDetailsHtml = new System.Text.StringBuilder();
            if (mobilization.ResourceMobilizationDetails != null && mobilization.ResourceMobilizationDetails.Any())
            {
                resourceDetailsHtml.Append("<table border='1' style='border-collapse: collapse; width: 100%;'>");
                resourceDetailsHtml.Append("<tr><th>Resource Type</th><th>Resource Name</th><th>Quantity</th><th>Unit</th></tr>");

                foreach (var detail in mobilization.ResourceMobilizationDetails)
                {
                    resourceDetailsHtml.Append("<tr>");
                    resourceDetailsHtml.Append($"<td>{detail.ResourceType}</td>");
                    resourceDetailsHtml.Append($"<td>{detail.Name ?? $"Resource {detail.ResourceId}"}</td>");
                    resourceDetailsHtml.Append($"<td>{detail.Quantity}</td>");
                    resourceDetailsHtml.Append($"<td>{detail.Unit ?? "Unit"}</td>");
                    resourceDetailsHtml.Append("</tr>");
                }

                resourceDetailsHtml.Append("</table>");
            }
            else
            {
                resourceDetailsHtml.Append("<p>No resource details available</p>");
            }

            // Format dates
            string requestDate = mobilization.RequestDate.ToString("yyyy-MM-dd");
            string createdDate = mobilization.CreatedAt.HasValue 
                ? mobilization.CreatedAt.Value.ToString("yyyy-MM-dd HH:mm:ss")
                : "N/A";
            string updatedDate = mobilization.UpdatedAt.HasValue
                ? mobilization.UpdatedAt.Value.ToString("yyyy-MM-dd HH:mm:ss")
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
                        table {{
                            width: 100%;
                            border-collapse: collapse;
                        }}
                        th, td {{
                            padding: 8px;
                            text-align: left;
                            border: 1px solid #ddd;
                        }}
                        th {{
                            background-color: #f2f2f2;
                        }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h2>Resource Mobilization Request Status Update</h2>
                        </div>
                        <p>Hello,</p>
                        <p>This is to inform you that the Resource Mobilization Request <strong>{mobilization.RequestName}</strong> (Code: {mobilization.RequestCode}) has been updated to status <span class='status'>{statusText}</span> by {actionPerformer.FullName}.</p>
                        
                        <h3>Request Details:</h3>
                        <ul>
                            <li><strong>Request Code:</strong> {mobilization.RequestCode}</li>
                            <li><strong>Request Name:</strong> {mobilization.RequestName}</li>
                            <li><strong>Project:</strong> {mobilization.Project?.ProjectName ?? "Unknown"}</li>
                            <li><strong>Request Type:</strong> {mobilization.RequestType}</li>
                            <li><strong>Priority Level:</strong> {mobilization.PriorityLevel}</li>
                            <li><strong>Request Date:</strong> {requestDate}</li>
                            <li><strong>Status:</strong> <span class='status'>{statusText}</span></li>
                            <li><strong>Created At:</strong> {createdDate}</li>
                            <li><strong>Updated At:</strong> {updatedDate}</li>
                        </ul>
                        
                        <h3>Resource Details:</h3>
                        {resourceDetailsHtml}
                        
                        <div class='footer'>
                            <p>This is an automated message. Please do not reply directly to this email.</p>
                        </div>
                    </div>
                </body>
                </html>";
        }

        private string GetStatusDisplayName(RequestStatus status)
        {
            return status switch
            {
                RequestStatus.Draft => "Draft",
                RequestStatus.WaitManagerApproval => "Waiting for Manager Approval",
                RequestStatus.ManagerApproved => "Manager Approved",
                RequestStatus.BodApproved => "Executive Board Approved",
                RequestStatus.Reject => "Rejected",
                _ => status.ToString()
            };
        }

        private string GetStatusColor(RequestStatus status)
        {
            return status switch
            {
                RequestStatus.Draft => "#6c757d",           // Secondary gray
                RequestStatus.WaitManagerApproval => "#ffc107", // Warning yellow
                RequestStatus.ManagerApproved => "#007bff",     // Primary blue
                RequestStatus.BodApproved => "#28a745",        // Success green
                RequestStatus.Reject => "#dc3545",           // Danger red
                _ => "#6c757d"                               // Default gray
            };
        }
    }
} 