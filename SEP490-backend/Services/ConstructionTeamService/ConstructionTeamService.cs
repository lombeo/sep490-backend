using Microsoft.EntityFrameworkCore;
using Sep490_Backend.Infra;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Infra.Helps;
using Sep490_Backend.Services.HelperService;
using Sep490_Backend.Services.DataService;
using Sep490_Backend.Services.CacheService;
using Sep490_Backend.DTO.Common;
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
        Task<ConstructionTeam> Save(ConstructionTeam model, int actionBy);
        Task<bool> Delete(int teamId, int actionBy);
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
        /// <param name="model">ConstructionTeam model with data to save</param>
        /// <param name="actionBy">ID of the user performing the action</param>
        /// <returns>The saved construction team entity</returns>
        public async Task<ConstructionTeam> Save(ConstructionTeam model, int actionBy)
        {
            var errors = new List<ResponseError>();

            // Authorization check - only Construction Manager can manage teams
            if (!_helperService.IsInRole(actionBy, RoleConstValue.CONSTRUCTION_MANAGER))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            // For update: verify that the user is the creator of the team
            if (model.Id > 0)
            {
                var existingTeam = await _context.ConstructionTeams
                    .FirstOrDefaultAsync(t => t.Id == model.Id && !t.Deleted);
                
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
                        Message = "Team manager not found",
                        Field = nameof(model.TeamManager).ToCamelCase()
                    });
                }
                else if (!_helperService.IsInRole(manager.Id, RoleConstValue.TEAM_LEADER))
                {
                    errors.Add(new ResponseError
                    {
                        Message = "Selected user must have Team Leader role",
                        Field = nameof(model.TeamManager).ToCamelCase()
                    });
                }
                
                // Check if manager is already assigned to another team (except current team)
                var existingTeam = await _context.ConstructionTeams
                    .FirstOrDefaultAsync(t => t.TeamManager == model.TeamManager && t.Id != model.Id && !t.Deleted);
                
                if (existingTeam != null)
                {
                    errors.Add(new ResponseError
                    {
                        Message = "This user is already managing another team",
                        Field = nameof(model.TeamManager).ToCamelCase()
                    });
                }
            }

            // Check for duplicate team name
            var duplicateTeam = await _context.ConstructionTeams
                .FirstOrDefaultAsync(t => t.TeamName == model.TeamName && t.Id != model.Id && !t.Deleted);

            if (duplicateTeam != null)
            {
                errors.Add(new ResponseError
                {
                    Message = Message.ConstructionTeamMessage.DUPLICATE_NAME,
                    Field = nameof(model.TeamName).ToCamelCase()
                });
            }

            // Throw aggregated errors
            if (errors.Count > 0)
                throw new ValidationException(errors);

            // Begin transaction
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                // If ID is provided, update existing team
                if (model.Id > 0)
                {
                    var teamToUpdate = await _context.ConstructionTeams
                        .Include(t => t.Members)
                        .FirstOrDefaultAsync(t => t.Id == model.Id && !t.Deleted);

                    if (teamToUpdate == null)
                    {
                        throw new KeyNotFoundException(Message.ConstructionTeamMessage.NOT_FOUND);
                    }

                    // Handle team manager change - update previous team members if manager changes
                    if (teamToUpdate.TeamManager != model.TeamManager)
                    {
                        // Get previous manager and remove team association
                        var previousManager = await _context.Users
                            .FirstOrDefaultAsync(u => u.Id == teamToUpdate.TeamManager);
                            
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
                            newManager.TeamId = teamToUpdate.Id;
                            _context.Users.Update(newManager);
                        }
                    }

                    // Update properties
                    teamToUpdate.TeamName = model.TeamName;
                    teamToUpdate.TeamManager = model.TeamManager;
                    teamToUpdate.Description = model.Description;
                    
                    // Update audit fields
                    teamToUpdate.UpdatedAt = DateTime.Now;
                    teamToUpdate.Updater = actionBy;

                    _context.ConstructionTeams.Update(teamToUpdate);
                    await _context.SaveChangesAsync();
                    
                    await transaction.CommitAsync();
                    
                    // Invalidate cache if needed
                    await InvalidateTeamCache();

                    return teamToUpdate;
                }
                else
                {
                    // Create new construction team
                    var newTeam = new ConstructionTeam
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

                    await _context.ConstructionTeams.AddAsync(newTeam);
                    await _context.SaveChangesAsync();
                    
                    // Set the manager's TeamId to this team
                    var manager = await _context.Users
                        .FirstOrDefaultAsync(u => u.Id == model.TeamManager);
                        
                    if (manager != null)
                    {
                        manager.TeamId = newTeam.Id;
                        _context.Users.Update(manager);
                        await _context.SaveChangesAsync();
                    }
                    
                    await transaction.CommitAsync();
                    
                    // Invalidate cache if needed
                    await InvalidateTeamCache();

                    return newTeam;
                }
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

            // Begin transaction
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                // Find the team with its members and plan items
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

                // Perform soft delete
                team.Deleted = true;
                team.UpdatedAt = DateTime.Now;
                team.Updater = actionBy;

                _context.ConstructionTeams.Update(team);
                await _context.SaveChangesAsync();
                
                await transaction.CommitAsync();

                // Invalidate cache
                await InvalidateTeamCache();

                return true;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
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
