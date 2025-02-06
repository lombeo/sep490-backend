namespace Sep490_Backend.Infra.Entities
{
    public class CommonEntity
    {
        public DateTime? UpdatedAt { get; set; }
        public DateTime? CreatedAt { get; set; }
        public bool Deleted { get; set; } = false;
        public int Creator { get; set; }
        public int Updater { get; set; }
    }
}
