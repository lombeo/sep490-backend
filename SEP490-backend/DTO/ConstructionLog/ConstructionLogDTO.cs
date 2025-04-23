using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Sep490_Backend.Infra.Entities;

namespace Sep490_Backend.DTO.ConstructionLog
{
    // DTO for construction log responses
    public class ConstructionLogDTO
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public string LogCode { get; set; }
        public string LogName { get; set; }
        public DateTime LogDate { get; set; }
        public List<ConstructionLogResourceDTO> Resources { get; set; }
        public List<WorkAmountDTO> WorkAmount { get; set; }
        public List<WeatherDTO> Weather { get; set; }
        public string Safety { get; set; }
        public string Quality { get; set; }
        public string Progress { get; set; }
        public string Problem { get; set; }
        public string Advice { get; set; }
        public List<AttachmentInfo> Images { get; set; }
        public List<AttachmentInfo> Attachments { get; set; }
        public string Note { get; set; }
        public ConstructionLogStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public int Creator { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int Updater { get; set; }
        public bool Deleted { get; set; }
    }

    // DTO for saving a construction log
    public class SaveConstructionLogDTO
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public string LogName { get; set; }
        public DateTime LogDate { get; set; }
        public List<ConstructionLogResourceDTO>? Resources { get; set; }
        public List<WorkAmountDTO>? WorkAmount { get; set; }
        public List<WeatherDTO>? Weather { get; set; }
        public string Safety { get; set; }
        public string Quality { get; set; }
        public string Progress { get; set; }
        public string Problem { get; set; }
        public string Advice { get; set; }
        public List<string>? Images { get; set; }
        public List<IFormFile>? ImageFiles { get; set; }
        public IFormFile? ImageFile { get; set; }
        public List<IFormFile>? AttachmentFiles { get; set; }
        public string Note { get; set; }
        public ConstructionLogStatus? Status { get; set; }
    }

    // DTO for searching construction logs
    public class SearchConstructionLogDTO
    {
        public int? ProjectId { get; set; }
        public string? LogCode { get; set; }
        public string? LogName { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? TaskIndex { get; set; }
        public ConstructionLogStatus? Status { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int ActionBy { get; set; }
    }

    // DTO for resource entries
    public class ConstructionLogResourceDTO
    {
        public int? Id { get; set; }
        public string TaskIndex { get; set; }
        public int ResourceType { get; set; } // 1: Labor, 2: Machinery, 3: Material
        public decimal Quantity { get; set; }
        public int ResourceId { get; set; }     
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
    }

    // DTO for work amount entries
    public class WorkAmountDTO
    {
        public int? Id { get; set; }
        public string TaskIndex { get; set; }
        public decimal WorkAmount { get; set; }
    }

    // DTO for weather entries
    public class WeatherDTO
    {
        public string Type { get; set; }
        public List<string> Values { get; set; }
    }

    // DTO for resource logs by task
    public class ResourceLogByTaskDTO
    {
        public List<TaskWorkAmountDTO> WorkAmount { get; set; }
        public List<TaskResourceDTO> Resources { get; set; }
    }

    // DTO for task work amount entries
    public class TaskWorkAmountDTO
    {
        public string LogDate { get; set; }
        public decimal WorkAmount { get; set; }
    }

    // DTO for task resource entries
    public class TaskResourceDTO
    {
        public string LogDate { get; set; }
        public int ResourceId { get; set; }
        public int ResourceType { get; set; }
        public string Unit { get; set; }
        public decimal Quantity { get; set; }
    }
} 