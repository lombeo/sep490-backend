using DocumentFormat.OpenXml.Drawing.Charts;
using Humanizer;
using NPOI.SS.Util;
using NuGet.Configuration;

namespace Sep490_Backend.Infra.Constants
{
    public class Message
    {
        public static class CommonMessage
        {
            // Common
            public const string NOT_ALLOWED = "E-CM-403"; // You are not allowed.
            public const string NOT_AUTHEN = "E-CM-401";
            public const string NOT_FOUND = "E-CM-404"; // Not found.
            public const string ERROR_HAPPENED = "E-CM-500"; // An error occurred, please contact the admin or try again later!
            public const string ACTION_SUCCESS = "E-CM-200"; // Action successfully.
            public const string MISSING_PARAM = "E-CM-005"; // Missing input parameters. Please check again!
            public const string INVALID_FORMAT = "E-CM-006"; //Invalid format. Please check again!;
            public const string PERMISSION_REQUIRED = "E-CM-007"; //You do not have permission to perform this action!
            public const string ACTION_FAIL = "E-CM-008"; //Internal server error!
        }

        public static class AuthenMessage
        {
            //Error
            public const string EXIST_EMAIL = "E-AUTHEN-001"; //Your email already exists.
            public const string EXIST_USERNAME = "E-AUTHEN-002"; //Your username already exists.
            public const string INVALID_EMAIL = "E-AUTHEN-003"; //The email you just entered is not in the correct forgitmat.
            public const string INVALID_USERNAME = "E-AUTHEN-004"; //Your username cannot contain spaces.
            public const string INVALID_PASSWORD = "E-AUTHEN-005"; //Your password is invalid.
            public const string INVALID_CURRENT_PASSWORD = "E-AUTHEN-006"; //Your current password is not correct!
            public const string INVALID_CONFIRM_PASSWORD = "E-AUTHEN-007"; //Your new password and confirm password is not match!
            public const string INVALID_CREDENTIALS = "E-AUTHEN-008"; //Your account not registered or wrong information.
            public const string ACCOUNT_NOT_VERIFIED = "E-AUTHEN-009"; //Your account not verified!
            public const string INVALID_OTP = "E-AUTHEN-010"; //Your OTP is incorrect, double check it in your email.
            public const string INVALID_TOKEN = "E-AUTHEN-011"; //Token outdate or revoked.
            public const string OTP_REQUIRED = "E-AUTHEN-012"; //This action require OTP.

            //Success
            public const string SIGNUP_SUCCESS = "S-AUTHEN-001"; //Account registration successful, please check your email to receive the OTP code used to verify your account.
            public const string VERIFY_OTP_SUCCESS = "S-AUTHEN-002"; //Successfully verified OTP code.
            public const string FORGET_PASSWORD_SUCCESS = "S-AUTHEN-003"; //Password reset request successful, please check your email to receive OTP code.
            public const string CHANGE_PASSWORD_SUCCESS = "S-AUTHEN-004"; //Password changed successfully!
            public const string SIGN_IN_SUCCESS = "S-AUTHEN-005"; //Log in successfully.
            public const string REFRESH_TOKEN_SUCCESS = "S-AUTHEN-006"; //Refresh token successfully.
        }

        public static class AdminMessage
        {
            //Success
            public const string SEARCH_SUCCESS = "S-ADMIN-001"; //Search list user successfully!
            public const string DELETE_USER_SUCCESS = "S-ADMIN-002"; //Delete user successfully!
            public const string CREATE_USER_SUCCESS = "S-ADMIN-003"; //Create user successfully!
            public const string UPDATE_USER_SUCCESS = "S-ADMIN-004"; //Update user successfully!

            //Error
            public const string DELETE_USER_ERROR = "E-ADMIN-001"; //An error occurred while deleting a user, you cannot delete your own account, or the person you want to delete has equal or higher authority than you.
            public const string CREATE_USER_ERROR = "E-ADMIN-002"; //User already exist, username and email already exist
            public const string INVALID_ROLE = "E-ADMIN-003"; //The role does not exist or you do not have permission to set this permission.
        }
    }
}
