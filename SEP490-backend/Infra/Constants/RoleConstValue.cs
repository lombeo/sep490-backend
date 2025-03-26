namespace Sep490_Backend.Infra.Constants
{
    public static class RoleConstValue
    {
        public static readonly HashSet<string> ValidRoles = new HashSet<string>
        {
            "Administrator",
            "User",
            "Construction Employee",
            "Business Employee",
            "Technical Manager",
            "Quality Assurance",
            "Executive Board",
            "Resource Manager",
            "Construction Manager",
            "Team Leader"
        };
        public const string ADMIN = "Administrator";
        public const string USER = "User";
        public const string CONSTRUCTION_EMPLOYEE = "Construction Employee";
        public const string BUSINESS_EMPLOYEE = "Business Employee";
        public const string TECHNICAL_MANAGER = "Technical Manager";
        public const string QUALITY_ASSURANCE = "Quality Assurance";
        public const string EXECUTIVE_BOARD = "Executive Board";
        public const string RESOURCE_MANAGER = "Resource Manager";
        public const string CONSTRUCTION_MANAGER = "Construction Manager";
        public const string TEAM_LEADER = "Team Leader";
    }
}
