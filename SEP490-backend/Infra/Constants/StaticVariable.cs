using Sep490_Backend.DTO;
using Sep490_Backend.DTO.Configuration;
using Sep490_Backend.Infra.Entities;

namespace Sep490_Backend.Infra.Constants
{
    public static class StaticVariable
    {
        public static int TimeToday = 0;
        public static RedisConfigDTO RedisConfig = AppSettings.Get<RedisConfigDTO>("RedisConfiguration");
        public static JwtValidationDTO JwtValidation = new JwtValidationDTO
        {
            ValidIssuer = Environment.GetEnvironmentVariable("JWT_VALID_ISSUER") ?? "Lombeo",
            ValidAudience = Environment.GetEnvironmentVariable("JWT_VALID_AUDIENCE") ?? "Lombeo",
            CertificatePath = Environment.GetEnvironmentVariable("JWT_CERTIFICATE_PATH"),
            CertificatePassword = Environment.GetEnvironmentVariable("JWT_CERTIFICATE_PASSWORD")
        };

        public static ScheduleDefaultDTO ScheduleDefault = AppSettings.Get<ScheduleDefaultDTO>("ScheduleDefault");

        public static bool IsInitializedUser = false;
        public static IEnumerable<User> UserMemory = new List<User>();
        public static bool IsInitializedUserProfile = false;
        public static IEnumerable<UserProfile> UserProfileMemory = new List<UserProfile>();
        public static readonly TimeSpan RefreshTokenExpiryDuration = TimeSpan.FromDays(7);
    }
}
