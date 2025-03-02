using Microsoft.AspNetCore.Http;
using Sep490_Backend.Infra.Enums;
using System.Collections.Generic;

namespace Sep490_Backend.DTO.Project
{
    public class SaveProjectDTO
    {
        public int Id { get; set; }
        public string ProjectCode { get; set; }
        public string ProjectName { get; set; }
        public int CustomerId { get; set; }
        public string ConstructType { get; set; }
        public string? Location { get; set; }
        public string? Area { get; set; }
        public string? Purpose { get; set; }
        public string? TechnicalReqs { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal Budget { get; set; }
        public ProjectStatusEnum Status { get; set; }
        public List<IFormFile>? Attachments { get; set; }
        public string? Description { get; set; }
    }
}
