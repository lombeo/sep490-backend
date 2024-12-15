namespace Sep490_Backend.Infra.Constants
{
    public class Message
    {
        public static class CommonMessage
        {
            // Common
            public const string NOT_ALLOWED = "Common_403"; // You are not allowed.
            public const string NOT_AUTHEN = "Common_401";
            public const string NOT_FOUND = "Common_404"; // Not found.
            public const string ERROR_HAPPENED = "Common_500"; // An error occurred, please contact the admin or try again later!
            public const string ACTION_SUCCESS = "Common_200"; // Action successfully.
            public const string MISSING_PARAM = "Common_005"; // Missing input parameters. Please check again!
            public const string INVALID_FORMAT = "Common_006"; //Invalid format. Please check again!;
            public const string PERMISSION_REQUIRED = "Common_007"; //You do not have permission to perform this action!
            public const string ACTION_FAIL = "Common_008"; //Internal server error!
        }

        public static class AuthenMessage
        {
            public const string EXIST_EMAIL = "Your email already exists."; //Your email already exists.
            public const string EXIST_USERNAME = "Your username already exists."; //Your username already exists.
            public const string INVALID_EMAIL = "The email you just entered is not in the correct format."; //The email you just entered is not in the correct format.
            public const string INVALID_USERNAME = "Your username cannot contain spaces."; //Your username cannot contain spaces.
            public const string INVALID_PASSWORD = "Your password must contain at least 6 characters, one lowercase letter, one uppercase letter, 1 special character and one number"; //Your password must contain at least 6 characters, one lowercase letter, one uppercase letter, 1 special character and one number
            public const string INVALID_USER = "You need to update your profile!";
            public const string INVALID_CURRENT_PASSWORD = "Your current password is not correct!";
            public const string INVALID_CONFIRM_PASSWORD = "Your new password and confirm password is not match!";
            public const string INVALID_CREDENTIALS = "Your account not registered or wrong information";
            public const string ACCOUNT_NOT_VERIFIED = "Your account not verified!";
            public const string INVALID_TOKEN = "Token outdate or revoked";

        }
    }
}
