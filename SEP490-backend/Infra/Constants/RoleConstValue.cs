namespace Sep490_Backend.Infra.Constants
{
    public static class RoleConstValue
    {
        public static readonly HashSet<string> ValidRoles = new HashSet<string>
        {
            "Administrator",
            "User",
        };
        public const string ADMIN = "Administrator";
        public const string USER = "User";
    }
}
