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
    public interface IResourceAllocationEmailService
    {
        Task SendAllocationStatusChangeNotification(int allocationId, RequestStatus newStatus, int actionBy);
    }

    public class ResourceAllocationEmailService : IResourceAllocationEmailService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IHostedService _emailNotificationService;
        private readonly ILogger<ResourceAllocationEmailService> _logger;

        public ResourceAllocationEmailService(
            IServiceScopeFactory serviceScopeFactory,
            IEnumerable<IHostedService> hostedServices,
            ILogger<ResourceAllocationEmailService> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            // Find the EmailNotificationService from the hosted services
            _emailNotificationService = hostedServices.FirstOrDefault(s => s is EmailNotificationService) 
                ?? throw new InvalidOperationException("EmailNotificationService not found in hosted services");
            _logger = logger;
        }

        public async Task SendAllocationStatusChangeNotification(int allocationId, RequestStatus newStatus, int actionBy)
        {
            try
            {
                // Create a new scope to resolve scoped services
                using (var scope = _serviceScopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<BackendContext>();
                    
                    // Get the allocation request with all required data
                    var allocation = await context.ResourceAllocationReqs
                        .Include(a => a.FromProject)
                        .Include(a => a.ToProject)
                        .FirstOrDefaultAsync(a => a.Id == allocationId && !a.Deleted);

                    if (allocation == null)
                    {
                        _logger.LogWarning("Resource Allocation Request with ID {AllocationId} not found", allocationId);
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

                    // Get source project users who should receive the notification
                    var fromProjectUsers = await context.ProjectUsers
                        .Where(pu => pu.ProjectId == allocation.FromProjectId && !pu.Deleted)
                        .Include(pu => pu.User)
                        .ToListAsync();

                    // Get destination project users who should receive the notification
                    var toProjectUsers = await context.ProjectUsers
                        .Where(pu => pu.ProjectId == allocation.ToProjectId && !pu.Deleted)
                        .Include(pu => pu.User)
                        .ToListAsync();

                    if (!fromProjectUsers.Any() && !toProjectUsers.Any())
                    {
                        _logger.LogWarning("No users found for projects involved in allocation request {AllocationId}", allocationId);
                        return;
                    }

                    // Get resource manager users who should also receive the notification
                    var resourceManagers = await context.Users
                        .Where(u => u.Role == RoleConstValue.RESOURCE_MANAGER && !u.Deleted)
                        .ToListAsync();

                    // Prepare the email subject and body
                    string subject = $"Resource Allocation Request Status Changed: {allocation.RequestCode}";
                    string body = GenerateEmailBody(allocation, actionPerformer, newStatus);

                    // Get recipients' email addresses from all three groups
                    var fromProjectEmails = fromProjectUsers
                        .Where(pu => pu.User != null && !string.IsNullOrEmpty(pu.User.Email))
                        .Select(pu => pu.User.Email)
                        .Distinct()
                        .ToList();

                    var toProjectEmails = toProjectUsers
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
                    var recipients = fromProjectEmails
                        .Union(toProjectEmails)
                        .Union(resourceManagerEmails)
                        .Distinct()
                        .ToList();

                    // Queue the emails
                    var emailService = (EmailNotificationService)_emailNotificationService;
                    emailService.QueueEmails(recipients, subject, body);
                    
                    _logger.LogInformation(
                        "Queued {Count} emails for Resource Allocation Request {AllocationId} status change to {Status}",
                        recipients.Count,
                        allocationId,
                        newStatus);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending resource allocation status notification for request ID {AllocationId}", allocationId);
            }
        }

        private string GenerateEmailBody(ResourceAllocationReqs allocation, User actionPerformer, RequestStatus newStatus)
        {
            // Get status name for display
            string statusText = GetStatusDisplayName(newStatus);
            string statusColor = GetStatusColor(newStatus);

            // Format the resource allocation details
            var resourceDetailsHtml = new System.Text.StringBuilder();
            if (allocation.ResourceAllocationDetails != null && allocation.ResourceAllocationDetails.Any())
            {
                resourceDetailsHtml.Append("<table border='1' style='border-collapse: collapse; width: 100%;'>");
                resourceDetailsHtml.Append("<tr><th>Resource Type</th><th>Resource Name</th><th>Quantity</th><th>Unit</th></tr>");

                foreach (var detail in allocation.ResourceAllocationDetails)
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
            string requestDate = allocation.RequestDate.ToString("yyyy-MM-dd");
            string createdDate = allocation.CreatedAt.HasValue 
                ? allocation.CreatedAt.Value.ToString("yyyy-MM-dd HH:mm:ss")
                : "N/A";
            string updatedDate = allocation.UpdatedAt.HasValue
                ? allocation.UpdatedAt.Value.ToString("yyyy-MM-dd HH:mm:ss")
                : "N/A";

            // Format request type
            string requestTypeText = allocation.RequestType switch
            {
                1 => "Project to Project",
                2 => "Project to Task",
                3 => "Task to Task",
                _ => $"Type {allocation.RequestType}"
            };

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
                            <h2>Resource Allocation Request Status Update</h2>
                        </div>
                        <p>Hello,</p>
                        <p>This is to inform you that the Resource Allocation Request <strong>{allocation.RequestName}</strong> (Code: {allocation.RequestCode}) has been updated to status <span class='status'>{statusText}</span> by {actionPerformer.FullName}.</p>
                        
                        <h3>Request Details:</h3>
                        <ul>
                            <li><strong>Request Code:</strong> {allocation.RequestCode}</li>
                            <li><strong>Request Name:</strong> {allocation.RequestName}</li>
                            <li><strong>From Project:</strong> {allocation.FromProject?.ProjectName ?? "Unknown"}</li>
                            <li><strong>To Project:</strong> {allocation.ToProject?.ProjectName ?? "Unknown"}</li>
                            <li><strong>Request Type:</strong> {requestTypeText}</li>
                            <li><strong>Priority Level:</strong> {allocation.PriorityLevel}</li>
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