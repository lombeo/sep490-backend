using Microsoft.EntityFrameworkCore;
using Sep490_Backend.DTO;
using Sep490_Backend.DTO.AdminDTO;
using Sep490_Backend.Infra;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Services.AuthenService;
using Sep490_Backend.Services.EmailService;
using Sep490_Backend.Services.HelperService;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Sep490_Backend.Services.AdminService
{
    public interface IAdminService
    {
        Task<List<UserDTO>> ListUser(AdminSearchUserDTO model);
        Task<bool> DeleteUser(int userId, int actionBy);
        Task<bool> CreateUser(AdminCreateUserDTO model, int actionBy);
        Task<bool> UpdateUser(AdminUpdateUserDTO model, int actionBy);
    }

    public class AdminService : IAdminService
    {
        private readonly BackendContext _context;
        private readonly IAuthenService _authenService;
        private readonly IEmailService _emailService;
        private readonly IHelperService _helperService;

        public AdminService(IHelperService helperService, BackendContext context, IAuthenService authenService, IEmailService emailService)
        {
            _context = context;
            _authenService = authenService;
            _helperService = helperService;
            _emailService = emailService;
        }

        public async Task<bool> DeleteUser(int userId, int actionBy)
        {
            if (!IsAdmin(actionBy))
            {
                throw new ApplicationException(Message.CommonMessage.NOT_ALLOWED);
            }
            if (userId == actionBy || IsAdmin(userId))
            {
                throw new ApplicationException(Message.AdminMessage.DELETE_USER_ERROR);
            }
            var user = await _context.Users.FirstOrDefaultAsync(t => t.Id == userId && !t.Deleted);
            var userProfile = await _context.UserProfiles.FirstOrDefaultAsync(t => t.UserId == userId && !t.Deleted);

            if (userProfile != null && user != null)
            {
                user.Deleted = true;
                userProfile.Deleted = true;
                _context.Update(user);
                _context.Update(userProfile);
                await _context.SaveChangesAsync();
            }
            else
            {
                throw new ApplicationException(Message.CommonMessage.NOT_FOUND);
            }

            _authenService.TriggerUpdateUserMemory(userId);
            _authenService.TriggerUpdateUserProfileMemory(userId);

            return true;
        }

        public async Task<List<UserDTO>> ListUser(AdminSearchUserDTO model)
        {
            var data = new List<UserDTO>();
            var user = StaticVariable.UserMemory.ToList();
            bool check = IsAdmin(model.ActionBy);
            if (!check)
            {
                throw new ApplicationException(Message.CommonMessage.NOT_ALLOWED);
            }
            var userProfile = StaticVariable.UserProfileMemory.ToList();

            for (int i = 0; i < user.Count(); i++)
            {
                data.Add(new UserDTO
                {
                    UserId = user[i].Id,
                    Username = user[i].Username,
                    Email = user[i].Email,
                    Role = user[i].Role,
                    IsVerify = user[i].IsVerify,
                    FullName = userProfile.FirstOrDefault(t => t.UserId == user[i].Id)?.FullName ?? "",
                    Phone = userProfile.FirstOrDefault(t => t.UserId == user[i].Id)?.Phone ?? "",
                    Gender = userProfile.FirstOrDefault(t => t.UserId == user[i].Id)?.Gender,
                    CreatedAt = user[i].CreatedAt,
                    UpdatedAt = userProfile.FirstOrDefault(t => t.UserId == user[i].Id)?.UpdatedAt
                });
            }

            data = data.OrderByDescending(t => t.CreatedAt).ToList();

            if (!string.IsNullOrWhiteSpace(model.KeyWord))
            {
                data = data.Where(t => t.FullName.Contains(model.KeyWord) || t.Username.Contains(model.KeyWord) || t.Email.Contains(model.KeyWord) || t.Phone.Contains(model.KeyWord)).ToList();
            }
            if (!string.IsNullOrWhiteSpace(model.Role))
            {
                data = data.Where(t => t.Role == model.Role).ToList();
            }
            if (model.Gender != null)
            {
                data = data.Where(t => t.Gender == model.Gender).ToList();
            }

            model.Total = data.Count();

            if (model.PageSize > 0)
            {
                data = data.Skip(model.Skip).Take(model.PageSize).ToList();
            }

            return data;
        }

        public async Task<bool> UpdateUser(AdminUpdateUserDTO model, int actionBy)
        {
            if (!IsAdmin(actionBy))
            {
                throw new ApplicationException(Message.CommonMessage.NOT_ALLOWED);
            }
            if (model == null || model.Id <= 0)
            {
                throw new ApplicationException(Message.CommonMessage.INVALID_FORMAT);
            }
            if (model.UserName.Contains(" "))
            {
                throw new ApplicationException(Message.AuthenMessage.INVALID_USERNAME);
            }

            if (!Regex.IsMatch(model.Email, PatternConst.EMAIL_PATTERN))
            {
                throw new ApplicationException(Message.AuthenMessage.INVALID_EMAIL);
            }

            //Lấy user
            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == model.Id && !u.Deleted);
            if (existingUser == null)
            {
                throw new ApplicationException(Message.CommonMessage.NOT_FOUND);
            }

            //Admin khoong the sua admin khac
            if (existingUser.Id == actionBy || IsAdmin(existingUser.Id))
            {
                throw new ApplicationException(Message.CommonMessage.NOT_ALLOWED);
            }

            //Cap nhat
            existingUser.Username = model.UserName ?? existingUser.Username;
            existingUser.Email = model.Email ?? existingUser.Email;
            existingUser.Role = model.Role ?? existingUser.Role;
            existingUser.IsVerify = model.IsVerify ? model.IsVerify : existingUser.IsVerify;
            existingUser.UpdatedAt = DateTime.UtcNow;

            _context.Update(existingUser);
            _context.SaveChanges();

            _authenService.TriggerUpdateUserMemory(existingUser.Id);
            return true;
        }

        public async Task<bool> CreateUser(AdminCreateUserDTO model, int actionBy)
        {
            //Check admin
            if (!IsAdmin(actionBy))
            {
                throw new ApplicationException(Message.CommonMessage.NOT_ALLOWED);
            }
            //check format
            if (model == null || string.IsNullOrWhiteSpace(model.UserName) || string.IsNullOrWhiteSpace(model.Email) || string.IsNullOrWhiteSpace(model.Role))
            {
                throw new ApplicationException(Message.CommonMessage.INVALID_FORMAT);
            }
            if (model.UserName.Contains(" "))
            {
                throw new ApplicationException(Message.AuthenMessage.INVALID_USERNAME);
            }

            if (!Regex.IsMatch(model.Email, PatternConst.EMAIL_PATTERN))
            {
                throw new ApplicationException(Message.AuthenMessage.INVALID_EMAIL);
            }
            //check exist
            var existingUser = StaticVariable.UserMemory.FirstOrDefault(u => u.Email == model.Email || u.Username == model.UserName);
            if (existingUser != null)
            {
                throw new ApplicationException(Message.AdminMessage.CREATE_USER_ERROR);
            }

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
                IsVerify = true,
            };
            await _context.AddAsync(newUser);

            var userProfile = new UserProfile
            {
                UserId = newUser.Id,
                FullName = "Created " + model.Role + " User",
                Phone = "000000000",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _context.AddAsync(userProfile);

            var emailTemp = await _context.EmailTemplates.FirstOrDefaultAsync(t => t.Title == "Your new password");
            if (emailTemp == null)
            {
                throw new ApplicationException(Message.CommonMessage.NOT_FOUND);
            }
            string formattedHtml = emailTemp.Body.Replace("{0}", newUser.Username).Replace("{1}", password);
            await _emailService.SendEmailAsync(newUser.Email, emailTemp.Title, formattedHtml);

            await _context.SaveChangesAsync();

            _authenService.TriggerUpdateUserMemory(newUser.Id);

            return true;
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
