namespace Sep490_Backend.Infra.Entities
{
    public class Project : CommonEntity
    {
        public int Id { get; set; }
        public string ProjectCode { get; set; }
        public string ProjectName { get; set; }
        public int CustomerId { get; set; }
        public int ConstructType { get; set; }
        public string? Location { get; set; }
        public string? Area { get; set; }
        public DateTime Timeline { get; set; }
        public string? Purpose { get; set; }
        public string? TechnicalReqs { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal Budget { get; set; }
        public int Status { get; set; }
        public string? Attachment { get; set; }
        public string? Description { get; set; }

    }
}
