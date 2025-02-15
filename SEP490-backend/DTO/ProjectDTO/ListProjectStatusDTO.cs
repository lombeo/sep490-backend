namespace Sep490_Backend.DTO.ProjectDTO
{
    public class ListProjectStatusDTO
    {
        public int ReceiveRequest { get; set; }
        public int Planning { get; set; }
        public int InProgress { get; set; }
        public int Completed { get; set; }
        public int Paused { get; set; }
        public int Closed { get; set; }
        public int Total { get; set; }
    }
}
