using Microsoft.EntityFrameworkCore;
using Sep490_Backend.Infra;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Infra.Helps;
using Sep490_Backend.Services.HelperService;
using Sep490_Backend.Services.DataService;
using Sep490_Backend.Services.CacheService;
using Sep490_Backend.DTO.Common;
using Sep490_Backend.DTO.ConstructionTeam;
using System.Text.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.Linq;
using Sep490_Backend.Controllers;

namespace Sep490_Backend.Services.ConstructionTeamService
{
    public interface IConstructionTeamService
    {
        Task<ConstructionTeam> Save(ConstructionTeamSaveDTO model, int actionBy);
        Task<bool> Delete(int teamId, int actionBy);
        Task<bool> RemoveMemberFromTeam(int memberId, int actionBy);
    }

    public class ConstructionTeamService : IConstructionTeamService
    {
        private readonly BackendContext _context;
        private readonly IHelperService _helperService;
        private readonly ICacheService _cacheService;
        private readonly IDataService _dataService;

        public ConstructionTeamService(
            BackendContext context,
            IHelperService helperService,
            ICacheService cacheService,
            IDataService dataService)
        {
            _context = context;
            _helperService = helperService;
            _cacheService = cacheService;
            _dataService = dataService;
        }

        /// <summary>
        /// Creates or updates a construction team
        /// </summary>
        /// <param name="model">ConstructionTeamSaveDTO model with data to save</param>
        /// <param name="actionBy">ID of the user performing the action</param>
        /// <returns>The saved construction team entity</returns>
        public async Task<ConstructionTeam> Save(ConstructionTeamSaveDTO model, int actionBy)
        {
            var errors = new List<ResponseError>();

            // Authorization check - only Construction Manager can manage teams
            if (!_helperService.IsInRole(actionBy, RoleConstValue.CONSTRUCTION_MANAGER))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            // For update: verify that the user is the creator of the team
            if (model.Id.HasValue && model.Id.Value > 0)
            {
                var existingTeam = await _context.ConstructionTeams
                    .FirstOrDefaultAsync(t => t.Id == model.Id.Value && !t.Deleted);
                
                if (existingTeam != null && existingTeam.Creator != actionBy)
                {
                    throw new UnauthorizedAccessException(Message.ConstructionTeamMessage.ONLY_CREATOR_CAN_UPDATE);
                }
            }

            // Validation
            if (string.IsNullOrWhiteSpace(model.TeamName))
            {
                errors.Add(new ResponseError
                {
                    Message = Message.ConstructionTeamMessage.NAME_REQUIRED,
                    Field = nameof(model.TeamName).ToCamelCase()
                });
            }

            if (model.TeamManager <= 0)
            {
                errors.Add(new ResponseError
                {
                    Message = Message.CommonMessage.MISSING_PARAM,
                    Field = nameof(model.TeamManager).ToCamelCase()
                });
            }
            else
            {
                // Verify the manager exists and has appropriate role
                var manager = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id == model.TeamManager && !u.Deleted);

                if (manager == null)
                {
                    errors.Add(new ResponseError
                    {
                        Message = Message.ConstructionTeamMessage.MANAGER_NOT_FOUND,
                        Field = nameof(model.TeamManager).ToCamelCase()
                    });
                }
                else if (!_helperService.IsInRole(manager.Id, RoleConstValue.TEAM_LEADER))
                {
                    errors.Add(new ResponseError
                    {
                        Message = Message.ConstructionTeamMessage.MANAGER_ROLE_REQUIRED,
                        Field = nameof(model.TeamManager).ToCamelCase()
                    });
                }
                
                // Check if manager is already assigned to another team (except current team)
                var existingTeam = await _context.ConstructionTeams
                    .FirstOrDefaultAsync(t => t.TeamManager == model.TeamManager && (model.Id == null || t.Id != model.Id) && !t.Deleted);
                
                if (existingTeam != null)
                {
                    errors.Add(new ResponseError
                    {
                        Message = Message.ConstructionTeamMessage.MANAGER_ALREADY_ASSIGNED,
                        Field = nameof(model.TeamManager).ToCamelCase()
                    });
                }
            }

            // Check for duplicate team name
            var duplicateTeam = await _context.ConstructionTeams
                .FirstOrDefaultAsync(t => t.TeamName == model.TeamName && (model.Id == null || t.Id != model.Id) && !t.Deleted);

            if (duplicateTeam != null)
            {
                errors.Add(new ResponseError
                {
                    Message = Message.ConstructionTeamMessage.DUPLICATE_NAME,
                    Field = nameof(model.TeamName).ToCamelCase()
                });
            }

            // Validate team members if provided
            if (model.TeamMemberIds != null && model.TeamMemberIds.Any())
            {
                // Check if the manager is also included in team members
                if (model.TeamMemberIds.Contains(model.TeamManager))
                {
                    errors.Add(new ResponseError
                    {
                        Message = Message.ConstructionTeamMessage.MANAGER_IN_MEMBERS,
                        Field = nameof(model.TeamMemberIds).ToCamelCase()
                    });
                }

                // Check if all team members exist and are not already in other teams
                var memberIds = model.TeamMemberIds.Distinct().ToList();
                var existingMembers = await _context.Users
                    .Where(u => memberIds.Contains(u.Id) && !u.Deleted)
                    .ToListAsync();

                if (existingMembers.Count != memberIds.Count)
                {
                    var foundIds = existingMembers.Select(u => u.Id);
                    var missingIds = memberIds.Where(id => !foundIds.Contains(id));
                    
                    errors.Add(new ResponseError
                    {
                        Message = Message.ConstructionTeamMessage.MEMBERS_NOT_FOUND,
                        Field = nameof(model.TeamMemberIds).ToCamelCase()
                    });
                }

                // Check if any members are already in other teams
                var membersInOtherTeams = existingMembers
                    .Where(u => u.TeamId.HasValue && (model.Id == null || u.TeamId != model.Id))
                    .ToList();

                if (membersInOtherTeams.Any())
                {
                    var conflictingUserNames = string.Join(", ", 
                        membersInOtherTeams.Select(u => u.FullName));
                    
                    errors.Add(new ResponseError
                    {
                        Message = Message.ConstructionTeamMessage.MEMBERS_IN_OTHER_TEAMS,
                        Field = nameof(model.TeamMemberIds).ToCamelCase()
                    });
                }
                
                // Check if all team members have Construction Employee role
                var membersWithInvalidRole = new List<int>();
                foreach (var member in existingMembers)
                {
                    if (!_helperService.IsInRole(member.Id, RoleConstValue.CONSTRUCTION_EMPLOYEE))
                    {
                        membersWithInvalidRole.Add(member.Id);
                    }
                }
                
                if (membersWithInvalidRole.Any())
                {
                    errors.Add(new ResponseError
                    {
                        Message = Message.ConstructionTeamMessage.INVALID_MEMBER_ROLE,
                        Field = nameof(model.TeamMemberIds).ToCamelCase()
                    });
                }
            }

            // Throw aggregated errors
            if (errors.Count > 0)
                throw new ValidationException(errors);

            // Begin transaction
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                ConstructionTeam teamEntity;
                
                // If ID is provided, update existing team
                if (model.Id.HasValue && model.Id.Value > 0)
                {
                    teamEntity = await _context.ConstructionTeams
                        .Include(t => t.Members)
                        .FirstOrDefaultAsync(t => t.Id == model.Id.Value && !t.Deleted);

                    if (teamEntity == null)
                    {
                        throw new KeyNotFoundException(Message.ConstructionTeamMessage.NOT_FOUND);
                    }

                    // Handle team manager change - update previous team members if manager changes
                    if (teamEntity.TeamManager != model.TeamManager)
                    {
                        // Get previous manager and remove team association
                        var previousManager = await _context.Users
                            .FirstOrDefaultAsync(u => u.Id == teamEntity.TeamManager);
                            
                        if (previousManager != null)
                        {
                            previousManager.TeamId = null;
                            _context.Users.Update(previousManager);
                        }
                        
                        // Set the new manager's TeamId to this team
                        var newManager = await _context.Users
                            .FirstOrDefaultAsync(u => u.Id == model.TeamManager);
                            
                        if (newManager != null)
                        {
                            newManager.TeamId = teamEntity.Id;
                            _context.Users.Update(newManager);
                        }
                    }

                    // Update properties
                    teamEntity.TeamName = model.TeamName;
                    teamEntity.TeamManager = model.TeamManager;
                    teamEntity.Description = model.Description ?? "";
                    
                    // Update audit fields
                    teamEntity.UpdatedAt = DateTime.Now;
                    teamEntity.Updater = actionBy;

                    _context.ConstructionTeams.Update(teamEntity);
                    await _context.SaveChangesAsync();
                    
                    // Get current team members except the manager (manager is also considered a team member)
                    var currentMemberIds = teamEntity.Members
                        .Where(m => m.Id != model.TeamManager)
                        .Select(m => m.Id)
                        .ToList();
                    
                    // Identify members to add and remove
                    var memberIdsToAdd = model.TeamMemberIds
                        .Where(id => !currentMemberIds.Contains(id))
                        .ToList();
                    
                    var memberIdsToRemove = currentMemberIds
                        .Where(id => !model.TeamMemberIds.Contains(id))
                        .ToList();
                        
                    // Remove users from team
                    if (memberIdsToRemove.Any())
                    {
                        var usersToRemove = await _context.Users
                            .Where(u => memberIdsToRemove.Contains(u.Id))
                            .ToListAsync();
                        
                        foreach (var user in usersToRemove)
                        {
                            user.TeamId = null;
                            _context.Users.Update(user);
                        }
                        await _context.SaveChangesAsync();
                    }
                    
                    // Add new users to team
                    if (memberIdsToAdd.Any())
                    {
                        var usersToAdd = await _context.Users
                            .Where(u => memberIdsToAdd.Contains(u.Id))
                            .ToListAsync();
                        
                        foreach (var user in usersToAdd)
                        {
                            user.TeamId = teamEntity.Id;
                            _context.Users.Update(user);
                        }
                        await _context.SaveChangesAsync();
                    }
                }
                else
                {
                    // Create new construction team
                    teamEntity = new ConstructionTeam
                    {
                        TeamName = model.TeamName,
                        TeamManager = model.TeamManager,
                        Description = model.Description ?? "",
                        
                        // Set audit fields
                        Creator = actionBy,
                        Updater = actionBy,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };

                    await _context.ConstructionTeams.AddAsync(teamEntity);
                    await _context.SaveChangesAsync();
                    
                    // Set the manager's TeamId to this team
                    var manager = await _context.Users
                        .FirstOrDefaultAsync(u => u.Id == model.TeamManager);
                        
                    if (manager != null)
                    {
                        manager.TeamId = teamEntity.Id;
                        _context.Users.Update(manager);
                        await _context.SaveChangesAsync();
                    }
                    
                    // Add team members if any are specified
                    if (model.TeamMemberIds.Any())
                    {
                        var membersToAdd = await _context.Users
                            .Where(u => model.TeamMemberIds.Contains(u.Id))
                            .ToListAsync();
                            
                        foreach (var member in membersToAdd)
                        {
                            member.TeamId = teamEntity.Id;
                            _context.Users.Update(member);
                        }
                        await _context.SaveChangesAsync();
                    }
                    }
                    
                    await transaction.CommitAsync();
                    
                    // Invalidate cache if needed
                    await InvalidateTeamCache();

                return teamEntity;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Deletes a construction team (soft delete)
        /// </summary>
        /// <param name="teamId">ID of the team to delete</param>
        /// <param name="actionBy">ID of the user performing the action</param>
        /// <returns>True if deletion was successful, otherwise false</returns>
        public async Task<bool> Delete(int teamId, int actionBy)
        {
            // Authorization check - only Construction Manager can delete teams
            if (!_helperService.IsInRole(actionBy, RoleConstValue.CONSTRUCTION_MANAGER))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            // Verify that the user is the creator of the team
            var team = await _context.ConstructionTeams
                .FirstOrDefaultAsync(t => t.Id == teamId && !t.Deleted);
            
            if (team == null)
            {
                throw new KeyNotFoundException(Message.ConstructionTeamMessage.NOT_FOUND);
            }
            
            if (team.Creator != actionBy)
            {
                throw new UnauthorizedAccessException(Message.ConstructionTeamMessage.ONLY_CREATOR_CAN_DELETE);
            }

            // Find the team with its members and plan items to check if it's in use
            var teamWithRelations = await _context.ConstructionTeams
                .Include(t => t.Members)
                .Include(t => t.ConstructPlanItems)
                .FirstOrDefaultAsync(t => t.Id == teamId && !t.Deleted);

            // Check if the team is assigned to construction plan items
            if (teamWithRelations.ConstructPlanItems != null && teamWithRelations.ConstructPlanItems.Any())
            {
                throw new InvalidOperationException(Message.ConstructionTeamMessage.TEAM_IN_USE);
            }

            // Update all members to remove team association
            foreach (var member in teamWithRelations.Members)
            {
                member.TeamId = null;
                _context.Users.Update(member);
            }
            await _context.SaveChangesAsync();

            // Use the extension method for soft delete
            await _context.SoftDeleteAsync(team, actionBy);

            // Invalidate all related caches
            await InvalidateTeamCaches(teamId);

            return true;
        }
        
        /// <summary>
        /// Removes a member from their construction team
        /// </summary>
        /// <param name="memberId">ID of the user to remove from team</param>
        /// <param name="actionBy">ID of the user performing the action</param>
        /// <returns>True if removal was successful, otherwise false</returns>
        public async Task<bool> RemoveMemberFromTeam(int memberId, int actionBy)
        {
            // Authorization check - only Construction Manager can remove team members
            if (!_helperService.IsInRole(actionBy, RoleConstValue.CONSTRUCTION_MANAGER))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }
            
            // Find the user to remove
            var member = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == memberId && !u.Deleted);
                
            if (member == null)
            {
                throw new KeyNotFoundException(Message.ConstructionTeamMessage.MEMBER_NOT_FOUND);
            }
            
            // Check if user is in a team
            if (!member.TeamId.HasValue)
            {
                throw new InvalidOperationException(Message.ConstructionTeamMessage.MEMBER_NOT_IN_TEAM);
            }
            
            // Find the team to verify team manager
            var team = await _context.ConstructionTeams
                .FirstOrDefaultAsync(t => t.Id == member.TeamId.Value && !t.Deleted);
                
            if (team == null)
            {
                throw new KeyNotFoundException(Message.ConstructionTeamMessage.NOT_FOUND);
            }
            
            // Check if user is the team manager - managers should be changed through Update API
            if (team.TeamManager == memberId)
            {
                throw new InvalidOperationException(Message.ConstructionTeamMessage.CANNOT_REMOVE_MANAGER);
            }
            
            // Check if actionBy is the creator of the team
            if (team.Creator != actionBy)
            {
                throw new UnauthorizedAccessException(Message.ConstructionTeamMessage.ONLY_CREATOR_CAN_UPDATE);
            }
            
            // Remove user from team
            member.TeamId = null;
            _context.Users.Update(member);
            await _context.SaveChangesAsync();
            
            // Invalidate caches
            await InvalidateTeamCaches(team.Id);

            return true;
        }

        /// <summary>
        /// Invalidates all team-related caches 
        /// </summary>
        private async Task InvalidateTeamCaches(int teamId)
        {
            // Main construction team cache
            await _cacheService.DeleteAsync(RedisCacheKey.CONSTRUCTION_TEAM_CACHE_KEY);
            
            // Team-specific cache if it exists
            await _cacheService.DeleteAsync($"CONSTRUCTION_TEAM:{teamId}");
            
            // User caches since members had their TeamId updated
            await _cacheService.DeleteAsync("USER_CACHE_KEY");
            
            // Construction plan caches, as they may include team information
            await _cacheService.DeleteAsync(RedisCacheKey.CONSTRUCTION_PLAN_CACHE_KEY);
            
            // Any pattern-based caches
            await _cacheService.DeleteByPatternAsync("CONSTRUCTION_TEAM:*");
        }

        /// <summary>
        /// Invalidates team-related cache
        /// </summary>
        private async Task InvalidateTeamCache()
        {
            await _cacheService.DeleteAsync(RedisCacheKey.CONSTRUCTION_TEAM_CACHE_KEY);
        }
    }
}
