namespace Sep490_Backend.DTO
{
    public class ScheduleDefaultDTO
    {
        public int ScheduleTimeInSeconds { get; set; }
        public int SyncBlogTimeInMinutes { get; set; }
        public int NumberBlogSyncPerTimes { get; set; }
        public bool Enabled { get; set; }
    }
}
