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
        public const string CONTRACT_BY_PROJECT_CACHE_KEY = "CONTRACT:PROJECT:{0}"; // Pattern: CONTRACT:PROJECT:projectId
        public const string CONTRACT_BY_USER_CACHE_KEY = "CONTRACT:USER:{0}"; // Pattern: CONTRACT:USER:userId
        public const string CONTRACT_DETAIL_BY_USER_CACHE_KEY = "CONTRACT_DETAIL:USER:{0}"; // Pattern: CONTRACT_DETAIL:USER:userId
        public const string MATERIAL_CACHE_KEY = "MATERIAL";
        public const string CONSTRUCTION_TEAM_CACHE_KEY = "CONSTRUCTION_TEAM";
        public const string CONSTRUCTION_PLAN_CACHE_KEY = "CONSTRUCTION_PLAN"; // Cache key for construction plans
        public const string CONSTRUCTION_PLAN_BY_USER_CACHE_KEY = "CONSTRUCTION_PLAN:USER:{0}"; // Pattern: CONSTRUCTION_PLAN:USER:userId
        public const string ALLOCATION_REQ_CACHE_KEY = "RESOURCE_ALLOCATION_REQ:{0}"; // Pattern: RESOURCE_ALLOCATION_REQ:reqId
        public const string ALLOCATION_REQ_BY_PROJECT_CACHE_KEY = "RESOURCE_ALLOCATION_REQ:PROJECT:{0}"; // Pattern: RESOURCE_ALLOCATION_REQ:PROJECT:projectId
        public const string MOBILIZATION_REQ_CACHE_KEY = "RESOURCE_MOBILIZATION_REQ:{0}"; // Pattern: RESOURCE_MOBILIZATION_REQ:reqId
        public const string MOBILIZATION_REQ_BY_PROJECT_CACHE_KEY = "RESOURCE_MOBILIZATION_REQ:PROJECT:{0}"; // Pattern: RESOURCE_MOBILIZATION_REQ:PROJECT:projectId
        public const string RESOURCE_INVENTORY_CACHE_KEY = "RESOURCE_INVENTORY"; // Cache key for all inventory resources
        public const string RESOURCE_INVENTORY_BY_TYPE_CACHE_KEY = "RESOURCE_INVENTORY:TYPE:{0}"; // Pattern: RESOURCE_INVENTORY:TYPE:resourceType
        public const string RESOURCE_INVENTORY_BY_ID_CACHE_KEY = "RESOURCE_INVENTORY:{0}"; // Pattern: RESOURCE_INVENTORY:resourceId
        public const string MOBILIZATION_REQS_LIST_CACHE_KEY = "RESOURCE_MOBILIZATION_REQS:LIST"; // Cache key for mobilization request lists
        public const string MOBILIZATION_REQS_BY_PROJECT_LIST_CACHE_KEY = "RESOURCE_MOBILIZATION_REQS:PROJECT:{0}:LIST"; // Pattern: RESOURCE_MOBILIZATION_REQS:PROJECT:projectId:LIST
        public const string MOBILIZATION_REQS_BY_STATUS_LIST_CACHE_KEY = "RESOURCE_MOBILIZATION_REQS:STATUS:{0}:LIST"; // Pattern: RESOURCE_MOBILIZATION_REQS:STATUS:status:LIST
        public const string ALLOCATION_REQS_LIST_CACHE_KEY = "RESOURCE_ALLOCATION_REQS:LIST"; // Cache key for allocation request lists
        public const string ALLOCATION_REQS_BY_FROM_PROJECT_LIST_CACHE_KEY = "RESOURCE_ALLOCATION_REQS:FROM:{0}:LIST"; // Pattern: RESOURCE_ALLOCATION_REQS:FROM:projectId:LIST
        public const string ALLOCATION_REQS_BY_TO_PROJECT_LIST_CACHE_KEY = "RESOURCE_ALLOCATION_REQS:TO:{0}:LIST"; // Pattern: RESOURCE_ALLOCATION_REQS:TO:projectId:LIST
        public const string ALLOCATION_REQS_BY_STATUS_LIST_CACHE_KEY = "RESOURCE_ALLOCATION_REQS:STATUS:{0}:LIST"; // Pattern: RESOURCE_ALLOCATION_REQS:STATUS:status:LIST
        public const string VEHICLE_CACHE_KEY = "VEHICLE"; // Cache key for all vehicles
        public const string VEHICLE_BY_ID_CACHE_KEY = "VEHICLE:{0}"; // Pattern: VEHICLE:vehicleId
        public const string VEHICLE_SEARCH_CACHE_KEY = "VEHICLE:SEARCH"; // Cache key for vehicle search results
        
        // Action Log Cache Keys
        public const string ACTION_LOG_ALL_CACHE_KEY = "ACTION_LOG:ALL"; // Cache key for all action logs
        public const string ACTION_LOG_BY_ID_CACHE_KEY = "ACTION_LOG:{0}"; // Pattern: ACTION_LOG:logId
    }
}
