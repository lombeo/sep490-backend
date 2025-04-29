using Sep490_Backend.DTO.Common;
using Sep490_Backend.Infra.Enums;
using Microsoft.AspNetCore.Http;
using System.Text.Json.Serialization;
using Sep490_Backend.DTO.ConstructionLog;
using System.Text.Json;

namespace Sep490_Backend.DTO.InspectionReport
{
    public class InspectionReportDTO
    {
        public int Id { get; set; }
        public int ConstructionProgressItemId { get; set; }
        public string? ProgressItemName { get; set; }
        public string? ProjectName { get; set; }
        public string? InspectCode { get; set; }
        public int InspectorId { get; set; }
        public string? InspectorName { get; set; }
        public DateTime InspectStartDate { get; set; }
        public DateTime InspectEndDate { get; set; }
        public string? Location { get; set; }
        public List<AttachmentInfo>? Attachment { get; set; }
        public InspectionDecision InspectionDecision { get; set; }
        public InspectionReportStatus Status { get; set; }
        public string? QualityNote { get; set; }
        public string? OtherNote { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? CreatorName { get; set; }
        public string? UpdaterName { get; set; }
    }

    public class SaveInspectionReportDTO
    {
        public int Id { get; set; }
        public int ConstructionProgressItemId { get; set; }
        public int InspectorId { get; set; }
        public DateTime InspectStartDate { get; set; }
        public DateTime InspectEndDate { get; set; }
        public string? Location { get; set; }
        public List<IFormFile>? AttachmentFiles { get; set; }
        public InspectionDecision? InspectionDecision { get; set; }
        public InspectionReportStatus? Status { get; set; }
        public string? QualityNote { get; set; }
        public string? OtherNote { get; set; }
    }

    public class SearchInspectionReportDTO : BaseQuery
    {
        public int? ProjectId { get; set; }
        public int? InspectorId { get; set; }
        public int? ConstructionProgressItemId { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public InspectionReportStatus? Status { get; set; }
        public InspectionDecision? Decision { get; set; }
        public string? Keyword { get; set; }
    }
} 