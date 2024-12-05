namespace Api_Project_Prn.Infra.Constants
{
    public class PatternConst
    {
        public const string PASSWORD_PATTERN = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z0-9]).{6,}$";
        public const string EMAIL_PATTERN = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$";
    }
}
