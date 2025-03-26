using Sep490_Backend.DTO;

namespace Sep490_Backend.DTO.ConstructionTeam
{
    public class ConstructionTeamSearchDTO : BaseQuery
    {
        public string? TeamName { get; set; }
        public int? TeamManager { get; set; }
    }
} 