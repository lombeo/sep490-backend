using Sep490_Backend.Infra.Enums;

namespace Sep490_Backend.DTO.ProjectDTO
{
    public class SearchProjectDTO : BaseQueryDTO
    {
        public string? KeyWord { get; set; }
        public int CustomerId { get; set; }
        public ProjectStatusEnum? Status { get; set; }
    }
}
