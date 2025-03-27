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
            public const string SEARCH_SUCCESS = "S-SUR-001"; //Search list site survey successfully!
            public const string DELETE_SUCCESS = "S-SUR-002"; //Delete site survey successfully!
            public const string SAVE_SUCCESS = "S-SUR-003"; //Save site survey successfully!
            public const string GET_DETAIL_SUCCESS = "S-SUR-004"; //Get detail site survey successfully!

            //Error
            public const string PROJECT_NOT_FOUND = "E-SUR-001"; //Project not found!
        }

        public static class CustomerMessage
        {
            //Success
            public const string SEARCH_SUCCESS = "S-CUS-001"; //Search list customer successfully!
            public const string DELETE_CUSTOMER_SUCCESS = "S-CUS-002"; //Delete customer successfully!
            public const string CREATE_CUSTOMER_SUCCESS = "S-CUS-003"; //Create customer successfully!
            public const string UPDATE_CUSTOMER_SUCCESS = "S-CUS-004"; //Update customer successfully!
          
            //Error
            public const string CUSTOMER_NOT_FOUND = "E-CUS-001"; //Customer not found.
            public const string CUSTOMER_CODE_DUPLICATE = "E-CUS-002"; //Customer code has already exist in the system.
            public const string CUSTOMER_EMAIL_DUPLICATE = "E-CUS-003"; //Customer email has already exist in the system.
            public const string FAX_CODE_DUPLICATE = "E-CUS-004"; //Fax code has already exist in the system.
            public const string TAX_CODE_DUPLICATE = "E-CUS-005"; //Tax code has already exist in the system.
            public const string BANK_ACCOUNT_DUPLICATE = "E-CUS-006"; //Bank account has already exist in the system.
        }

        public static class ProjectMessage
        {
            //Success
            public const string SEARCH_SUCCESS = "S-PRJ-001"; //Search list project successfully!
            public const string DELETE_SUCCESS = "S-PRJ-002"; //Delete project successfully!
            public const string SAVE_SUCCESS = "S-PRJ-003"; //Save project successfully!
            public const string GET_LIST_STATUS_SUCCESS = "S-PRJ-004"; //Get list status successfully!
            public const string GET_DETAIL_SUCCESS = "S-PRJ-005"; //Get detail project successfully!

            //Error
            public const string INVALID_DATE = "E-PRJ-001"; //The end date cannot be before the start date.
            public const string PROJECT_CODE_EXIST = "E-PRJ-002"; //Project code has already exist!
        }

        public static class ContractMessage
        {
            //Success
            public const string SEARCH_SUCCESS = "S-CTR-001"; //Search list contract successfully!
            public const string DELETE_SUCCESS = "S-CTR-002"; //Delete contract successfully!
            public const string SAVE_SUCCESS = "S-CTR-003"; //Save contract successfully!
            //Error
            public const string CONTRACT_CODE_EXIST = "E-CTR-001"; //Contract code has already exist
        }

        public static class ConstructionPlanMessage
        {
            //Success
            public const string SEARCH_SUCCESS = "S-CP-001"; //Search list construction plan successfully!
            public const string DELETE_SUCCESS = "S-CP-002"; //Delete construction plan successfully!
            public const string CREATE_SUCCESS = "S-CP-003"; //Create construction plan successfully!
            public const string UPDATE_SUCCESS = "S-CP-004"; //Update construction plan successfully!
            public const string GET_DETAIL_SUCCESS = "S-CP-005"; //Get detail construction plan successfully!
            public const string APPROVE_SUCCESS = "S-CP-006"; //Approve construction plan successfully!
            public const string REJECT_SUCCESS = "S-CP-007"; //Reject construction plan successfully!
            public const string IMPORT_SUCCESS = "S-CP-008"; //Import construction plan successfully!
            public const string ASSIGN_TEAM_SUCCESS = "S-CP-009"; //Assign team to construction plan successfully!
            
            //Error
            public const string NOT_FOUND = "E-CP-001"; //Construction plan not found!
            public const string PLAN_NAME_EXIST = "E-CP-002"; //Construction plan name already exists!
            public const string INVALID_PROJECT = "E-CP-003"; //Invalid project!
            public const string INVALID_FILE_FORMAT = "E-CP-004"; //Invalid file format!
            public const string INVALID_FILE_CONTENT = "E-CP-005"; //Invalid file content!
            public const string INVALID_TEAM = "E-CP-006"; //Invalid construction team!
        }

        public static class ResourceRequestMessage
        {
            // Success messages
            public const string SAVE_SUCCESS = "S-RR-001"; // Save resource request successfully!
            public const string DELETE_SUCCESS = "S-RR-002"; // Delete resource request successfully!
            public const string SEARCH_SUCCESS = "S-RR-003"; // Search resource requests successfully!
            public const string SEND_REQUEST_SUCCESS = "S-RR-004"; // Send resource request for approval successfully!
            public const string APPROVE_SUCCESS = "S-RR-005"; // Approve resource request successfully!
            public const string REJECT_SUCCESS = "S-RR-006"; // Reject resource request successfully!
            public const string ADD_INVENTORY_SUCCESS = "S-RR-007"; // Add inventory item successfully!
            public const string UPDATE_INVENTORY_SUCCESS = "S-RR-008"; // Update inventory item successfully!
            public const string DELETE_INVENTORY_SUCCESS = "S-RR-009"; // Delete inventory item successfully!
            
            // Error messages
            public const string REQUEST_NOT_FOUND = "E-RR-001"; // Resource request not found!
            public const string ONLY_CREATOR_CAN_MODIFY = "E-RR-002"; // Only the creator can modify this request!
            public const string ONLY_CREATOR_CAN_DELETE = "E-RR-003"; // Only the creator can delete this request!
            public const string ONLY_CREATOR_CAN_SEND = "E-RR-004"; // Only the creator can send this request!
            public const string ONLY_DRAFT_CAN_BE_UPDATED = "E-RR-005"; // Only draft or rejected requests can be updated!
            public const string ONLY_DRAFT_CAN_BE_DELETED = "E-RR-006"; // Only draft or rejected requests can be deleted!
            public const string ONLY_DRAFT_CAN_BE_SENT = "E-RR-007"; // Only draft or rejected requests can be sent for approval!
            public const string NOT_WAITING_FOR_APPROVAL = "E-RR-008"; // This request is not waiting for approval!
            public const string PROJECT_NOT_FOUND = "E-RR-009"; // Project not found!
            public const string MISSING_RESOURCE_DETAILS = "E-RR-010"; // At least one resource must be specified!
            public const string INVALID_RESOURCE_TYPE = "E-RR-011"; // Resource type must be specified!
            public const string INVALID_QUANTITY = "E-RR-012"; // Quantity must be greater than zero!
            public const string INVALID_REQUEST_DATE = "E-RR-013"; // Request date cannot be in the past!
            public const string SOURCE_PROJECT_NOT_FOUND = "E-RR-014"; // Source project not found!
            public const string DESTINATION_PROJECT_NOT_FOUND = "E-RR-015"; // Destination project not found!
            public const string INVALID_PROJECT_SELECTION = "E-RR-016"; // Source and destination projects cannot be the same!
            public const string INVENTORY_NOT_FOUND = "E-RR-017"; // Resource inventory not found!
            public const string NAME_REQUIRED = "E-RR-018"; // Resource name is required!
            public const string NEGATIVE_QUANTITY = "E-RR-019"; // Quantity cannot be negative!
        }

        public static class MaterialMessage
        {
            // Success messages
            public const string SAVE_SUCCESS = "S-MTL-001"; // Save material successfully!
            public const string DELETE_SUCCESS = "S-MTL-002"; // Delete material successfully!
            public const string SEARCH_SUCCESS = "S-MTL-003"; // Search materials successfully!
            public const string GET_DETAIL_SUCCESS = "S-MTL-004"; // Get material details successfully!
            
            // Error messages
            public const string NOT_FOUND = "E-MTL-001"; // Material not found!
            public const string CODE_EXISTS = "E-MTL-002"; // Material code already exists!
            public const string MATERIAL_IN_USE = "E-MTL-003"; // Cannot delete material because it is in use in construction plans!
            public const string CODE_REQUIRED = "E-MTL-004"; // Material code is required!
            public const string NAME_REQUIRED = "E-MTL-005"; // Material name is required!
        }

        public static class ConstructionTeamMessage
        {
            // Success messages
            public const string SAVE_SUCCESS = "S-CTM-001"; // Save construction team successfully!
            public const string DELETE_SUCCESS = "S-CTM-002"; // Delete construction team successfully!
            public const string SEARCH_SUCCESS = "S-CTM-003"; // Search construction teams successfully!
            public const string GET_DETAIL_SUCCESS = "S-CTM-004"; // Get construction team details successfully!
            
            // Error messages
            public const string NOT_FOUND = "E-CTM-001"; // Construction team not found!
            public const string ONLY_CREATOR_CAN_UPDATE = "E-CTM-002"; // Only the creator of a construction team can update it!
            public const string ONLY_CREATOR_CAN_DELETE = "E-CTM-003"; // Only the creator of a construction team can delete it!
            public const string TEAM_IN_USE = "E-CTM-004"; // Cannot delete team because it is assigned to construction plan items!
            public const string NAME_REQUIRED = "E-CTM-005"; // Construction team name is required!
            public const string DUPLICATE_NAME = "E-CTM-006"; // Construction team name already exists!
        }

        public static class VehicleMessage
        {
            // Success messages
            public const string CREATE_SUCCESS = "S-VEH-001"; // Create vehicle successfully!
            public const string UPDATE_SUCCESS = "S-VEH-002"; // Update vehicle successfully!
            public const string DELETE_SUCCESS = "S-VEH-003"; // Delete vehicle successfully!
            public const string SEARCH_SUCCESS = "S-VEH-004"; // Search vehicles successfully!
            public const string GET_DETAIL_SUCCESS = "S-VEH-005"; // Get vehicle details successfully!
            
            // Error messages
            public const string NOT_FOUND = "E-VEH-001"; // Vehicle not found!
            public const string LICENSE_PLATE_EXISTS = "E-VEH-002"; // License plate already exists!
            public const string VEHICLE_IN_USE = "E-VEH-003"; // Cannot delete vehicle because it is in use in construction plans!
        }

        public static class ActionLogMessage
        {
            // Success messages
            public const string CREATE_SUCCESS = "S-LOG-001"; // Create action log successfully!
            public const string UPDATE_SUCCESS = "S-LOG-002"; // Update action log successfully!
            public const string DELETE_SUCCESS = "S-LOG-003"; // Delete action log successfully!
            public const string SEARCH_SUCCESS = "S-LOG-004"; // Search action logs successfully!
            public const string GET_DETAIL_SUCCESS = "S-LOG-005"; // Get action log details successfully!
            public const string CACHE_INVALIDATED = "S-LOG-006"; // Action log cache invalidated successfully!
            
            // Error messages
            public const string NOT_FOUND = "E-LOG-001"; // Action log not found!
        }
    }
}
