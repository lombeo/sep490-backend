using Microsoft.EntityFrameworkCore;
using Sep490_Backend.DTO;
using Sep490_Backend.DTO.AdminDTO;
using Sep490_Backend.Infra;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Services.AuthenService;

namespace Sep490_Backend.Services.AdminService
{
    public interface IAdminService
    {
        Task<List<UserDTO>> ListUser(AdminSearchUserDTO model);
        Task<bool> DeleteUser(int userId, int actionBy);
    }

    public class AdminService : IAdminService
    {
        private readonly BackendContext _context;
        private readonly IAuthenService _authenService;

        public AdminService(BackendContext context, IAuthenService authenService)
        {
            _context = context;
            _authenService = authenService;
        }

        public async Task<bool> DeleteUser(int userId, int actionBy)
        {
            if (!IsAdmin(actionBy))
            {
                throw new ApplicationException(Message.CommonMessage.NOT_ALLOWED);
            }
            if(userId == actionBy || IsAdmin(userId))
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
                    FullName = userProfile[i].FullName,
                    Phone = userProfile[i].Phone,
                    Gender = userProfile[i].Gender,
                    CreatedAt = user[i].CreatedAt,
                    UpdatedAt = userProfile[i].UpdatedAt
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
            if(model.Gender != null)
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
