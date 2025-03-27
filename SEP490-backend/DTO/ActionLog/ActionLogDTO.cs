using Sep490_Backend.Infra.Enums;
using Sep490_Backend.DTO;

namespace Sep490_Backend.DTO.ActionLog
{
    public class ActionLogDTO
    {
        public int Id { get; set; }
        public ActionLogType LogType { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int Creator { get; set; }
        public int Updater { get; set; }
    }

    public class ActionLogCreateDTO
    {
        public ActionLogType LogType { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
    }

    public class ActionLogUpdateDTO
    {
        public ActionLogType LogType { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
    }

    public class ActionLogQuery : BaseQuery
    {
        public ActionLogType? LogType { get; set; }
        public string? SearchTerm { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
    }
} 