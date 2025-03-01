using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Infra.Enums;
using System.Collections.Generic;

namespace Sep490_Backend.DTO.Project
{
    public class ProjectDTO : CommonEntity
    {
        public int Id { get; set; }
        public string ProjectCode { get; set; }
        public string ProjectName { get; set; }
        public Infra.Entities.Customer Customer { get; set; }
        public int ConstructType { get; set; }
        public string? Location { get; set; }
        public string? Area { get; set; }
        public string? Purpose { get; set; }
        public string? TechnicalReqs { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal Budget { get; set; }
        public ProjectStatusEnum Status { get; set; }
        public List<AttachmentInfo>? Attachments { get; set; }
        public string? Description { get; set; }
    }
}
