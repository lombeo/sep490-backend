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
using Sep490_Backend.Services.OTPService;
using System.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Sep490_Backend.Services.AuthenService
{
    public interface IAuthenService
    {
        void InitUserMemory();
        void UpdateUserMemory(int userId);
        void TriggerUpdateUserMemory(int userId);
        Task<bool> SignUp(SignUpDTO model);
        Task<bool> VerifyOTP(VerifyOtpDTO model);
        Task<bool> ChangePassword(ChangePasswordDTO model, int userId);
        Task<int> ForgetPassword(string email);
        Task<ReturnSignInDTO> SignIn(SignInDTO model);
        Task<string> Refresh(string refreshToken);
        Task<ReturnSignInDTO> GoogleCallback(string authorizationToken);
        void TriggerUpdateUserProfileMemory(int userProfileId);
        void UpdateUserProfileMemory(int userProfileId);
    }

    public class AuthenService : IAuthenService
    {
        private readonly BackendContext _context;
        private readonly IEmailService _email;
        private readonly IPubSubService _pubSubService;
        private readonly ILogger<AuthenService> _logger;
        private readonly IOTPService _otpService;
        private readonly ICacheService _cacheService;

        private static readonly char[] UpperCaseLetters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();
        private static readonly char[] LowerCaseLetters = "abcdefghijklmnopqrstuvwxyz".ToCharArray();
        private static readonly char[] Numbers = "0123456789".ToCharArray();
        private static readonly char[] SpecialChars = "!@#$%^&*()-_=+[]{}|;:',.<>?".ToCharArray();

        public AuthenService(BackendContext context, IEmailService email, IPubSubService pubSubService, ILogger<AuthenService> logger, IOTPService otpService, ICacheService cacheService)
        {
            _context = context;
            _email = email;
            _pubSubService = pubSubService;
            _logger = logger;
            _otpService = otpService;
            _cacheService = cacheService;
        }

        public void InitUserMemory()
        {
            if (!StaticVariable.IsInitializedUser)
            {
                _logger.LogError($"InitUserMemory and InitUserProfileMemory Started: {DateTime.UtcNow}");

                var data = _context.Users.AsNoTracking().Where(t => !t.Deleted)
                                            .OrderByDescending(t => t.UpdatedAt).ToList();
                var profileData = _context.UserProfiles.AsNoTracking().Where(t => !t.Deleted)
                                            .OrderByDescending(t => t.UpdatedAt).ToList();
                StaticVariable.UserMemory = data;
                StaticVariable.UserProfileMemory = profileData;
                StaticVariable.IsInitializedUser = true;
                StaticVariable.IsInitializedUserProfile = true;

                _logger.LogError($"InitUserMemory and InitUserProfileMemory Finished: {DateTime.UtcNow}");
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

        public void UpdateUserProfileMemory(int userProfileId)
        {
            var list = StaticVariable.UserProfileMemory.ToList();
            list = list ?? new List<UserProfile>();
            if (list.Any(t => t.Id == userProfileId))
            {
                list = list.Where(t => t.Id != userProfileId).OrderByDescending(t => t.UpdatedAt).ToList();
            }

            var user = _context.UserProfiles.AsNoTracking().FirstOrDefault(t => !t.Deleted && t.Id == userProfileId);
            if (user != null)
            {
                list.Add(user);
            }

            StaticVariable.UserProfileMemory = list;
        }

        public void TriggerUpdateUserProfileMemory(int userProfileId)
        {
            UpdateUserProfileMemory(userProfileId);

            _pubSubService.PublishSystem(new PubSubMessage
            {
                PubSubEnum = PubSubEnum.UpdateUserProfileMemory,
                Data = userProfileId.ToString()
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
                Role = RoleConstValue.USER,
                IsVerify = false,
            };

            await _context.AddAsync(account);
            await _context.SaveChangesAsync();

            var userProfile = new UserProfile
            {
                UserId = account.Id,
                Phone = model.Phone,
                Gender = model.Gender,
                FullName = model.FullName,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _context.AddAsync(userProfile);
            await _context.SaveChangesAsync();

            var otpCode = await _otpService.GenerateOTP(6, ReasonOTP.SignUp, account.Id, TimeSpan.FromMinutes(10));

            TriggerUpdateUserMemory(account.Id);

            var emailTemp = await _context.EmailTemplates.FirstOrDefaultAsync(t => t.Title == "Verify your account");

            if(emailTemp == null)
            {
                throw new ApplicationException(Message.CommonMessage.ERROR_HAPPENED);
            }
            string formattedHtml = emailTemp.Body.Replace("{0:s}", account.Username).Replace("{1:s}", otpCode);
            // Gửi email
            await _email.SendEmailAsync(account.Email, emailTemp.Title, formattedHtml);

            return true;
        }

        public async Task<bool> VerifyOTP(VerifyOtpDTO model)
        {
            var otp = await _otpService.GetStoredOTP(model.UserId, model.Reason);

            if (otp.Code != model.OtpCode)
            {
                return false;
            }

            var user = await _context.Users.FindAsync(model.UserId);
            if (user == null)
            {
                throw new ApplicationException(Message.CommonMessage.NOT_FOUND);
            }

            if(model.Reason == ReasonOTP.ForgetPassword)
            {
                var password = GenerateStrongPassword();
                user.PasswordHash = HashPassword(password);

                var emailTemp = await _context.EmailTemplates.FirstOrDefaultAsync(t => t.Title == "Your new password");

                if (emailTemp == null)
                {
                    throw new ApplicationException(Message.CommonMessage.ERROR_HAPPENED);
                }
                string formattedHtml = emailTemp.Body.Replace("{0}", user.Username).Replace("{1}", password);
                // Gửi email
                await _email.SendEmailAsync(user.Email, emailTemp.Title, formattedHtml);
            }

            if(model.Reason == ReasonOTP.SignUp)
            {
                user.IsVerify = true;
                _context.Users.Update(user);
            }

            _ = _cacheService.DeleteAsync(string.Format(RedisCacheKey.OTP_CACHE_KEY, model.UserId, model.Reason));
            await _context.SaveChangesAsync();

            return true;
        }

        private void ValidateSignUp(SignUpDTO model)
        {
            var data = StaticVariable.UserMemory.ToList();

            if(string.IsNullOrWhiteSpace(model.Username) ||
                string.IsNullOrWhiteSpace(model.Password) ||
                string.IsNullOrWhiteSpace(model.Email) ||
                string.IsNullOrWhiteSpace(model.FullName) ||
                string.IsNullOrWhiteSpace(model.Phone))
            {
                throw new ApplicationException(Message.CommonMessage.MISSING_PARAM);
            }

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

        public async Task<bool> ChangePassword(ChangePasswordDTO model, int userId)
        {
            var user = await _context.Users.FirstOrDefaultAsync(t => t.Id == userId);

            if(string.IsNullOrWhiteSpace(model.CurrentPassword) || string.IsNullOrWhiteSpace(model.ConfirmPassword) || string.IsNullOrWhiteSpace(model.NewPassword))
            {
                throw new ApplicationException(Message.CommonMessage.MISSING_PARAM);
            }

            if (user == null)
            {
                throw new ApplicationException(Message.CommonMessage.NOT_FOUND);
            }
            if (string.Compare(HashPassword(model.CurrentPassword), user.PasswordHash) != 0)
            {
                throw new ApplicationException(Message.AuthenMessage.INVALID_CURRENT_PASSWORD);
            }
            if(string.Compare(model.NewPassword, model.ConfirmPassword) != 0)
            {
                throw new ApplicationException(Message.AuthenMessage.INVALID_CONFIRM_PASSWORD);
            }
            user.PasswordHash = HashPassword(model.NewPassword);
            _context.Update(user);
            await _context.SaveChangesAsync();
            TriggerUpdateUserMemory(user.Id);
            return true;
        }

        public async Task<int> ForgetPassword(string email)
        {
            var user = StaticVariable.UserMemory.FirstOrDefault(t => t.Email == email && t.IsVerify == true);
            if(user == null)
            {
                throw new ApplicationException(Message.CommonMessage.NOT_FOUND);
            }
            var otpCode = await _otpService.GenerateOTP(8, ReasonOTP.ForgetPassword, user.Id, TimeSpan.FromMinutes(10));

            var emailTemp = await _context.EmailTemplates.FirstOrDefaultAsync(t => t.Title == "Reset password request");

            if(emailTemp == null)
            {
                throw new ApplicationException(Message.CommonMessage.ERROR_HAPPENED);
            }

            string formattedHtml = emailTemp.Body.Replace("{0}", user.Username).Replace("{1}", otpCode);

            await _email.SendEmailAsync(email, emailTemp.Title, formattedHtml);

            return user.Id;
        }

        public static string GenerateStrongPassword(int length = 12)
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
                throw new ApplicationException(Message.CommonMessage.NOT_FOUND);
            }
            return GenerateAccessToken(user);
        }

        public async Task<ReturnSignInDTO> GoogleCallback(string authorizationToken)
        {
            string redirectUri = "https://localhost:7233/sep490/authen/google-callback";
            string tokenUrl = "https://oauth2.googleapis.com/token";
            string userInfoUrl = "https://www.googleapis.com/oauth2/v2/userinfo?fields=email&access_token=";

            var dicData = new Dictionary<string, string>
            {
                ["client_id"] = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID"),
                ["client_secret"] = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET"),
                ["code"] = HttpUtility.UrlDecode(authorizationToken),
                ["grant_type"] = "authorization_code",
                ["redirect_uri"] = redirectUri
            };

            try
            {
                using (var client = new HttpClient())
                {
                    // Lấy access token
                    using (var content = new FormUrlEncodedContent(dicData))
                    {
                        HttpResponseMessage tokenResponse = await client.PostAsync(tokenUrl, content);
                        string tokenJson = await tokenResponse.Content.ReadAsStringAsync();

                        if (!tokenResponse.IsSuccessStatusCode)
                        {
                            throw new ApplicationException(Message.AuthenMessage.IVALID_ACCESS_TOKEN);
                        }
                        var tokenData = JsonSerializer.Deserialize<GoogleTokenResponseDTO>(tokenJson);

                        // Lấy thông tin người dùng từ Google
                        HttpResponseMessage userInfoResponse = await client.GetAsync(userInfoUrl + tokenData.AccessToken);
                        string userInfoJson = await userInfoResponse.Content.ReadAsStringAsync();
                        var jsonNode = JsonNode.Parse(userInfoJson);
                        var email = jsonNode?["email"]?.ToString();

                        // Check Email
                        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
                        if (user == null)
                        {
                            var account = new User
                            {
                                Username = "",
                                Email = email,
                                PasswordHash = "",
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow,
                                Role = RoleConstValue.USER,
                                IsVerify = true,
                            };
                            await _context.AddAsync(account);
                            await _context.SaveChangesAsync();
                            var userProfile = new UserProfile
                            {
                                UserId = account.Id,
                                Phone = null,
                                Gender = true,
                                FullName = "",
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow
                            };

                            await _context.AddAsync(userProfile);
                            await _context.SaveChangesAsync();
                            TriggerUpdateUserMemory(account.Id);
                        }
                        TriggerUpdateUserMemory(user.Id);

                        // Tạo AccessToken và RefreshToken cho người dùng
                        var accessToken = GenerateAccessToken(user);
                        var refreshToken = GenerateRefreshToken();

                        var existingToken = await _context.RefreshTokens.FirstOrDefaultAsync(rt => rt.UserId == user.Id);
                        var expiryDate = DateTime.UtcNow.Add(StaticVariable.RefreshTokenExpiryDuration);
                        if (existingToken != null)
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
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Unexpected error ");
                throw;
            }
        }


    }
}
