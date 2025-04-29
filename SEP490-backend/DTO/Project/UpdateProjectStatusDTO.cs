using Sep490_Backend.Infra.Enums;
using System.ComponentModel.DataAnnotations;

namespace Sep490_Backend.DTO.Project
{
    public class UpdateProjectStatusDTO
    {
        [Required(ErrorMessage = "Project ID is required")]
        public int ProjectId { get; set; }
        
        [Required(ErrorMessage = "Target status is required")]
        public ProjectStatusEnum TargetStatus { get; set; }
        
        public string? Notes { get; set; }
    }
} 