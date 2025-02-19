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
            public const string ACTION_SUCCESS = "S-CM-200"; // Action successfully.
            public const string MISSING_PARAM = "E-CM-005"; // Missing input parameters. Please check again!
            public const string INVALID_FORMAT = "E-CM-006"; //Invalid format. Please check again!;
            public const string PERMISSION_REQUIRED = "E-CM-007"; //You do not have permission to perform this action!
            public const string ACTION_FAIL = "E-CM-008"; //Internal server error!
            public const string VALIDATE_ERROR = "E-CM-009"; //One or more validation errors occurred.
        }

        public static class AuthenMessage
        {
            //Error
            public const string EXIST_EMAIL = "E-AUTHEN-001"; //Your email already exists.
            public const string EXIST_USERNAME = "E-AUTHEN-002"; //Your username already exists.
            public const string INVALID_EMAIL = "E-AUTHEN-003"; //The email you just entered is not in the correct format.
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
            public const string GET_USER_DETAIL_SUCCESS = "S-AUTHEN-007"; //Get user detail successfully.
            public const string UPDATE_PROFILE_SUCCESS = "S-AUTHEN-008"; //Update profile successfully.
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

        public static class SiteSurveyMessage
        {
            //Success
            public const string SEARCH_SUCCESS = "S-SURVEY-001"; //Search list site survey successfully!
            public const string DELETE_SUCCESS = "S-SURVEY-002";
            public const string SAVE_SUCCESS = "S-SURVEY-003";
            public const string GET_DETAIL_SUCCESS = "S-SURVEY-004";

            //Error
            public const string PROJECT_NOT_FOUND = "E-SURVEY-001";
        }

        public static class CustomerMessage
        {
            //Success
            public const string SEARCH_SUCCESS = "S-CUSTOMER-001"; //Search list customer successfully!
            public const string DELETE_CUSTOMER_SUCCESS = "S-CUSTOMER-002"; //Delete customer successfully!
            public const string CREATE_CUSTOMER_SUCCESS = "S-CUSTOMER-003"; //Create customer successfully!
            public const string UPDATE_CUSTOMER_SUCCESS = "S-CUSTOMER-004"; //Update customer successfully!
            //Error
            public const string CUSTOMER_NOT_FOUND = "E-CUSTOMER-001"; //Customer not found.
            public const string CUSTOMER_CODE_DUPLICATE = "E-CUSTOMER-002"; //Customer code can't be duplicated
            public const string CUSTOMER_EMAIL_DUPLICATE = "E-CUSTOMER-003"; //Customer email can't be duplicated
            public const string FAX_CODE_DUPLICATE = "E-CUSTOMER-004";
            public const string TAX_CODE_DUPLICATE = "E-CUSTOMER-005";
            public const string BANK_ACCOUNT_DUPLICATE = "E-CUSTOMER-006";
        }

        public static class ProjectMessage
        {
            //Success
            public const string SEARCH_SUCCESS = "S-PROJECT-001"; //Search list project successfully!
            public const string DELETE_SUCCESS = "S-PROJECT-002"; //Delete project successfully!
            public const string SAVE_SUCCESS = "S-PROJECT-003"; //Save project successfully!
            public const string GET_LIST_STATUS_SUCCESS = "S-PROJECT-004"; //Get list status successfully!
            public const string GET_DETAIL_SUCCESS = "S-PROJECT-005";

            //Error
            public const string INVALID_DATE = "E-PROJECT-001"; //The end date cannot be before the start date.
            public const string PROJECT_CODE_EXIST = "E-PROJECT-002"; //Project code has already exist!
        }

        public static class ContractMessage
        {
            //Success
            public const string SEARCH_SUCCESS = "S-CONTRACT-001"; //Search list contract successfully!
            public const string DELETE_SUCCESS = "S-CONTRACT-002"; //Delete contract successfully!
            public const string SAVE_SUCCESS = "S-CONTRACT-003"; //Save contract successfully!
            //Error
            public const string CONTRACT_CODE_EXIST = "E-CONTRACT-001"; //Contract code has already exist
        }
    }
}
