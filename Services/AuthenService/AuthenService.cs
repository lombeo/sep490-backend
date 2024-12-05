using Sep490_Backend.DTO.AuthenDTO;
using Sep490_Backend.Infra.Constants;
using System.Text.RegularExpressions;
using System.Text;
using Sep490_Backend.Infra;
using Sep490_Backend.Infra.Entities;
using System.Security.Cryptography;

namespace Sep490_Backend.Services.AuthenService
{
    public interface IAuthenService
    {
        Task<bool> SignUp(SignUpDTO model);
    }

    public class AuthenService : IAuthenService
    {
        private readonly BackendContext _context;

        public AuthenService(BackendContext context)
        {
            _context = context;
        }

        public async Task<bool> SignUp(SignUpDTO model)
        {
            ValidateSignUp(model);

            var password = HashPassword(model.Password);

            var account = new User
            {
                Username = model.Username,
                Email = model.Email,
                PasswordHash = password,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Role = "User"
            };


            await _context.Users.AddAsync(account);

            await _context.SaveChangesAsync();

            return true;
        }

        private void ValidateSignUp(SignUpDTO model)
        {
            var data = StaticVariable.UserMemory.ToList();

            if (data.Any(t => t.Email.ToLower().Equals(model.Email.ToLower())))
            {
                throw new ApplicationException(Message.AuthenMessage.EXIST_EMAIL);
            }

            if (data.Any(t => t.Username.ToLower().Equals(model.Username.ToLower())))
            {
                throw new ApplicationException(Message.AuthenMessage.EXIST_USERNAME);
            }

            if (model.Username.Contains(" "))
            {
                throw new ApplicationException(Message.AuthenMessage.INVALID_USERNAME);
            }

            if (!Regex.IsMatch(model.Email, PatternConst.EMAIL_PATTERN))
            {
                throw new ApplicationException(Message.AuthenMessage.INVALID_EMAIL);
            }

            if (!Regex.IsMatch(model.Password, PatternConst.PASSWORD_PATTERN))
            {
                throw new ApplicationException(Message.AuthenMessage.INVALID_PASSWORD);
            }
        }

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
    }
}
