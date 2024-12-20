namespace Sep490_Backend.Infra.Entities
{
    public class CommonEntity
    {
        public DateTime? UpdatedAt { get; set; }
        public DateTime? CreatedAt { get; set; }
        public bool Deleted { get; set; } = false;
    }
}
