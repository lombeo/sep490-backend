using Microsoft.AspNetCore.Http;
using Sep490_Backend.Infra.Enums;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Sep490_Backend.DTO.Project
{
    public class SaveProjectDTO
    {
        public int Id { get; set; }
        
        [Required(ErrorMessage = "Project code is required")]
        public string ProjectCode { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Project name is required")]
        public string ProjectName { get; set; } = string.Empty;
        
        public int CustomerId { get; set; }
        
        [Required(ErrorMessage = "Construction type is required")]
        public string ConstructType { get; set; } = string.Empty;
        
        public string? Location { get; set; }
        public string? Area { get; set; }
        public string? Purpose { get; set; }
        public string? TechnicalReqs { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal Budget { get; set; }
        public List<IFormFile>? Attachments { get; set; }
        public string? Description { get; set; }
        public List<int> ViewerUserIds { get; set; } = new List<int>();
    }
}
