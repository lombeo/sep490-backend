namespace Api_Project_Prn.DTO
{
    public class ScheduleDefaultDTO
    {
        public int ScheduleTimeInSeconds { get; set; }
        public int SyncBlogTimeInMinutes { get; set; }
        public int NumberBlogSyncPerTimes { get; set; }
        public bool Enabled { get; set; }
    }
}
