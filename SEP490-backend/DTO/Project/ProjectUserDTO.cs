using Sep490_Backend.Infra.Entities;

namespace Sep490_Backend.DTO.Project
{
    public class ProjectUserDTO
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int ProjectId { get; set; }
        public bool IsCreator { get; set; }
        public bool Deleted { get; set; }
    }
} 