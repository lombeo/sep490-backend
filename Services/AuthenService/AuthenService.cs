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
using Sep490_Backend.DTO;
using Sep490_Backend.Infra.Enums;
using Sep490_Backend.Services.CacheService;

namespace Sep490_Backend.Services.AuthenService
{
    public interface IAuthenService
    {
        void InitUserMemory();
        void UpdateUserMemory(int userId);
        void TriggerUpdateUserMemory(int userId);
        Task<bool> SignUp(SignUpDTO model);
        Task<bool> VerifyOTP(VerifyOtpDTO model);
        Task<bool> ChangePassword(ChangePasswordDTO model);
        Task<int> ForgetPassword(string email);
        Task<ReturnSignInDTO> SignIn(SignInDTO model);
        Task<string> Refresh(string refreshToken);
        Task<bool> SignInWithGoogle(string idToken);
    }

    public class AuthenService : IAuthenService
    {
        private readonly BackendContext _context;
        private readonly IEmailService _email;
        private readonly IPubSubService _pubSubService;
        private readonly ILogger<AuthenService> _logger;

        public AuthenService(BackendContext context, IEmailService email, IPubSubService pubSubService, ILogger<AuthenService> logger)
        {
            _context = context;
            _email = email;
            _pubSubService = pubSubService;
            _logger = logger;
        }

        public void InitUserMemory()
        {
            if (!StaticVariable.IsInitializedUser)
            {
                _logger.LogError($"InitUserMemory Started: {DateTime.UtcNow}");

                var data = _context.Users.AsNoTracking().Where(t => !t.Deleted)
                                            .OrderByDescending(t => t.UpdatedAt).ToList();
                StaticVariable.UserMemory = data;
                StaticVariable.IsInitializedUser = true;

                _logger.LogError($"InitUserMemory Finished: {DateTime.UtcNow}");
            }
        }

        public void UpdateUserMemory(int userId)
        {
            var list = StaticVariable.UserMemory.ToList();
            list = list ?? new List<User>();
            if (list.Any(t => t.Id == userId))
            {
                list = list.Where(t => t.Id != userId).OrderByDescending(t => t.UpdatedAt).ToList();
            }

            var user = _context.Users.AsNoTracking().FirstOrDefault(t => !t.Deleted && t.Id == userId);
            if (user != null)
            {
                list.Add(user);
            }

            StaticVariable.UserMemory = list;
        }

        public void TriggerUpdateUserMemory(int userId)
        {
            UpdateUserMemory(userId);

            _pubSubService.PublishSystem(new PubSubMessage
            {
                PubSubEnum = PubSubEnum.UpdateUserMemory,
                Data = userId.ToString()
            });
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
                Reason = ReasonOTP.SignUp,
                Code = otpCode,
                ExpiryTime = DateTime.UtcNow.AddMinutes(10)
            };
            if((_context.OTPs.FirstOrDefault(t => t.UserId == account.Id && t.Reason == ReasonOTP.SignUp)) != null)
            {
                _context.Update(otp);
            }
            else
            {
                await _context.AddAsync(otp);
            }
            await _context.SaveChangesAsync();
            TriggerUpdateUserMemory(account.Id);

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

        public async Task<bool> VerifyOTP(VerifyOtpDTO model)
        {
            var otp = await _context.OTPs
                .Where(o => o.UserId == model.UserId && o.Code == model.OtpCode && o.Reason == model.Reason)
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

            var user = await _context.Users.FindAsync(model.UserId);
            if (user != null)
            {
                user.IsVerify = true;
                _context.Users.Update(user);
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

        public async Task<bool> ChangePassword(ChangePasswordDTO model)
        {
            if(string.IsNullOrWhiteSpace(model.OtpCode) && model.UserId == 0)
            {
                throw new ApplicationException(Message.AuthenMessage.OTP_REQUIRED);
            }

            if(!string.IsNullOrWhiteSpace(model.OtpCode))
            {
                var check = await VerifyOTP(new VerifyOtpDTO
                {
                    OtpCode = model.OtpCode,
                    Reason = ReasonOTP.ForgetPassword,
                    UserId = model.UserId
                });
                if(check == false)
                {
                    throw new ApplicationException(Message.AuthenMessage.INVALID_OTP);
                }
            }
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

        public async Task<int> ForgetPassword(string email)
        {
            var user = StaticVariable.UserMemory.FirstOrDefault(t => t.Email == email && t.IsVerify == true);
            if(user == null)
            {
                throw new ApplicationException(Message.CommonMessage.NOT_FOUND);
            }
            var otpCode = GenerateOTP(8);
            var otp = new OTP
            {
                UserId = user.Id,
                Reason = ReasonOTP.ForgetPassword,
                Code = otpCode,
                ExpiryTime = DateTime.UtcNow.AddMinutes(10)
            };
            if ((_context.OTPs.FirstOrDefault(t => t.UserId == user.Id && t.Reason == ReasonOTP.ForgetPassword)) != null)
            {
                _context.Update(otp);
            }
            else
            {
                await _context.AddAsync(otp);
            }
            await _context.SaveChangesAsync();

            var emailBody = $@"<!DOCTYPE html>
                                <html lang=""en"">
                                <head>
                                    <meta charset=""UTF-8"">
                                    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
                                    <title>OTP Code for Password Reset Request</title>
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
                                                <p style=""color: #333333; font-size: 16px; line-height: 24px; margin: 0 0 20px;"">Dear Customer,</p>
                                                <p style=""color: #333333; font-size: 16px; line-height: 24px; margin: 0 0 20px;"">We have received a request to reset your account password. Your OTP code is: </p>
                                                <p style=""color: #333333; font-size: 24px; font-weight: bold; text-align: center; margin: 0 0 20px; padding: 20px; background-color: #f8f8f8; border-radius: 5px; letter-spacing: 2px;"">{otpCode}</p>
                                                <p style=""color: #333333; font-size: 16px; line-height: 24px; margin: 0 0 20px;"">If you did not request to reset your password, please contact our support team immediately.</p>
                                                <a href=""#"" style=""display: inline-block; padding: 12px 24px; background-color: #4CAF50; color: #ffffff; text-decoration: none; font-weight: bold; border-radius: 5px;"">Login now</a>
                                            </td>
                                        </tr>
                                        <tr>
                                            <td style=""padding: 30px; background-color: #f8f8f8; text-align: center;"">
                                                <p style=""color: #888888; font-size: 14px; margin: 0 0 10px;"">This is an automated email, please do not reply.</p>
                                                <p style=""color: #888888; font-size: 14px; margin: 0;"">© 2023 SEP490. All rights reserved.</p>
                                            </td>
                                        </tr>
                                    </table>
                                </body>
                                </html>";

            await _email.SendEmailAsync(email, "Reset password request", emailBody);

            return user.Id;
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
                Username = user.Username,
                IsVerify = user.IsVerify
            };
        }

        private static string GenerateAccessToken(User user)
        {
            try
            {
                var claims = new[]
                {
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim(ClaimTypes.Role, user.Role),
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Email, user.Email)
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
