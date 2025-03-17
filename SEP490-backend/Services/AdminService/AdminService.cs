using Microsoft.EntityFrameworkCore;
using Sep490_Backend.Controllers;
using Sep490_Backend.DTO.Admin;
using Sep490_Backend.DTO.Common;
using Sep490_Backend.Infra;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Infra.Helps;
using Sep490_Backend.Services.AuthenService;
using Sep490_Backend.Services.DataService;
using Sep490_Backend.Services.EmailService;
using Sep490_Backend.Services.HelperService;
using System.Text.RegularExpressions;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sep490_Backend.Services.AdminService
{
    public interface IAdminService
    {
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
        private readonly IDataService _dataService;

        public AdminService(IHelperService helperService, BackendContext context, IAuthenService authenService, IEmailService emailService, IDataService dataService)
        {
            _context = context;
            _authenService = authenService;
            _helperService = helperService;
            _emailService = emailService;
            _dataService = dataService;
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
            var user = await _context.Users.FirstOrDefaultAsync(t => t.Id == userId && !t.Deleted);

            if (user != null)
            {
                user.Deleted = true;
                _context.Update(user);
                await _context.SaveChangesAsync();
            }
            else
            {
                throw new KeyNotFoundException(Message.CommonMessage.NOT_FOUND);
            }

            _authenService.TriggerUpdateUserMemory(userId);

            return true;
        }

        public async Task<bool> UpdateUser(AdminUpdateUserDTO model, int actionBy)
        {
            if (!IsAdmin(actionBy))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            var errors = new List<ResponseError>();

            //Lấy user
            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == model.Id && !u.Deleted);
            if (existingUser == null)
            {
                throw new KeyNotFoundException(Message.CommonMessage.NOT_FOUND);
            }

            if (model.UserName.Contains(" "))
            {
                errors.Add(new ResponseError
                {
                    Message = Message.AuthenMessage.INVALID_USERNAME,
                    Field = nameof(model.UserName).ToCamelCase()
                });
            }

            if(StaticVariable.UserMemory.FirstOrDefault(t => t.Username == model.UserName && existingUser.Username != model.UserName) != null)
            {
                errors.Add(new ResponseError
                {
                    Message = Message.AuthenMessage.EXIST_USERNAME,
                    Field = nameof(model.UserName).ToCamelCase()
                });
            }

            if (StaticVariable.UserMemory.FirstOrDefault(t => t.Email == model.Email && existingUser.Email != model.Email) != null)
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

            //Admin khoong the sua admin khac
            if (existingUser.Id == actionBy || IsAdmin(existingUser.Id))
            {
                errors.Add(new ResponseError
                {
                    Message = Message.CommonMessage.NOT_ALLOWED,
                    Field = nameof(model.Id).ToCamelCase()
                });
            }

            if (errors.Count > 0)
                throw new ValidationException(errors);

            //Cap nhat
            existingUser.Username = model.UserName ?? existingUser.Username;
            existingUser.Email = model.Email ?? existingUser.Email;
            existingUser.Role = model.Role ?? existingUser.Role;
            existingUser.IsVerify = model.IsVerify ? model.IsVerify : existingUser.IsVerify;
            existingUser.FullName = model.FullName ?? existingUser.FullName;
            existingUser.Phone = model.Phone ?? existingUser.Phone;
            existingUser.Gender = model.Gender ?? existingUser.Gender;
            existingUser.UpdatedAt = DateTime.UtcNow;
            existingUser.Updater = actionBy;

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
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            var errors = new List<ResponseError>();

            //check exist
            var existingUser = StaticVariable.UserMemory.FirstOrDefault(u => u.Email == model.Email || u.Username == model.UserName);
            if (existingUser != null)
            {
                throw new ApplicationException(Message.AdminMessage.CREATE_USER_ERROR);
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
