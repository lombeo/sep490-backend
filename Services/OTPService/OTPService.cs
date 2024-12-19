using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Linq;
using System.Threading.Tasks;
using Sep490_Backend.Services.CacheService;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Infra.Enums;
using Sep490_Backend.Infra.Constants;

namespace Sep490_Backend.Services.OTPService
{
    public interface IOTPService
    {
        Task<OTP> GetStoredOTP(int userId, ReasonOTP reason);
        Task<string> GenerateOTP(int length, ReasonOTP reason, int userId, TimeSpan validFor);
    }

    public class OTPService : IOTPService
    {
        private readonly ICacheService _cacheService;

        public OTPService(ICacheService cacheService)
        {
            _cacheService = cacheService;
        }

        public async Task<string> GenerateOTP(int length, ReasonOTP reason, int userId, TimeSpan validFor)
        {
            var otp = GetOTP(length);
            var expiryTime = DateTime.UtcNow.Add(validFor);
            var newOTP = new OTP
            {
                UserId = userId,
                Code = otp,
                Reason = reason,
                ExpiryTime = expiryTime
            };

            var cacheKey = string.Format(RedisCacheKey.OTP_CACHE_KEY, userId, reason);

            await _cacheService.SetAsync(cacheKey, newOTP, validFor);

            return otp;
        }

        public async Task<OTP> GetStoredOTP(int userId, ReasonOTP reason)
        {
            var cacheKey = string.Format(RedisCacheKey.OTP_CACHE_KEY, userId, reason);

            var otp = await _cacheService.GetAsync<OTP>(cacheKey);

            if (otp == null || otp.ExpiryTime < DateTime.UtcNow)
            {
                throw new ApplicationException(Message.CommonMessage.NOT_FOUND);
            }

            return otp;
        }

        private string GetOTP(int length = 6)
        {
            byte[] randomNumber = new byte[length];

            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomNumber);
            }

            var otpCode = string.Join("", randomNumber.Select(b => (b % 10).ToString()));

            return otpCode.Substring(0, length);
        }
    }
}
