﻿using Microsoft.EntityFrameworkCore;
using Sep490_Backend.Controllers;
using Sep490_Backend.DTO.Admin;
using Sep490_Backend.DTO.Common;
using Sep490_Backend.Infra;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Infra.Helps;
using Sep490_Backend.Services.AuthenService;
using Sep490_Backend.Services.CacheService;
using Sep490_Backend.Services.EmailService;
using Sep490_Backend.Services.HelperService;
using System.Text.RegularExpressions;

namespace Sep490_Backend.Services.AdminService
{
    public interface IAdminService
    {
        Task<bool> DeleteUser(int userId, int actionBy);
        Task<User> CreateUser(AdminCreateUserDTO model, int actionBy);
        Task<User> UpdateUser(AdminUpdateUserDTO model, int actionBy);
    }

    public class AdminService : IAdminService
    {
        private readonly BackendContext _context;
        private readonly IAuthenService _authenService;
        private readonly IEmailService _emailService;
        private readonly IHelperService _helperService;
        private readonly ICacheService _cacheService;

        public AdminService(IHelperService helperService, BackendContext context, IAuthenService authenService, IEmailService emailService, ICacheService cacheService)
        {
            _context = context;
            _authenService = authenService;
            _helperService = helperService;
            _emailService = emailService;
            _cacheService = cacheService;
        }

        public async Task<bool> DeleteUser(int userId, int actionBy)
        {
            if (!IsAdmin(actionBy))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }
            if (userId == actionBy || IsAdmin(userId))
            {
                throw new ApplicationException(Message.AdminMessage.DELETE_USER_ERROR);
            }
            
            var user = await _context.Users
                .Include(u => u.ProjectUsers)
                .FirstOrDefaultAsync(t => t.Id == userId && !t.Deleted);

            if (user == null)
            {
                throw new KeyNotFoundException(Message.CommonMessage.NOT_FOUND);
            }

            // Use the extension method for soft delete
            await _context.SoftDeleteAsync(user, actionBy);

            // Invalidate user cache
            _authenService.TriggerUpdateUserMemory(userId);
            
            // Invalidate other related caches
            await InvalidateUserRelatedCaches(userId, user.ProjectUsers?.Select(pu => pu.ProjectId).ToList());

            return true;
        }

        /// <summary>
        /// Invalidates all caches related to a user
        /// </summary>
        private async Task InvalidateUserRelatedCaches(int userId, List<int> relatedProjectIds)
        {
            // User memory is already updated by TriggerUpdateUserMemory
            
            // Invalidate construction team caches if user was part of a team
            _ = _cacheService.DeleteAsync(RedisCacheKey.CONSTRUCTION_TEAM_CACHE_KEY);
            
            // If user was part of projects, invalidate project-related caches
            if (relatedProjectIds != null && relatedProjectIds.Any())
            {
                _ = _cacheService.DeleteAsync(RedisCacheKey.PROJECT_CACHE_KEY);
                _ = _cacheService.DeleteAsync(RedisCacheKey.PROJECT_USER_CACHE_KEY);
                
                // Invalidate specific project caches
                foreach (var projectId in relatedProjectIds)
                {
                    var projectCacheKey = $"PROJECT:{projectId}";
                    _ = _cacheService.DeleteAsync(projectCacheKey);
                }
            }
        }

        public async Task<User> UpdateUser(AdminUpdateUserDTO model, int actionBy)
        {
            if (!IsAdmin(actionBy))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            // Verify user exists
            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == model.Id && !u.Deleted);
            if (existingUser == null)
            {
                throw new KeyNotFoundException(Message.CommonMessage.NOT_FOUND);
            }

            // Admin cannot modify other admins or themselves
            if (existingUser.Id == actionBy || (existingUser.Id != actionBy && IsAdmin(existingUser.Id)))
            {
                throw new ApplicationException(Message.CommonMessage.NOT_ALLOWED);
            }

            var errors = new List<ResponseError>();

            // Validate username
            if (!string.IsNullOrEmpty(model.UserName))
            {
                if (model.UserName.Contains(" "))
                {
                    errors.Add(new ResponseError
                    {
                        Message = Message.AuthenMessage.INVALID_USERNAME,
                        Field = nameof(model.UserName).ToCamelCase()
                    });
                }

                // Check if username is already taken by another user
                if (StaticVariable.UserMemory.FirstOrDefault(t => 
                    t.Username == model.UserName && 
                    t.Id != existingUser.Id) != null)
                {
                    errors.Add(new ResponseError
                    {
                        Message = Message.AuthenMessage.EXIST_USERNAME,
                        Field = nameof(model.UserName).ToCamelCase()
                    });
                }
            }

            // Validate email
            if (!string.IsNullOrEmpty(model.Email))
            {
                if (!Regex.IsMatch(model.Email, PatternConst.EMAIL_PATTERN))
                {
                    errors.Add(new ResponseError
                    {
                        Message = Message.AuthenMessage.INVALID_EMAIL,
                        Field = nameof(model.Email).ToCamelCase()
                    });
                }

                // Check if email is already taken by another user
                if (StaticVariable.UserMemory.FirstOrDefault(t => 
                    t.Email == model.Email && 
                    t.Id != existingUser.Id) != null)
                {
                    errors.Add(new ResponseError
                    {
                        Message = Message.AuthenMessage.EXIST_EMAIL,
                        Field = nameof(model.Email).ToCamelCase()
                    });
                }
            }

            // Validate role
            if (!string.IsNullOrEmpty(model.Role) && !RoleConstValue.ValidRoles.Contains(model.Role))
            {
                errors.Add(new ResponseError
                {
                    Message = Message.AdminMessage.INVALID_ROLE,
                    Field = nameof(model.Role).ToCamelCase()
                });
            }

            if (errors.Count > 0)
                throw new ValidationException(errors);

            // Update user properties
            existingUser.Username = !string.IsNullOrEmpty(model.UserName) ? model.UserName : existingUser.Username;
            existingUser.Email = !string.IsNullOrEmpty(model.Email) ? model.Email : existingUser.Email;
            existingUser.Role = !string.IsNullOrEmpty(model.Role) ? model.Role : existingUser.Role;
            existingUser.IsVerify = model.IsVerify.HasValue ? model.IsVerify.Value : existingUser.IsVerify;
            existingUser.FullName = !string.IsNullOrEmpty(model.FullName) ? model.FullName : existingUser.FullName;
            existingUser.Phone = model.Phone ?? existingUser.Phone;
            existingUser.Gender = model.Gender ?? existingUser.Gender;
            existingUser.Dob = model.Dob ?? existingUser.Dob;
            existingUser.TeamId = model.TeamId ?? existingUser.TeamId;
            existingUser.UpdatedAt = DateTime.UtcNow;
            existingUser.Updater = actionBy;

            _context.Update(existingUser);
            await _context.SaveChangesAsync();

            _authenService.TriggerUpdateUserMemory(existingUser.Id);
            
            // Create a secure copy without sensitive information
            var secureUser = new User
            {
                Id = existingUser.Id,
                Username = existingUser.Username,
                Email = existingUser.Email,
                Role = existingUser.Role,
                FullName = existingUser.FullName,
                Phone = existingUser.Phone,
                Gender = existingUser.Gender,
                Dob = existingUser.Dob,
                IsVerify = existingUser.IsVerify,
                TeamId = existingUser.TeamId,
                CreatedAt = existingUser.CreatedAt,
                UpdatedAt = existingUser.UpdatedAt,
                Creator = existingUser.Creator,
                Updater = existingUser.Updater,
                Deleted = existingUser.Deleted
                // PasswordHash and RefreshTokens are not included for security
            };

            return secureUser;
        }

        public async Task<User> CreateUser(AdminCreateUserDTO model, int actionBy)
        {
            //Check admin
            if (!IsAdmin(actionBy))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            var errors = new List<ResponseError>();

            //check exist
            var existingUser = StaticVariable.UserMemory.FirstOrDefault(u => u.Email == model.Email || u.Username == model.UserName);
            if (existingUser != null)
            {
                throw new ArgumentException(Message.AdminMessage.CREATE_USER_ERROR);
            }

            if (model.UserName.Contains(" "))
            {
                errors.Add(new ResponseError
                {
                    Message = Message.AuthenMessage.INVALID_USERNAME,
                    Field = nameof(model.UserName).ToCamelCase()
                });
            }

            if (StaticVariable.UserMemory.FirstOrDefault(t => t.Username == model.UserName) != null)
            {
                errors.Add(new ResponseError
                {
                    Message = Message.AuthenMessage.EXIST_USERNAME,
                    Field = nameof(model.UserName).ToCamelCase()
                });
            }

            if (StaticVariable.UserMemory.FirstOrDefault(t => t.Email == model.Email) != null)
            {
                errors.Add(new ResponseError
                {
                    Message = Message.AuthenMessage.EXIST_EMAIL,
                    Field = nameof(model.Email).ToCamelCase()
                });
            }

            if (!Regex.IsMatch(model.Email, PatternConst.EMAIL_PATTERN))
            {
                errors.Add(new ResponseError
                {
                    Message = Message.AuthenMessage.INVALID_EMAIL,
                    Field = nameof(model.Email).ToCamelCase()
                });
            }

            // Kiểm tra Role có hợp lệ không
            if (!RoleConstValue.ValidRoles.Contains(model.Role))
            {
                errors.Add(new ResponseError
                {
                    Message = Message.AdminMessage.INVALID_ROLE,
                    Field = nameof(model.Role).ToCamelCase()
                });
            }

            if (errors.Count > 0)
                throw new ValidationException(errors);

            //Tao moi 
            var password = _helperService.GenerateStrongPassword();
            var passwordHash = _helperService.HashPassword(password);
            var newUser = new User
            {
                Username = model.UserName,
                Email = model.Email,
                PasswordHash = passwordHash,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Role = model.Role,
                FullName = model.FullName,
                Phone = model.Phone,
                Gender = model.Gender,
                Dob = model.Dob,
                IsVerify = true,
                Creator = actionBy,
                Updater = actionBy
            };

            await _context.Users.AddAsync(newUser);
            await _context.SaveChangesAsync();

            var emailTemp = await _context.EmailTemplates.FirstOrDefaultAsync(t => t.Title == "Your new password");
            if (emailTemp == null)
            {
                throw new KeyNotFoundException(Message.CommonMessage.NOT_FOUND);
            }
            string formattedHtml = emailTemp.Body.Replace("{0}", newUser.Username).Replace("{1}", password);
            await _emailService.SendEmailAsync(newUser.Email, emailTemp.Title, formattedHtml);

            _authenService.TriggerUpdateUserMemory(newUser.Id);

            // Create a secure copy without sensitive information
            var secureUser = new User
            {
                Id = newUser.Id,
                Username = newUser.Username,
                Email = newUser.Email,
                Role = newUser.Role,
                FullName = newUser.FullName,
                Phone = newUser.Phone,
                Gender = newUser.Gender,
                Dob = newUser.Dob,
                IsVerify = newUser.IsVerify,
                TeamId = newUser.TeamId,
                CreatedAt = newUser.CreatedAt,
                UpdatedAt = newUser.UpdatedAt,
                Creator = newUser.Creator,
                Updater = newUser.Updater,
                Deleted = newUser.Deleted
                // PasswordHash and RefreshTokens are not included for security
            };

            return secureUser;
        }

        private bool IsAdmin(int userId)
        {
            var user = StaticVariable.UserMemory.ToList();
            var checkAdmin = user.FirstOrDefault(t => t.Id == userId);
            if (checkAdmin == null || checkAdmin.Role != RoleConstValue.ADMIN)
            {
                return false;
            }
            return true;
        }
    }
}
