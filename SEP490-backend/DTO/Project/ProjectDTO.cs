using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Infra.Enums;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Sep490_Backend.DTO.Project
{
    public class ProjectDTO : CommonEntity
    {
        public int Id { get; set; }
        
        [Required(ErrorMessage = "Project code is required")]
        public string ProjectCode { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Project name is required")]
        public string ProjectName { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Customer information is required")]
        public Infra.Entities.Customer Customer { get; set; } = new Infra.Entities.Customer();
        
        [Required(ErrorMessage = "Construction type is required")]
        public string ConstructType { get; set; } = string.Empty;
        
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
        public bool IsCreator { get; set; } = false;
        public List<int> ViewerUserIds { get; set; } = new List<int>();
        public List<ProjectUserDTO>? ProjectUsers { get; set; }
    }
}
