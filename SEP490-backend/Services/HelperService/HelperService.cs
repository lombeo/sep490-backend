using System.Security.Cryptography;
using System.Text;

namespace Sep490_Backend.Services.HelperService
{
    public interface IHelperService
    {
        string HashPassword(string password);
        string GenerateStrongPassword(int length = 12);
    }

    public class HelperService : IHelperService
    {
        private static readonly char[] UpperCaseLetters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();
        private static readonly char[] LowerCaseLetters = "abcdefghijklmnopqrstuvwxyz".ToCharArray();
        private static readonly char[] Numbers = "0123456789".ToCharArray();
        private static readonly char[] SpecialChars = "!@#$%^&*()-_=+[]{}|;:',.<>?".ToCharArray();

        public string HashPassword(string password)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(password));

                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        public string GenerateStrongPassword(int length = 12)
        {
            if (length < 12) throw new ApplicationException("Password length should be at least 12 characters for security reasons.");
            var passwordBuilder = new StringBuilder(length);
            passwordBuilder.Append(GenerateRandomChar(UpperCaseLetters));
            passwordBuilder.Append(GenerateRandomChar(LowerCaseLetters));
            passwordBuilder.Append(GenerateRandomChar(Numbers));
            passwordBuilder.Append(GenerateRandomChar(SpecialChars));
            var allChars = new char[UpperCaseLetters.Length + LowerCaseLetters.Length + Numbers.Length + SpecialChars.Length];
            UpperCaseLetters.CopyTo(allChars, 0);
            LowerCaseLetters.CopyTo(allChars, UpperCaseLetters.Length);
            Numbers.CopyTo(allChars, UpperCaseLetters.Length + LowerCaseLetters.Length);
            SpecialChars.CopyTo(allChars, UpperCaseLetters.Length + LowerCaseLetters.Length + Numbers.Length);
            using (var rng = RandomNumberGenerator.Create())
            {
                while (passwordBuilder.Length < length)
                {
                    var randomIndex = RandomNumberGenerator.GetInt32(allChars.Length);
                    passwordBuilder.Append(allChars[randomIndex]);
                }
            }
            var passwordArray = passwordBuilder.ToString().ToCharArray();
            using (var rng = RandomNumberGenerator.Create())
            {
                for (int i = passwordArray.Length - 1; i > 0; i--)
                {
                    var j = RandomNumberGenerator.GetInt32(i + 1);
                    var temp = passwordArray[i];
                    passwordArray[i] = passwordArray[j];
                    passwordArray[j] = temp;
                }
            }
            return new string(passwordArray);
        }

        private static char GenerateRandomChar(char[] charArray)
        {
            var randomByte = RandomNumberGenerator.GetInt32(charArray.Length);
            return charArray[randomByte];
        }
    }
}
