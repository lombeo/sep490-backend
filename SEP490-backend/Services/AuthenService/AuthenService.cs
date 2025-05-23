﻿using Sep490_Backend.DTO.Authen;
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
using Sep490_Backend.Services.HelperService;
using Sep490_Backend.Services.GoogleDriveService;
using Sep490_Backend.DTO.Common;
using Sep490_Backend.Infra.Helps;
using Sep490_Backend.Controllers;

namespace Sep490_Backend.Services.AuthenService
{
    public interface IAuthenService
    {
        void InitUserMemory();
        void UpdateUserMemory(int userId);
        void TriggerUpdateUserMemory(int userId);
        //Task<bool> SignUp(SignUpDTO model);
        Task<bool> VerifyOTP(VerifyOtpDTO model);
        Task<bool> ChangePassword(ChangePasswordDTO model, int userId);
        Task<int> ForgetPassword(string email);
        Task<ReturnSignInDTO> SignIn(SignInDTO model);
        Task<string> Refresh(string refreshToken);
        UserDTO UserProfileDetail(int actionBy);
        Task<UserDTO> UpdateProfile(int actionBy, UserUpdateProfileDTO model);
    }

    public class AuthenService : IAuthenService
    {
        private readonly BackendContext _context;
        private readonly IEmailService _email;
        private readonly IPubSubService _pubSubService;
        private readonly ILogger<AuthenService> _logger;
        private readonly IOTPService _otpService;
        private readonly ICacheService _cacheService;
        private readonly IHelperService _helperService;
        private readonly IGoogleDriveService _googleDriveService;

        public AuthenService(IHelperService helperService, BackendContext context, IEmailService email, IPubSubService pubSubService, ILogger<AuthenService> logger, IOTPService otpService, ICacheService cacheService, IGoogleDriveService googleDriveService)
        {
            _context = context;
            _email = email;
            _pubSubService = pubSubService;
            _logger = logger;
            _otpService = otpService;
            _cacheService = cacheService;
            _helperService = helperService;
            _googleDriveService = googleDriveService;
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

        //public async Task<bool> SignUp(SignUpDTO model)
        //{
        //    ValidateSignUp(model);

        //    var password = _helperService.HashPassword(model.Password);

        //    var account = new User
        //    {
        //        Username = model.Username,
        //        Email = model.Email,
        //        PasswordHash = password,
        //        CreatedAt = DateTime.UtcNow,
        //        UpdatedAt = DateTime.UtcNow,
        //        Role = RoleConstValue.USER,
        //        IsVerify = false,
        //    };

        //    await _context.AddAsync(account);
        //    await _context.SaveChangesAsync();

        //    var userProfile = new UserProfile
        //    {
        //        UserId = account.Id,
        //        Phone = model.Phone,
        //        Gender = model.Gender,
        //        FullName = model.FullName,
        //        CreatedAt = DateTime.UtcNow,
        //        UpdatedAt = DateTime.UtcNow
        //    };
        //    await _context.AddAsync(userProfile);
        //    await _context.SaveChangesAsync();

        //    var otpCode = await _otpService.GenerateOTP(6, ReasonOTP.SignUp, account.Id, TimeSpan.FromMinutes(10));

        //    TriggerUpdateUserMemory(account.Id);

        //    var emailTemp = await _context.EmailTemplates.FirstOrDefaultAsync(t => t.Title == "Verify your account");

        //    if(emailTemp == null)
        //    {
        //        throw new ApplicationException(Message.CommonMessage.ERROR_HAPPENED);
        //    }
        //    string formattedHtml = emailTemp.Body.Replace("{0:s}", account.Username).Replace("{1:s}", otpCode);
        //    // Gửi email
        //    await _email.SendEmailAsync(account.Email, emailTemp.Title, formattedHtml);

        //    return true;
        //}

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
                throw new KeyNotFoundException(Message.CommonMessage.NOT_FOUND);
            }

            if(model.Reason == ReasonOTP.ForgetPassword)
            {
                var password = _helperService.GenerateStrongPassword();
                user.PasswordHash = _helperService.HashPassword(password);

                var emailTemp = await _context.EmailTemplates.FirstOrDefaultAsync(t => t.Title == "Your new password");

                if (emailTemp == null)
                {
                    throw new KeyNotFoundException(Message.CommonMessage.NOT_FOUND);
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

        //private void ValidateSignUp(SignUpDTO model)
        //{
        //    var data = StaticVariable.UserMemory.ToList();

        //    if(string.IsNullOrWhiteSpace(model.Username) ||
        //        string.IsNullOrWhiteSpace(model.Password) ||
        //        string.IsNullOrWhiteSpace(model.Email) ||
        //        string.IsNullOrWhiteSpace(model.FullName) ||
        //        string.IsNullOrWhiteSpace(model.Phone))
        //    {
        //        throw new ApplicationException(Message.CommonMessage.MISSING_PARAM);
        //    }

        //    if (data.Any(t => t.Email.ToLower().Equals(model.Email.ToLower())))
        //    {
        //        throw new ApplicationException(Message.AuthenMessage.EXIST_EMAIL);
        //    }

        //    if (data.Any(t => t.Username.ToLower().Equals(model.Username.ToLower())))
        //    {
        //        throw new ApplicationException(Message.AuthenMessage.EXIST_USERNAME);
        //    }

        //    if (model.Username.Contains(" "))
        //    {
        //        throw new ApplicationException(Message.AuthenMessage.INVALID_USERNAME);
        //    }

        //    if (!Regex.IsMatch(model.Email, PatternConst.EMAIL_PATTERN))
        //    {
        //        throw new ApplicationException(Message.AuthenMessage.INVALID_EMAIL);
        //    }

        //    if (!Regex.IsMatch(model.Password, PatternConst.PASSWORD_PATTERN))
        //    {
        //        throw new ApplicationException(Message.AuthenMessage.INVALID_PASSWORD);
        //    }
        //}

        public async Task<bool> ChangePassword(ChangePasswordDTO model, int userId)
        {
            var user = await _context.Users.FirstOrDefaultAsync(t => t.Id == userId);

            if(string.IsNullOrWhiteSpace(model.CurrentPassword) || string.IsNullOrWhiteSpace(model.ConfirmPassword) || string.IsNullOrWhiteSpace(model.NewPassword))
            {
                throw new ArgumentNullException(Message.CommonMessage.MISSING_PARAM);
            }

            if (user == null)
            {
                throw new KeyNotFoundException(Message.CommonMessage.NOT_FOUND);
            }
            if (!Regex.IsMatch(model.CurrentPassword, PatternConst.PASSWORD_PATTERN) || !Regex.IsMatch(model.NewPassword, PatternConst.PASSWORD_PATTERN))
            {
                throw new ArgumentException(Message.AuthenMessage.INVALID_PASSWORD);
            }
            if (string.Compare(_helperService.HashPassword(model.CurrentPassword), user.PasswordHash) != 0)
            {
                throw new ArgumentException(Message.AuthenMessage.INVALID_CURRENT_PASSWORD);
            }
            if(string.Compare(model.NewPassword, model.ConfirmPassword) != 0)
            {
                throw new ArgumentException(Message.AuthenMessage.INVALID_CONFIRM_PASSWORD);
            }
            user.PasswordHash = _helperService.HashPassword(model.NewPassword);
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
                throw new KeyNotFoundException(Message.CommonMessage.NOT_FOUND);
            }
            var otpCode = await _otpService.GenerateOTP(8, ReasonOTP.ForgetPassword, user.Id, TimeSpan.FromMinutes(10));

            var emailTemp = await _context.EmailTemplates.FirstOrDefaultAsync(t => t.Title == "Reset password request");

            if(emailTemp == null)
            {
                throw new KeyNotFoundException(Message.CommonMessage.NOT_FOUND);
            }

            string formattedHtml = emailTemp.Body.Replace("{0}", user.Username).Replace("{1}", otpCode);

            await _email.SendEmailAsync(email, emailTemp.Title, formattedHtml);

            return user.Id;
        }

        public async Task<ReturnSignInDTO> SignIn(SignInDTO model)
        {
            //Validate
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == model.Username || u.Email == model.Username);

            //Check username + password
            if (user == null || (_helperService.HashPassword(model.Password) != user.PasswordHash))
            {
                throw new SecurityTokenException(Message.AuthenMessage.INVALID_CREDENTIALS);
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
                UserId = user.Id,
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
                throw new ArgumentException(Message.AuthenMessage.INVALID_TOKEN);
            }
            var userId = existingRefreshToken.UserId;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                throw new KeyNotFoundException(Message.CommonMessage.NOT_FOUND);
            }
            return GenerateAccessToken(user);
        }

        public UserDTO UserProfileDetail(int actionBy)
        {
            var data = StaticVariable.UserMemory.FirstOrDefault(t => t.Id == actionBy);
            if(data == null)
            {
                throw new KeyNotFoundException(Message.CommonMessage.NOT_FOUND);
            }
            return new UserDTO
            {
                UserId = data.Id,
                Username = data.Username,
                Email = data.Email,
                Role = data.Role,
                IsVerify = data.IsVerify,
                FullName = data.FullName,
                Phone = data.Phone,
                Gender = data.Gender,
                UpdatedAt = data.UpdatedAt,
                CreatedAt = data.CreatedAt,
                Creator = data.Creator,
                Updater = data.Updater,
                PicProfile = data.PicProfile,
                Address = data.Address,
                Dob = data.Dob
            };
        }

        public async Task<UserDTO> UpdateProfile(int actionBy, UserUpdateProfileDTO model)
        {
            var errors = new List<ResponseError>();

            // Get user from database (not just memory) to ensure we can update it
            var user = await _context.Users.FirstOrDefaultAsync(t => t.Id == actionBy && !t.Deleted);
            if (user == null)
            {
                throw new KeyNotFoundException(Message.CommonMessage.NOT_FOUND);
            }

            // Validate username if being updated
            if (!string.IsNullOrEmpty(model.Username))
            {
                if (model.Username.Contains(" "))
                {
                    errors.Add(new ResponseError
                    {
                        Message = Message.AuthenMessage.INVALID_USERNAME,
                        Field = nameof(model.Username).ToCamelCase()
                    });
                }

                if (StaticVariable.UserMemory.FirstOrDefault(t => t.Username == model.Username && t.Id != actionBy) != null)
                {
                    errors.Add(new ResponseError
                    {
                        Message = Message.AuthenMessage.EXIST_USERNAME,
                        Field = nameof(model.Username).ToCamelCase()
                    });
                }
            }

            if (errors.Count > 0)
                throw new ValidationException(errors);

            // Update profile picture if provided
            if (model.PicProfile != null)
            {
                // Delete existing profile picture if any
                if (!string.IsNullOrEmpty(user.PicProfile))
                {
                    try
                    {
                        // Extract file ID from the URL
                        var fileId = user.PicProfile.Split("id=").Last().Split("&").First();
                        await _googleDriveService.DeleteFile(fileId);
                    }
                    catch (Exception ex)
                    {
                        // Log the error but continue
                        _logger.LogError($"Failed to delete old profile picture: {ex.Message}");
                    }
                }

                // Upload new profile picture
                using (var stream = model.PicProfile.OpenReadStream())
                {
                    var uploadResult = await _googleDriveService.UploadFile(
                        stream,
                        model.PicProfile.FileName,
                        model.PicProfile.ContentType
                    );

                    // Store the URL
                    user.PicProfile = uploadResult;
                }
            }

            // Update user properties
            user.Username = !string.IsNullOrEmpty(model.Username) ? model.Username : user.Username;
            user.FullName = !string.IsNullOrEmpty(model.FullName) ? model.FullName : user.FullName;
            user.Phone = !string.IsNullOrEmpty(model.Phone) ? model.Phone : user.Phone;
            user.Gender = model.Gender ?? user.Gender;
            user.Dob = model.Dob ?? user.Dob;
            user.Address = !string.IsNullOrEmpty(model.Address) ? model.Address : user.Address;
            user.UpdatedAt = DateTime.UtcNow;
            user.Updater = actionBy;

            _context.Update(user);
            await _context.SaveChangesAsync();

            // Update in-memory cache
            TriggerUpdateUserMemory(user.Id);

            // Return updated user information
            return new UserDTO
            {
                UserId = user.Id,
                Username = user.Username,
                Email = user.Email,
                Role = user.Role,
                IsVerify = user.IsVerify,
                FullName = user.FullName,
                Phone = user.Phone,
                Gender = user.Gender,
                UpdatedAt = user.UpdatedAt,
                CreatedAt = user.CreatedAt,
                Creator = user.Creator,
                Updater = user.Updater,
                PicProfile = user.PicProfile,
                Address = user.Address,
                Dob = user.Dob
            };
        }
    }
}
