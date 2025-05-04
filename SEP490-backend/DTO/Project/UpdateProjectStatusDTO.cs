using Sep490_Backend.Infra.Enums;
using System.ComponentModel.DataAnnotations;

namespace Sep490_Backend.DTO.Project
{
    /// <summary>
    /// DTO for updating a project's status
    /// </summary>
    public class UpdateProjectStatusDTO
    {
        /// <summary>
        /// ID of the project to update
        /// </summary>
        [Required(ErrorMessage = "Project ID is required")]
        public int ProjectId { get; set; }
        
        /// <summary>
        /// Target status for the project
        /// </summary>
        [Required(ErrorMessage = "Target status is required")]
        public ProjectStatusEnum TargetStatus { get; set; }
        
        public string? Notes { get; set; }
    }
} 