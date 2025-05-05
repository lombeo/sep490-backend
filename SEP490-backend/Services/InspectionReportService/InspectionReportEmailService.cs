using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sep490_Backend.Infra;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Infra.Enums;
using Sep490_Backend.Services.Hosted;
using System.Linq;
using System.Text.Json;
using Sep490_Backend.DTO;
using Sep490_Backend.DTO.InspectionReport;

namespace Sep490_Backend.Services.InspectionReportService
{
    public interface IInspectionReportEmailService
    {
        Task SendInspectionReportStatusNotification(int inspectionReportId, InspectionReportStatus status, int actionBy);
    }

    public class InspectionReportEmailService : IInspectionReportEmailService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IHostedService _emailNotificationService;
        private readonly ILogger<InspectionReportEmailService> _logger;

        public InspectionReportEmailService(
            IServiceScopeFactory serviceScopeFactory,
            IEnumerable<IHostedService> hostedServices,
            ILogger<InspectionReportEmailService> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            // Find the EmailNotificationService from the hosted services
            _emailNotificationService = hostedServices.FirstOrDefault(s => s is EmailNotificationService) 
                ?? throw new InvalidOperationException("EmailNotificationService not found in hosted services");
            _logger = logger;
        }

        public async Task SendInspectionReportStatusNotification(int inspectionReportId, InspectionReportStatus status, int actionBy)
        {
            try
            {
                // Create a new scope to resolve scoped services
                using (var scope = _serviceScopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<BackendContext>();
                    
                    // Get the inspection report with all required data
                    var inspectionReport = await context.Set<InspectionReport>()
                        .Include(ir => ir.Inspector)
                        .Include(ir => ir.ConstructionProgressItem)
                        .ThenInclude(cpi => cpi.ConstructionProgress)
                        .ThenInclude(cp => cp.Project)
                        .FirstOrDefaultAsync(ir => ir.Id == inspectionReportId && !ir.Deleted);

                    if (inspectionReport == null)
                    {
                        _logger.LogWarning("Inspection report with ID {InspectionReportId} not found", inspectionReportId);
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

                    // Get project ID
                    int projectId = inspectionReport.ConstructionProgressItem.ConstructionProgress.ProjectId;

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
                    string actionType = status == InspectionReportStatus.Approved ? "approved" : "rejected";
                    string subject = $"Inspection Report {actionType}: {inspectionReport.InspectCode} - {inspectionReport.InspectionName}";

                    string body = GenerateEmailBody(inspectionReport, actionPerformer, status);

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
                        "Queued {Count} emails for Inspection Report {ReportId} status change to {Status}",
                        recipients.Count,
                        inspectionReportId,
                        status);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending inspection report status notification for report ID {InspectionReportId}", inspectionReportId);
            }
        }

        private string GenerateEmailBody(InspectionReport inspectionReport, User actionPerformer, InspectionReportStatus status)
        {
            string statusText = status == InspectionReportStatus.Approved ? "approved" : "rejected";
            string statusColor = status == InspectionReportStatus.Approved ? "#4CAF50" : "#F44336";

            // Format dates
            string inspectStartDate = inspectionReport.InspectStartDate.ToString("yyyy-MM-dd");
            string inspectEndDate = inspectionReport.InspectEndDate.ToString("yyyy-MM-dd");
            string createdDate = inspectionReport.CreatedAt.HasValue 
                ? inspectionReport.CreatedAt.Value.ToString("yyyy-MM-dd HH:mm:ss")
                : "N/A";
            string updatedDate = inspectionReport.UpdatedAt.HasValue
                ? inspectionReport.UpdatedAt.Value.ToString("yyyy-MM-dd HH:mm:ss")
                : "N/A";

            // Get inspection decision text
            string decisionText = (InspectionDecision)inspectionReport.InspectionDecision switch
            {
                InspectionDecision.Pass => "Pass",
                InspectionDecision.Fail => "Fail",
                _ => "None"
            };

            // Get attachment count
            int attachmentCount = 0;
            if (inspectionReport.Attachment != null)
            {
                try
                {
                    var attachments = JsonSerializer.Deserialize<List<AttachmentInfo>>(
                        inspectionReport.Attachment.RootElement.ToString());
                    attachmentCount = attachments?.Count ?? 0;
                }
                catch
                {
                    // Ignore deserialization errors
                }
            }

            string projectName = inspectionReport.ConstructionProgressItem?.ConstructionProgress?.Project?.ProjectName ?? "Unknown";
            string progressItemName = inspectionReport.ConstructionProgressItem?.WorkName ?? "Unknown";

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
                            <h2>Inspection Report Status Update</h2>
                        </div>
                        <p>Hello,</p>
                        <p>This is to inform you that the Inspection Report <strong>{inspectionReport.InspectCode}</strong> for project <strong>{projectName}</strong> has been <span class='status'>{statusText}</span> by {actionPerformer.FullName}.</p>
                        
                        <h3>Inspection Report Details:</h3>
                        <ul>
                            <li><strong>Report Code:</strong> {inspectionReport.InspectCode}</li>
                            <li><strong>Inspection Name:</strong> {inspectionReport.InspectionName}</li>
                            <li><strong>Project:</strong> {projectName}</li>
                            <li><strong>Progress Item:</strong> {progressItemName}</li>
                            <li><strong>Inspector:</strong> {inspectionReport.Inspector?.FullName ?? "Unknown"}</li>
                            <li><strong>Inspection Period:</strong> {inspectStartDate} to {inspectEndDate}</li>
                            <li><strong>Location:</strong> {inspectionReport.Location}</li>
                            <li><strong>Inspection Decision:</strong> {decisionText}</li>
                            <li><strong>Status:</strong> <span class='status'>{statusText}</span></li>
                            <li><strong>Attachments:</strong> {attachmentCount} file(s)</li>
                            <li><strong>Created At:</strong> {createdDate}</li>
                            <li><strong>Updated At:</strong> {updatedDate}</li>
                        </ul>
                        
                        <p>You can view the full details of this inspection report in the system.</p>
                        
                        <div class='footer'>
                            <p>This is an automated message. Please do not reply directly to this email.</p>
                        </div>
                    </div>
                </body>
                </html>";
        }
    }
} 