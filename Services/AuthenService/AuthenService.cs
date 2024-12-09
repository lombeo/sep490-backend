using Sep490_Backend.DTO.AuthenDTO;
using Sep490_Backend.Infra.Constants;
using System.Text.RegularExpressions;
using System.Text;
using Sep490_Backend.Infra;
using Sep490_Backend.Infra.Entities;
using System.Security.Cryptography;
using Sep490_Backend.Services.EmailService;
using Microsoft.EntityFrameworkCore;

namespace Sep490_Backend.Services.AuthenService
{
    public interface IAuthenService
    {
        Task<bool> SignUp(SignUpDTO model);
        Task<bool> VerifyOTP(int userId, string otpCode);
    }

    public class AuthenService : IAuthenService
    {
        private readonly BackendContext _context;
        private readonly IEmailService _email;

        public AuthenService(BackendContext context, IEmailService email)
        {
            _context = context;
            _email = email;
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
                Role = "User",
                IsVerify = false
            };

            await _context.AddAsync(account);

            var otpCode = GenerateOTP();

            var otp = new OTP
            {
                UserId = account.Id,
                Code = otpCode,
                ExpiryTime = DateTime.UtcNow.AddMinutes(10)
            };
            if(await _context.OTPs.FirstOrDefaultAsync(t => t.UserId == account.Id) != null)
            {
                _context.Update(otp);
            }
            else
            {
                await _context.AddAsync(otp);
            }
            await _context.SaveChangesAsync();

            var emailBody = $@"
                            <!DOCTYPE html>
                            <html lang='en'>
                            <head>
                                <meta charset='UTF-8'>
                                <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                                <title>Your OTP Code</title>
                            </head>
                            <body style='margin: 0; padding: 0; font-family: Arial, sans-serif; background-color: #f4f4f4;'>
                                <table border='0' cellpadding='0' cellspacing='0' width='100%' style='max-width: 600px; margin: 0 auto; background-color: #ffffff;'>
                                    <tr>
                                        <td style='padding: 40px 30px; background-color: #3498db; text-align: center;'>
                                            <h1 style='color: #ffffff; font-size: 28px; margin: 0;'>Your OTP Code</h1>
                                        </td>
                                    </tr>
                                    <tr>
                                        <td style='padding: 40px 30px;'>
                                            <p style='color: #333333; font-size: 16px; line-height: 24px; margin: 0 0 20px;'>Dear {account.Username},</p>
                                            <p style='color: #333333; font-size: 16px; line-height: 24px; margin: 0 0 20px;'>Your One-Time Password (OTP) for account verification is:</p>
                                            <p style='color: #333333; font-size: 36px; font-weight: bold; text-align: center; margin: 0 0 20px; padding: 20px; background-color: #f8f8f8; border-radius: 5px;'>{otpCode}</p>
                                            <p style='color: #333333; font-size: 16px; line-height: 24px; margin: 0 0 20px;'>This code will expire in 10 minutes. Please do not share this code with anyone.</p>
                                            <p style='color: #333333; font-size: 16px; line-height: 24px; margin: 0 0 20px;'>If you didn't request this code, please ignore this email or contact our support team if you have any concerns.</p>
                                        </td>
                                    </tr>
                                    <tr>
                                        <td style='padding: 30px; background-color: #f8f8f8; text-align: center;'>
                                            <p style='color: #888888; font-size: 14px; margin: 0 0 10px;'>This is an automated message, please do not reply.</p>
                                            <p style='color: #888888; font-size: 14px; margin: 0;'>© 2023 Your Company Name. All rights reserved.</p>
                                        </td>
                                    </tr>
                                </table>
                            </body>
                            </html>";

            // Gửi email
            await _email.SendEmailAsync(account.Email, "Verify your account", emailBody);

            return true;
        }

        private string GenerateOTP(int length = 6)
        {
            byte[] randomNumber = new byte[length];

            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomNumber);
            }

            var otpCode = string.Join("", randomNumber.Select(b => (b % 10).ToString()));

            return otpCode.Substring(0, length);
        }

        public async Task<bool> VerifyOTP(int userId, string otpCode)
        {
            var otp = await _context.OTPs
                .Where(o => o.UserId == userId && o.Code == otpCode)
                .OrderByDescending(o => o.ExpiryTime)
                .FirstOrDefaultAsync();

            if (otp == null)
            {
                return false;
            }

            if (otp.ExpiryTime < DateTime.UtcNow)
            {
                return false;
            }

            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                user.IsVerify = true;
                _context.Users.Update(user);
                await _context.SaveChangesAsync();
            }

            _context.OTPs.Remove(otp);
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
