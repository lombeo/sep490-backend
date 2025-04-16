using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace Sep490_Backend.DTO.ConstructionTeam
{
    public class ConstructionTeamSaveDTO
    {
        public int? Id { get; set; }
        
        [Required(ErrorMessage = "Team name is required")]
        public string TeamName { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Team manager ID is required")]
        public int TeamManager { get; set; }
        
        public string? Description { get; set; }

        /// <summary>
        /// Collection of user IDs to be added as team members
        /// </summary>
        public List<int> TeamMemberIds { get; set; } = new List<int>();
    }
} 