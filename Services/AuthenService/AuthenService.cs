using Sep490_Backend.DTO.AuthenDTO;
using Sep490_Backend.Infra.Constants;
using System.Text.RegularExpressions;
using System.Text;
using Sep490_Backend.Infra;
using Sep490_Backend.Infra.Entities;
using System.Security.Cryptography;
using Sep490_Backend.Services.EmailService;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;

namespace Sep490_Backend.Services.AuthenService
{
    public interface IAuthenService
    {
        Task<bool> SignUp(SignUpDTO model);
        Task<bool> VerifyOTP(int userId, string otpCode);
        Task<bool> ChangePassword(ChangePasswordDTO model);
        Task<bool> ForgetPassword(string email);
        Task<ReturnSignInDTO> SignIn(SignInDTO model);
        Task<string> Refresh(string refreshToken);
        Task<bool> SignInWithGoogle(string idToken);
    }

    public class AuthenService : IAuthenService
    {
        private readonly BackendContext _context;
        private readonly IEmailService _email;

        private static readonly char[] UpperCaseLetters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();
        private static readonly char[] LowerCaseLetters = "abcdefghijklmnopqrstuvwxyz".ToCharArray();
        private static readonly char[] Numbers = "0123456789".ToCharArray();
        private static readonly char[] SpecialChars = "!@#$%^&*()-_=+[]{}|;:'\",.<>?/".ToCharArray();

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
            await _context.SaveChangesAsync();

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

        private async void ValidateSignUp(SignUpDTO model)
        {
            var data = await _context.Users.Where(t => t.Deleted == false).ToListAsync();

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

        public async Task<bool> ChangePassword(ChangePasswordDTO model)
        {
            var user = await _context.Users.FirstOrDefaultAsync(t => t.Id == model.UserId);
            if (user == null)
            {
                throw new ApplicationException(Message.CommonMessage.NOT_FOUND);
            }
            if(string.Compare(HashPassword(model.CurrentPassword), user.PasswordHash) != 0)
            {
                if (string.Compare(HashPassword(model.CurrentPassword), user.StrongPassword) != 0)
                {
                    throw new ApplicationException(Message.AuthenMessage.INVALID_CURRENT_PASSWORD);
                }
            }
            if(string.Compare(model.NewPassword, model.ConfirmPassword) != 0)
            {
                throw new ApplicationException(Message.AuthenMessage.INVALID_CONFIRM_PASSWORD);
            }
            user.PasswordHash = HashPassword(model.NewPassword);
            user.StrongPassword = null;
            _context.Update(user);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> ForgetPassword(string email)
        {
            var user = await _context.Users.FirstOrDefaultAsync(t => t.Email == email && t.IsVerify == true);
            if(user == null)
            {
                throw new ApplicationException(Message.CommonMessage.NOT_FOUND);
            }
            var newPassword = GenerateStrongPassword();
            user.StrongPassword = HashPassword(newPassword);
            _context.Update(user);
            await _context.SaveChangesAsync();

            var emailBody = $@"<!DOCTYPE html>
                                <html lang=""vi"">
                                <head>
                                    <meta charset=""UTF-8"">
                                    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
                                    <title>Mật khẩu mới của bạn</title>
                                </head>
                                <body style=""margin: 0; padding: 0; font-family: Arial, sans-serif; background-color: #f4f4f4;"">
                                    <table border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""max-width: 600px; margin: 0 auto; background-color: #ffffff;"">
                                        <tr>
                                            <td style=""padding: 40px 30px; background-color: #4CAF50; text-align: center;"">
                                                <h1 style=""color: #ffffff; font-size: 28px; margin: 0;"">Mật khẩu mới của bạn</h1>
                                            </td>
                                        </tr>
                                        <tr>
                                            <td style=""padding: 40px 30px;"">
                                                <p style=""color: #333333; font-size: 16px; line-height: 24px; margin: 0 0 20px;"">Kính gửi quý khách,</p>
                                                <p style=""color: #333333; font-size: 16px; line-height: 24px; margin: 0 0 20px;"">Chúng tôi đã nhận được yêu cầu đặt lại mật khẩu cho tài khoản của bạn. Mật khẩu mới của bạn là:</p>
                                                <p style=""color: #333333; font-size: 24px; font-weight: bold; text-align: center; margin: 0 0 20px; padding: 20px; background-color: #f8f8f8; border-radius: 5px; letter-spacing: 2px;"">{newPassword}</p>
                                                <p style=""color: #333333; font-size: 16px; line-height: 24px; margin: 0 0 20px;"">Vui lòng đăng nhập và thay đổi mật khẩu này ngay lập tức để đảm bảo an toàn cho tài khoản của bạn.</p>
                                                <p style=""color: #333333; font-size: 16px; line-height: 24px; margin: 0 0 20px;"">Nếu bạn không yêu cầu đặt lại mật khẩu, vui lòng liên hệ ngay với đội ngũ hỗ trợ của chúng tôi.</p>
                                                <a href=""#"" style=""display: inline-block; padding: 12px 24px; background-color: #4CAF50; color: #ffffff; text-decoration: none; font-weight: bold; border-radius: 5px;"">Đăng nhập ngay</a>
                                            </td>
                                        </tr>
                                        <tr>
                                            <td style=""padding: 30px; background-color: #f8f8f8; text-align: center;"">
                                                <p style=""color: #888888; font-size: 14px; margin: 0 0 10px;"">Đây là email tự động, vui lòng không trả lời.</p>
                                                <p style=""color: #888888; font-size: 14px; margin: 0;"">© 2023 Tên Công Ty Của Bạn. Bảo lưu mọi quyền.</p>
                                            </td>
                                        </tr>
                                    </table>
                                </body>
                                </html>";

            await _email.SendEmailAsync(email, "Reset password request", emailBody);

            return true;
        }

        public static string GenerateStrongPassword(int length = 16)
        {
            if (length < 12) throw new ArgumentException("Password length should be at least 12 characters for security reasons.");

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

        public async Task<ReturnSignInDTO> SignIn(SignInDTO model)
        {
            //Validate
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == model.Username || u.Email == model.Username);

            //Check username + password
            if (user == null || (HashPassword(model.Password) != user.PasswordHash))
            {
                throw new ApplicationException(Message.AuthenMessage.INVALID_CREDENTIALS);
            } 
            //Check vertify
            if (!user.IsVerify)
            {
                throw new ApplicationException(Message.AuthenMessage.ACCOUNT_NOT_VERIFIED);
            }

            //Tạo accesstoken + refresh token lưu trong db
            var accessToken = GenerateAccessToken(user);
            var refreshToken = GenerateRefreshToken();

            var existingToken = await _context.RefreshTokens.FirstOrDefaultAsync(rt => rt.UserId == user.Id);
            var expiryDate = DateTime.UtcNow.Add(StaticVariable.RefreshTokenExpiryDuration);
            if (existingToken !=null)
            {
                existingToken.Token = refreshToken;
                existingToken.ExpiryDate = expiryDate;
            }
            else
            {
                var newToken = new RefreshToken
                {
                    UserId = user.Id,
                    Token = refreshToken,
                    ExpiryDate = expiryDate,
                    IsRevoked = false
                };
                await _context.RefreshTokens.AddAsync(newToken);
            }
            await _context.SaveChangesAsync();

            return new ReturnSignInDTO
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                Email = user.Email,
                Role = user.Role,
                Username = user.Username
            };
        }

        private static string GenerateAccessToken(User user)
        {
            try
            {
                var claims = new[]
                {
                    new Claim(JwtRegisteredClaimNames.Sub,  user.Username),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
                };
                X509Certificate2 cert = new X509Certificate2(StaticVariable.JwtValidation.CertificatePath, StaticVariable.JwtValidation.CertificatePassword);
                var key = new X509SecurityKey(cert);
                var creds = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);

                var token = new JwtSecurityToken(
                    issuer: StaticVariable.JwtValidation.ValidIssuer,
                    audience: StaticVariable.JwtValidation.ValidAudience,
                    claims: claims,
                    expires: DateTime.UtcNow.AddMinutes(30),
                    signingCredentials: creds
                );

                var tokenHandler = new JwtSecurityTokenHandler();
                var jwtToken = tokenHandler.WriteToken(token);
                return jwtToken;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        private static string GenerateRefreshToken()
        {
            byte[] randomNumber = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }

        public async Task<string> Refresh(string refreshToken)
        {
            var existingRefreshToken = await _context.RefreshTokens.FirstOrDefaultAsync(rt => rt.Token == refreshToken && rt.ExpiryDate > DateTime.UtcNow && !rt.IsRevoked);
            if (existingRefreshToken == null)
            {
                throw new ApplicationException(Message.AuthenMessage.INVALID_TOKEN);
            }
            var userId = existingRefreshToken.UserId;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                throw new ApplicationException(Message.AuthenMessage.INVALID_USER);
            }
            return GenerateAccessToken(user);
        }

        public Task<bool> SignInWithGoogle(string idToken)
        {
            throw new NotImplementedException();
        }
    }
}
