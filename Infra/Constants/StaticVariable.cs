using Api_Project_Prn.DTO;
using Api_Project_Prn.DTO.Configuration;
using Api_Project_Prn.Infra.Entities;

namespace Api_Project_Prn.Infra.Constants
{
    public static class StaticVariable
    {
        public static int TimeToday = 0;
        public static RedisConfigDTO RedisConfig = AppSettings.Get<RedisConfigDTO>("RedisConfiguration");
        public static JwtValidationDTO JwtValidation = AppSettings.Get<JwtValidationDTO>("JwtValidation");

        public static ScheduleDefaultDTO ScheduleDefault = AppSettings.Get<ScheduleDefaultDTO>("ScheduleDefault");

        public static bool IsInitializedUser = false;
        public static IEnumerable<User> UserMemory = new List<User>();
    }
}
