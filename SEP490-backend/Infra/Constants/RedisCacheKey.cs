namespace Sep490_Backend.Infra.Constants
{
    public class RedisCacheKey
    {
        public const string OTP_CACHE_KEY = "OTP:{0}:{1}";
        public const string SITE_SURVEY_CACHE_KEY = "SITE_SURVEY";
        public const string CUSTOMER_CACHE_KEY = "CUSTOMER";
        public const string PROJECT_CACHE_KEY = "PROJECT";
        public const string PROJECT_USER_CACHE_KEY = "PROJECT_USER";
        public const string PROJECT_BY_USER_CACHE_KEY = "PROJECT:USER:{0}"; // Pattern: PROJECT:USER:userId
        public const string CONTRACT_CACHE_KEY = "CONTRACT";
        public const string CONTRACT_DETAIL_CACHE_KEY = "CONTRACT_DETAIL";
        public const string MATERIAL_CACHE_KEY = "MATERIAL";
    }
}
