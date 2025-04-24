using Microsoft.EntityFrameworkCore;
using Sep490_Backend.Controllers;
using Sep490_Backend.DTO;
using Sep490_Backend.DTO.Common;
using Sep490_Backend.DTO.ResourceReqs;
using Sep490_Backend.Infra;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Infra.Enums;
using Sep490_Backend.Infra.Helps;
using Sep490_Backend.Services.CacheService;
using Sep490_Backend.Services.DataService;
using Sep490_Backend.Services.HelperService;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Sep490_Backend.Services.ResourceReqService
{
    public interface IResourceReqService
    {
        Task<ResourceAllocationReqs> SaveResourceAllocationReq(SaveResourceAllocationReqDTO model, int actionBy);
        Task<ResourceMobilizationReqs> SaveResourceMobilizationReq(SaveResourceMobilizationReqDTO model, int actionBy);
        Task<bool> DeleteResourceAllocationReq(int reqId, int actionBy);
        Task<bool> DeleteResourceMobilizationReq(int reqId, int actionBy);
        
        // Updated method signatures to use BaseQuery instead of PagedResponseDTO
        Task<List<ResourceMobilizationReqs>> ViewResourceMobilizationRequests(int projectId, RequestStatus? status, MobilizationRequestType? requestType, string? searchTerm, BaseQuery query);
        Task<ResourceMobilizationReqs> GetResourceMobilizationRequestById(int id);
        Task<ResourceMobilizationReqs> SendResourceMobilizationRequest(int reqId, int actionBy);
        Task<ResourceMobilizationReqs> ApproveResourceMobilizationRequest(int reqId, string comments, int actionBy);
        Task<ResourceMobilizationReqs> RejectResourceMobilizationRequest(int reqId, string reason, int actionBy);
        
        // Cache invalidation methods
        Task InvalidateMobilizationCacheForProject(int projectId);
        Task InvalidateAllMobilizationCaches();
        
        Task<List<ResourceInventoryDTO>> ViewInventoryResources(ResourceType? type, int? projectId, BaseQuery query);
        Task<ResourceInventoryDTO> GetInventoryResourceById(int id);
        Task<ResourceInventory> AddInventoryResource(AddResourceInventoryDTO model, int actionBy);
        Task<ResourceInventory> UpdateInventoryResource(UpdateResourceInventoryDTO model, int actionBy);
        Task<bool> DeleteInventoryResource(int resourceId, int actionBy);
        
        Task<List<ResourceAllocationReqs>> ViewResourceAllocationRequests(int? fromProjectId, int? toProjectId, RequestStatus? status, int? requestType, string? searchTerm, BaseQuery query);
        Task<ResourceAllocationReqs> GetResourceAllocationRequestById(int id);
        Task<ResourceAllocationReqs> SendResourceAllocationRequest(int reqId, int actionBy);
        Task<ResourceAllocationReqs> ApproveResourceAllocationRequest(int reqId, string comments, int actionBy);
        Task<ResourceAllocationReqs> RejectResourceAllocationRequest(int reqId, string reason, int actionBy);
    }

    public class ResourceReqService : IResourceReqService
    {
        private readonly BackendContext _context;
        private readonly ICacheService _cacheService;
        private readonly IHelperService _helperService;
        private readonly IDataService _dataService;

        public ResourceReqService(
            BackendContext context,
            ICacheService cacheService,
            IHelperService helperService,
            IDataService dataService)
        {
            _context = context;
            _cacheService = cacheService;
            _helperService = helperService;
            _dataService = dataService;
        }

        /// <summary>
        /// Creates or updates a resource allocation request
        /// </summary>
        /// <param name="model">SaveResourceAllocationReqDTO with data to save</param>
        /// <param name="actionBy">ID of the user performing the action</param>
        /// <returns>The saved resource allocation request entity</returns>
        public async Task<ResourceAllocationReqs> SaveResourceAllocationReq(SaveResourceAllocationReqDTO model, int actionBy)
        {
            // Validate user role - only Resource Manager can create or update resource allocation requests
            if (!_helperService.IsInRole(actionBy, RoleConstValue.RESOURCE_MANAGER))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            var errors = new List<ResponseError>();

            // For updates, verify the user is the creator
            if (model.Id.HasValue && model.Id.Value > 0)
            {
                var existingReq = await _context.ResourceAllocationReqs
                    .FirstOrDefaultAsync(r => r.Id == model.Id.Value && !r.Deleted);
                
                if (existingReq != null && existingReq.Creator != actionBy)
                {
                    throw new UnauthorizedAccessException(Message.ResourceRequestMessage.ONLY_CREATOR_CAN_MODIFY);
                }
            }

            // Validate projects - both source and destination projects must exist
            var fromProject = await _context.Projects.FirstOrDefaultAsync(p => p.Id == model.FromProjectId && !p.Deleted);
            if (fromProject == null)
            {
                errors.Add(new ResponseError { Field = "FromProjectId", Message = Message.ResourceRequestMessage.SOURCE_PROJECT_NOT_FOUND });
            }

            var toProject = await _context.Projects.FirstOrDefaultAsync(p => p.Id == model.ToProjectId && !p.Deleted);
            if (toProject == null)
            {
                errors.Add(new ResponseError { Field = "ToProjectId", Message = Message.ResourceRequestMessage.DESTINATION_PROJECT_NOT_FOUND });
            }

            // Cannot allocate resources from a project to itself unless using tasks
            if (model.FromProjectId == model.ToProjectId && model.RequestType == 1) // PROJECT_TO_PROJECT
            {
                errors.Add(new ResponseError { Field = "ToProjectId", Message = Message.ResourceRequestMessage.INVALID_PROJECT_SELECTION });
            }

            // Validate request type and task IDs
            if (model.RequestType < 1 || model.RequestType > 3)
            {
                errors.Add(new ResponseError { Field = "RequestType", Message = "Invalid request type. Must be 1 (Project to Project), 2 (Project to Task), or 3 (Task to Task)." });
            }

            // Validate task IDs based on request type
            if (model.RequestType == 2) // PROJECT_TO_TASK
            {
                if (!model.ToTaskId.HasValue)
                {
                    errors.Add(new ResponseError { Field = "ToTaskId", Message = "Destination task ID is required for Project to Task allocation." });
                }
            }
            else if (model.RequestType == 3) // TASK_TO_TASK
            {
                if (!model.FromTaskId.HasValue)
                {
                    errors.Add(new ResponseError { Field = "FromTaskId", Message = "Source task ID is required for Task to Task allocation." });
                }
                if (!model.ToTaskId.HasValue)
                {
                    errors.Add(new ResponseError { Field = "ToTaskId", Message = "Destination task ID is required for Task to Task allocation." });
                }
            }

            // Validate resource details
            if (model.ResourceAllocationDetails == null || !model.ResourceAllocationDetails.Any())
            {
                errors.Add(new ResponseError { Field = "ResourceAllocationDetails", Message = Message.ResourceRequestMessage.MISSING_RESOURCE_DETAILS });
            }
            else
            {
                // Validate each resource detail
                for (int i = 0; i < model.ResourceAllocationDetails.Count; i++)
                {
                    var detail = model.ResourceAllocationDetails[i];
                    
                    // Check resource type
                    if (detail.ResourceType == ResourceType.NONE)
                    {
                        errors.Add(new ResponseError 
                        { 
                            Field = $"ResourceAllocationDetails[{i}].ResourceType", 
                            Message = Message.ResourceRequestMessage.INVALID_RESOURCE_TYPE
                        });
                    }
                    
                    // Check quantity
                    if (detail.Quantity <= 0)
                    {
                        errors.Add(new ResponseError 
                        { 
                            Field = $"ResourceAllocationDetails[{i}].Quantity", 
                            Message = Message.ResourceRequestMessage.INVALID_QUANTITY
                        });
                    }
                    
                    // Set the correct detail type
                    detail.Type = RequestDetailType.Allocation;
                }
            }

            // If there are validation errors, throw exception
            if (errors.Any())
            {
                throw new ValidationException(errors);
            }

            // Begin transaction
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                if (model.Id.HasValue && model.Id.Value > 0)
                {
                    // Update existing resource allocation request
                    var reqToUpdate = await _context.ResourceAllocationReqs
                        .FirstOrDefaultAsync(r => r.Id == model.Id.Value && !r.Deleted);

                    if (reqToUpdate == null)
                    {
                        throw new KeyNotFoundException(Message.ResourceRequestMessage.REQUEST_NOT_FOUND);
                    }

                    // Check if the request is in a state that can be updated
                    if (reqToUpdate.Status != RequestStatus.Draft && reqToUpdate.Status != RequestStatus.Reject)
                    {
                        throw new ArgumentException(Message.ResourceRequestMessage.ONLY_DRAFT_CAN_BE_UPDATED);
                    }

                    // Update properties
                    reqToUpdate.RequestType = model.RequestType;
                    reqToUpdate.FromProjectId = model.FromProjectId;
                    reqToUpdate.ToProjectId = model.ToProjectId;
                    reqToUpdate.FromTaskId = model.FromTaskId;
                    reqToUpdate.ToTaskId = model.ToTaskId;
                    reqToUpdate.RequestName = model.RequestName;
                    reqToUpdate.ResourceAllocationDetails = model.ResourceAllocationDetails;
                    reqToUpdate.Description = model.Description;
                    reqToUpdate.PriorityLevel = model.PriorityLevel;
                    reqToUpdate.Status = model.Status;
                    reqToUpdate.Attachments = model.Attachments;
                    reqToUpdate.RequestDate = model.RequestDate;
                    
                    // Update audit fields
                    reqToUpdate.UpdatedAt = DateTime.UtcNow;
                    reqToUpdate.Updater = actionBy;

                    _context.ResourceAllocationReqs.Update(reqToUpdate);
                    await _context.SaveChangesAsync();
                    
                    await transaction.CommitAsync();
                    
                    // Invalidate cache
                    await InvalidateResourceAllocationReqCache(reqToUpdate.Id, reqToUpdate.FromProjectId, reqToUpdate.ToProjectId);

                    return reqToUpdate;
                }
                else
                {
                    // Generate a unique request code
                    string requestCode = $"AL-{DateTime.Now.ToString("yyyyMMddHHmmss")}-{Guid.NewGuid().ToString().Substring(0, 8)}";

                    // Create new resource allocation request
                    var newReq = new ResourceAllocationReqs
                    {
                        RequestCode = requestCode,
                        RequestType = model.RequestType,
                        FromProjectId = model.FromProjectId,
                        ToProjectId = model.ToProjectId,
                        FromTaskId = model.FromTaskId,
                        ToTaskId = model.ToTaskId,
                        RequestName = model.RequestName,
                        ResourceAllocationDetails = model.ResourceAllocationDetails,
                        Description = model.Description,
                        PriorityLevel = model.PriorityLevel,
                        Status = RequestStatus.Draft, // New requests always start as Draft
                        Attachments = model.Attachments,
                        RequestDate = model.RequestDate,
                        
                        // Set audit fields
                        Creator = actionBy,
                        Updater = actionBy,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    await _context.ResourceAllocationReqs.AddAsync(newReq);
                    await _context.SaveChangesAsync();
                    
                    await transaction.CommitAsync();
                    
                    // Invalidate cache after adding new resource allocation request
                    await InvalidateResourceAllocationReqCache(newReq.Id, newReq.FromProjectId, newReq.ToProjectId);

                    return newReq;
                }
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Creates or updates a resource mobilization request
        /// </summary>
        /// <param name="model">SaveResourceMobilizationReqDTO with data to save</param>
        /// <param name="actionBy">ID of the user performing the action</param>
        /// <returns>The saved resource mobilization request entity</returns>
        public async Task<ResourceMobilizationReqs> SaveResourceMobilizationReq(SaveResourceMobilizationReqDTO model, int actionBy)
        {
            // Validate user role - only Resource Manager can create or update resource mobilization requests
            if (!_helperService.IsInRole(actionBy, RoleConstValue.RESOURCE_MANAGER))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            var errors = new List<ResponseError>();

            // For updates, verify the user is the creator
            if (model.Id.HasValue)
            {
                var existingReq = await _context.ResourceMobilizationReqs
                    .FirstOrDefaultAsync(r => r.Id == model.Id.Value && !r.Deleted);
                
                if (existingReq != null && existingReq.Creator != actionBy)
                {
                    throw new UnauthorizedAccessException(Message.ResourceRequestMessage.ONLY_CREATOR_CAN_MODIFY);
                }
            }

            // Validate project - project must exist
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == model.ProjectId && !p.Deleted);
            if (project == null)
            {
                errors.Add(new ResponseError { Field = "ProjectId", Message = Message.ResourceRequestMessage.PROJECT_NOT_FOUND });
            }

            // Validate resource details
            if (model.ResourceMobilizationDetails == null || !model.ResourceMobilizationDetails.Any())
            {
                errors.Add(new ResponseError { Field = "ResourceMobilizationDetails", Message = Message.ResourceRequestMessage.MISSING_RESOURCE_DETAILS });
            }
            else
            {
                // Validate each resource detail
                for (int i = 0; i < model.ResourceMobilizationDetails.Count; i++)
                {
                    var detail = model.ResourceMobilizationDetails[i];
                    
                    // Check resource type
                    if (detail.ResourceType == ResourceType.NONE)
                    {
                        errors.Add(new ResponseError 
                        { 
                            Field = $"ResourceMobilizationDetails[{i}].ResourceType", 
                            Message = Message.ResourceRequestMessage.INVALID_RESOURCE_TYPE
                        });
                    }
                    
                    // Check quantity
                    if (detail.Quantity <= 0)
                    {
                        errors.Add(new ResponseError 
                        { 
                            Field = $"ResourceMobilizationDetails[{i}].Quantity", 
                            Message = Message.ResourceRequestMessage.INVALID_QUANTITY
                        });
                    }
                    
                    // Set the correct detail type
                    detail.Type = RequestDetailType.Mobilization;
                }
            }

            // Validate request date
            if (model.RequestDate < DateTime.Today)
            {
                errors.Add(new ResponseError { Field = "RequestDate", Message = Message.ResourceRequestMessage.INVALID_REQUEST_DATE });
            }

            // Validate request type
            if (model.RequestType != MobilizationRequestType.SupplyMore && model.RequestType != MobilizationRequestType.AddNew)
            {
                errors.Add(new ResponseError { Field = "RequestType", Message = "Invalid request type" });
            }

            // If there are validation errors, throw exception
            if (errors.Any())
            {
                throw new ValidationException(errors);
            }

            // Begin transaction
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                if (model.Id.HasValue)
                {
                    // Update existing resource mobilization request
                    var reqToUpdate = await _context.ResourceMobilizationReqs
                        .FirstOrDefaultAsync(r => r.Id == model.Id.Value && !r.Deleted);

                    if (reqToUpdate == null)
                    {
                        throw new KeyNotFoundException(Message.ResourceRequestMessage.REQUEST_NOT_FOUND);
                    }

                    // Check if the request is in a state that can be updated
                    if (reqToUpdate.Status != RequestStatus.Draft && reqToUpdate.Status != RequestStatus.Reject)
                    {
                        throw new ArgumentException(Message.ResourceRequestMessage.ONLY_DRAFT_CAN_BE_UPDATED);
                    }

                    // Update properties
                    reqToUpdate.ProjectId = model.ProjectId;
                    reqToUpdate.RequestName = model.RequestName;
                    reqToUpdate.ResourceMobilizationDetails = model.ResourceMobilizationDetails;
                    reqToUpdate.Description = model.Description;
                    reqToUpdate.PriorityLevel = model.PriorityLevel;
                    reqToUpdate.Status = model.Status;
                    reqToUpdate.Attachments = model.Attachments;
                    reqToUpdate.RequestDate = model.RequestDate;
                    reqToUpdate.RequestType = model.RequestType;
                    
                    // Update audit fields
                    reqToUpdate.UpdatedAt = DateTime.UtcNow;
                    reqToUpdate.Updater = actionBy;

                    _context.ResourceMobilizationReqs.Update(reqToUpdate);
                    await _context.SaveChangesAsync();
                    
                    await transaction.CommitAsync();
                    
                    // Invalidate cache
                    await InvalidateResourceMobilizationReqCache(reqToUpdate.Id, reqToUpdate.ProjectId);

                    return reqToUpdate;
                }
                else
                {
                    // Generate a unique request code
                    string requestCode = $"MB-{DateTime.Now.ToString("yyyyMMddHHmmss")}-{Guid.NewGuid().ToString().Substring(0, 8)}";

                    // Create new resource mobilization request
                    var newReq = new ResourceMobilizationReqs
                    {
                        RequestCode = requestCode,
                        ProjectId = model.ProjectId,
                        RequestName = model.RequestName,
                        ResourceMobilizationDetails = model.ResourceMobilizationDetails,
                        Description = model.Description,
                        PriorityLevel = model.PriorityLevel,
                        Status = RequestStatus.Draft, // New requests always start as Draft
                        Attachments = model.Attachments,
                        RequestDate = model.RequestDate,
                        RequestType = model.RequestType,
                        
                        // Set audit fields
                        Creator = actionBy,
                        Updater = actionBy,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    await _context.ResourceMobilizationReqs.AddAsync(newReq);
                    await _context.SaveChangesAsync();
                    
                    await transaction.CommitAsync();
                    
                    // Invalidate cache after adding new resource mobilization request
                    await InvalidateResourceMobilizationReqCache(newReq.Id, newReq.ProjectId);

                    return newReq;
                }
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Deletes a resource allocation request (soft delete)
        /// </summary>
        /// <param name="reqId">ID of the request to delete</param>
        /// <param name="actionBy">ID of the user performing the action</param>
        /// <returns>True if deletion was successful</returns>
        public async Task<bool> DeleteResourceAllocationReq(int reqId, int actionBy)
        {
            // Validate user role - only Resource Manager can delete resource allocation requests
            if (!_helperService.IsInRole(actionBy, RoleConstValue.RESOURCE_MANAGER))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            // Find the resource allocation request
            var request = await _context.ResourceAllocationReqs
                .FirstOrDefaultAsync(r => r.Id == reqId && !r.Deleted);

            if (request == null)
            {
                throw new KeyNotFoundException(Message.ResourceRequestMessage.REQUEST_NOT_FOUND);
            }

            // Verify the user is the creator
            if (request.Creator != actionBy)
            {
                throw new UnauthorizedAccessException(Message.ResourceRequestMessage.ONLY_CREATOR_CAN_DELETE);
            }

            // Check if the request is in a state that can be deleted
            if (request.Status != RequestStatus.Draft && request.Status != RequestStatus.Reject)
            {
                throw new InvalidOperationException(Message.ResourceRequestMessage.ONLY_DRAFT_CAN_BE_DELETED);
            }

            // Use extension method for soft delete
            await _context.SoftDeleteAsync(request, actionBy);
            
            // Invalidate cache
            await InvalidateResourceAllocationReqCache(request.Id, request.FromProjectId, request.ToProjectId);

            return true;
        }

        /// <summary>
        /// Deletes a resource mobilization request (soft delete)
        /// </summary>
        /// <param name="reqId">ID of the request to delete</param>
        /// <param name="actionBy">ID of the user performing the action</param>
        /// <returns>True if deletion was successful</returns>
        public async Task<bool> DeleteResourceMobilizationReq(int reqId, int actionBy)
        {
            // Validate user role - only Resource Manager can delete resource mobilization requests
            if (!_helperService.IsInRole(actionBy, RoleConstValue.RESOURCE_MANAGER))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            // Find the resource mobilization request
            var request = await _context.ResourceMobilizationReqs
                .FirstOrDefaultAsync(r => r.Id == reqId && !r.Deleted);

            if (request == null)
            {
                throw new KeyNotFoundException(Message.ResourceRequestMessage.REQUEST_NOT_FOUND);
            }

            // Verify the user is the creator
            if (request.Creator != actionBy)
            {
                throw new UnauthorizedAccessException(Message.ResourceRequestMessage.ONLY_CREATOR_CAN_DELETE);
            }

            // Check if the request is in a state that can be deleted
            if (request.Status != RequestStatus.Draft && request.Status != RequestStatus.Reject)
            {
                throw new InvalidOperationException(Message.ResourceRequestMessage.ONLY_DRAFT_CAN_BE_DELETED);
            }

            // Use our extension method to soft delete the entity
            await _context.SoftDeleteAsync(request, actionBy);
            
            // Invalidate cache
            await InvalidateResourceMobilizationReqCache(request.Id, request.ProjectId);

            return true;
        }

        /// <summary>
        /// Invalidates resource allocation request related cache
        /// </summary>
        private async Task InvalidateResourceAllocationReqCache(int reqId, int fromProjectId, int toProjectId)
        {
            // Delete specific request cache
            await _cacheService.DeleteAsync(string.Format(RedisCacheKey.ALLOCATION_REQ_CACHE_KEY, reqId));
            
            // Delete request detail cache
            await _cacheService.DeleteAsync(string.Format(RedisCacheKey.RESOURCE_ALLOCATION_REQ_BY_ID_CACHE_KEY, reqId));
            
            // Delete project-related caches
            await _cacheService.DeleteAsync(string.Format(RedisCacheKey.ALLOCATION_REQ_BY_PROJECT_CACHE_KEY, fromProjectId));
            await _cacheService.DeleteAsync(string.Format(RedisCacheKey.ALLOCATION_REQ_BY_PROJECT_CACHE_KEY, toProjectId));
            
            // Delete list caches
            await _cacheService.DeleteAsync(RedisCacheKey.ALLOCATION_REQS_LIST_CACHE_KEY);
            
            // Use pattern-based deletion for project-specific caches
            string fromProjectPattern = string.Format(RedisCacheKey.ALLOCATION_REQS_BY_FROM_PROJECT_LIST_CACHE_KEY, fromProjectId);
            await _cacheService.DeleteByPatternAsync(fromProjectPattern);
            
            string toProjectPattern = string.Format(RedisCacheKey.ALLOCATION_REQS_BY_TO_PROJECT_LIST_CACHE_KEY, toProjectId);
            await _cacheService.DeleteByPatternAsync(toProjectPattern);
            
            // Delete all status-based caches using pattern matching
            await _cacheService.DeleteByPatternAsync(RedisCacheKey.ALLOCATION_REQS_BY_STATUS_LIST_CACHE_KEY);
            
            // Clear all paginated caches using pattern matching
            await _cacheService.DeleteByPatternAsync(RedisCacheKey.ALLOCATION_REQS_LIST_CACHE_KEY);
        }

        /// <summary>
        /// Invalidates resource mobilization request related cache
        /// </summary>
        private async Task InvalidateResourceMobilizationReqCache(int reqId, int projectId)
        {
            // Delete specific request cache
            await _cacheService.DeleteAsync(string.Format(RedisCacheKey.MOBILIZATION_REQ_CACHE_KEY, reqId));
            
            // Delete request detail cache
            await _cacheService.DeleteAsync(string.Format(RedisCacheKey.RESOURCE_MOBILIZATION_REQ_BY_ID_CACHE_KEY, reqId));
            
            // Delete project-related cache
            await _cacheService.DeleteAsync(string.Format(RedisCacheKey.MOBILIZATION_REQ_BY_PROJECT_CACHE_KEY, projectId));
            
            // Delete list caches
            await _cacheService.DeleteAsync(RedisCacheKey.MOBILIZATION_REQS_LIST_CACHE_KEY);
            
            // Use pattern-based deletion for project-specific and paginated caches
            string projectPattern = string.Format(RedisCacheKey.MOBILIZATION_REQS_BY_PROJECT_LIST_CACHE_KEY, projectId);
            await _cacheService.DeleteByPatternAsync(projectPattern);
            
            // Delete all status-based caches using pattern matching
            await _cacheService.DeleteByPatternAsync(RedisCacheKey.MOBILIZATION_REQS_BY_STATUS_LIST_CACHE_KEY);
            
            // Clear all paginated caches for mobilization requests using pattern matching
            await _cacheService.DeleteByPatternAsync(RedisCacheKey.MOBILIZATION_REQS_LIST_CACHE_KEY);
        }

        #region Resource Mobilization Methods

        /// <summary>
        /// Retrieves all resource mobilization requests with optional filtering
        /// </summary>
        /// <param name="projectId">Optional filter by project ID</param>
        /// <param name="status">Optional filter by request status</param>
        /// <param name="requestType">Optional filter by request type</param>
        /// <param name="searchTerm">Optional search by request code or request name</param>
        /// <param name="query">BaseQuery object containing pagination parameters</param>
        /// <returns>List of resource mobilization requests</returns>
        public async Task<List<ResourceMobilizationReqs>> ViewResourceMobilizationRequests(
            int projectId, RequestStatus? status, MobilizationRequestType? requestType, string? searchTerm, BaseQuery query)
        {
            // Check authorization - this method is accessible by both Resource Manager, Technical Department, and Executive Board
            if (!_helperService.IsInRole(query.ActionBy, RoleConstValue.RESOURCE_MANAGER) && 
                !_helperService.IsInRole(query.ActionBy, RoleConstValue.TECHNICAL_MANAGER) &&
                !_helperService.IsInRole(query.ActionBy, RoleConstValue.EXECUTIVE_BOARD))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            // Build cache key based on parameters
            string cacheKey;
            if (projectId > 0 && status.HasValue && requestType.HasValue && !string.IsNullOrEmpty(searchTerm))
            {
                cacheKey = $"{RedisCacheKey.MOBILIZATION_REQS_BY_PROJECT_LIST_CACHE_KEY}:{status.Value}:{requestType.Value}:SEARCH:{searchTerm}:PAGE:{query.PageIndex}:SIZE:{query.PageSize}";
                cacheKey = string.Format(cacheKey, projectId);
            }
            else if (projectId > 0 && status.HasValue && requestType.HasValue)
            {
                cacheKey = $"{RedisCacheKey.MOBILIZATION_REQS_BY_PROJECT_LIST_CACHE_KEY}:{status.Value}:{requestType.Value}:PAGE:{query.PageIndex}:SIZE:{query.PageSize}";
                cacheKey = string.Format(cacheKey, projectId);
            }
            else if (projectId > 0 && status.HasValue && !string.IsNullOrEmpty(searchTerm))
            {
                cacheKey = $"{RedisCacheKey.MOBILIZATION_REQS_BY_PROJECT_LIST_CACHE_KEY}:{status.Value}:SEARCH:{searchTerm}:PAGE:{query.PageIndex}:SIZE:{query.PageSize}";
                cacheKey = string.Format(cacheKey, projectId);
            }
            else if (projectId > 0 && status.HasValue)
            {
                cacheKey = $"{RedisCacheKey.MOBILIZATION_REQS_BY_PROJECT_LIST_CACHE_KEY}:{status.Value}:PAGE:{query.PageIndex}:SIZE:{query.PageSize}";
                cacheKey = string.Format(cacheKey, projectId);
            }
            else if (projectId > 0 && requestType.HasValue && !string.IsNullOrEmpty(searchTerm))
            {
                cacheKey = $"{RedisCacheKey.MOBILIZATION_REQS_BY_PROJECT_LIST_CACHE_KEY}:{requestType.Value}:SEARCH:{searchTerm}:PAGE:{query.PageIndex}:SIZE:{query.PageSize}";
                cacheKey = string.Format(cacheKey, projectId);
            }
            else if (projectId > 0 && requestType.HasValue)
            {
                cacheKey = $"{RedisCacheKey.MOBILIZATION_REQS_BY_PROJECT_LIST_CACHE_KEY}:{requestType.Value}:PAGE:{query.PageIndex}:SIZE:{query.PageSize}";
                cacheKey = string.Format(cacheKey, projectId);
            }
            else if (projectId > 0 && !string.IsNullOrEmpty(searchTerm))
            {
                cacheKey = $"{RedisCacheKey.MOBILIZATION_REQS_BY_PROJECT_LIST_CACHE_KEY}:SEARCH:{searchTerm}:PAGE:{query.PageIndex}:SIZE:{query.PageSize}";
                cacheKey = string.Format(cacheKey, projectId);
            }
            else if (status.HasValue && requestType.HasValue && !string.IsNullOrEmpty(searchTerm))
            {
                cacheKey = $"{RedisCacheKey.MOBILIZATION_REQS_BY_STATUS_LIST_CACHE_KEY}:{status.Value}:{requestType.Value}:SEARCH:{searchTerm}:PAGE:{query.PageIndex}:SIZE:{query.PageSize}";
                cacheKey = string.Format(cacheKey, projectId);
            }
            else if (status.HasValue && requestType.HasValue)
            {
                cacheKey = $"{RedisCacheKey.MOBILIZATION_REQS_BY_STATUS_LIST_CACHE_KEY}:{status.Value}:{requestType.Value}:PAGE:{query.PageIndex}:SIZE:{query.PageSize}";
                cacheKey = string.Format(cacheKey, projectId);
            }
            else if (status.HasValue && !string.IsNullOrEmpty(searchTerm))
            {
                cacheKey = $"{RedisCacheKey.MOBILIZATION_REQS_BY_STATUS_LIST_CACHE_KEY}:{status.Value}:SEARCH:{searchTerm}:PAGE:{query.PageIndex}:SIZE:{query.PageSize}";
                cacheKey = string.Format(cacheKey, projectId);
            }
            else if (projectId > 0)
            {
                cacheKey = $"{RedisCacheKey.MOBILIZATION_REQS_BY_PROJECT_LIST_CACHE_KEY}:PAGE:{query.PageIndex}:SIZE:{query.PageSize}";
                cacheKey = string.Format(cacheKey, projectId);
            }
            else if (requestType.HasValue && !string.IsNullOrEmpty(searchTerm))
            {
                cacheKey = $"{RedisCacheKey.MOBILIZATION_REQS_BY_PROJECT_LIST_CACHE_KEY}:SEARCH:{searchTerm}:PAGE:{query.PageIndex}:SIZE:{query.PageSize}";
                cacheKey = string.Format(cacheKey, projectId);
            }
            else if (requestType.HasValue)
            {
                cacheKey = $"{RedisCacheKey.MOBILIZATION_REQS_BY_PROJECT_LIST_CACHE_KEY}:PAGE:{query.PageIndex}:SIZE:{query.PageSize}";
                cacheKey = string.Format(cacheKey, projectId);
            }
            else if (!string.IsNullOrEmpty(searchTerm))
            {
                cacheKey = $"{RedisCacheKey.MOBILIZATION_REQS_LIST_CACHE_KEY}:SEARCH:{searchTerm}:PAGE:{query.PageIndex}:SIZE:{query.PageSize}";
                cacheKey = string.Format(cacheKey, projectId);
            }
            else
            {
                cacheKey = $"{RedisCacheKey.MOBILIZATION_REQS_LIST_CACHE_KEY}:PAGE:{query.PageIndex}:SIZE:{query.PageSize}";
                cacheKey = string.Format(cacheKey, projectId);
            }

            // Try to get from cache first
            var cachedResult = await _cacheService.GetAsync<(int Total, List<ResourceMobilizationReqs> Items)>(cacheKey);
            if (cachedResult.Items != null)
            {
                query.Total = cachedResult.Total;
                return cachedResult.Items;
            }

            // Start with base query
            var dbQuery = _context.ResourceMobilizationReqs
                .Include(r => r.Project)
                .Where(r => !r.Deleted);

            // Apply filters if provided
            if (projectId > 0)
            {
                dbQuery = dbQuery.Where(r => r.ProjectId == projectId);
            }

            if (status.HasValue)
            {
                dbQuery = dbQuery.Where(r => r.Status == status.Value);
            }

            if (requestType.HasValue)
            {
                dbQuery = dbQuery.Where(r => r.RequestType == requestType.Value);
            }

            if (!string.IsNullOrEmpty(searchTerm))
            {
                var searchTermLower = searchTerm.ToLower();
                dbQuery = dbQuery.Where(r => 
                    r.RequestCode.ToLower().Contains(searchTermLower) || 
                    (r.RequestName != null && r.RequestName.ToLower().Contains(searchTermLower)));
            }

            // Get total count for pagination
            query.Total = await dbQuery.CountAsync();

            // Apply pagination
            var items = await dbQuery
                .OrderByDescending(r => r.CreatedAt)
                .Skip(query.Skip)
                .Take(query.PageSize)
                .ToListAsync();

            // Cache the result for 15 minutes
            await _cacheService.SetAsync(cacheKey, (query.Total, items), TimeSpan.FromMinutes(15));

            return items;
        }

        /// <summary>
        /// Gets a specific resource mobilization request by ID
        /// </summary>
        /// <param name="id">The ID of the request to retrieve</param>
        /// <returns>The resource mobilization request if found</returns>
        public async Task<ResourceMobilizationReqs> GetResourceMobilizationRequestById(int id)
        {
            // Check cache first
            string cacheKey = string.Format(RedisCacheKey.RESOURCE_MOBILIZATION_REQ_BY_ID_CACHE_KEY, id);
            var cachedResult = await _cacheService.GetAsync<ResourceMobilizationReqs>(cacheKey);
            
            if (cachedResult != null)
            {
                return cachedResult;
            }
            
            // Get from database if not in cache
            var request = await _context.ResourceMobilizationReqs
                .Include(r => r.Project)
                .Include(r => r.Requester)
                .Include(r => r.Approver)
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id && !r.Deleted);
            
            if (request == null)
            {
                throw new KeyNotFoundException($"Resource mobilization request with ID {id} not found");
            }
            
            // Cache the result
            await _cacheService.SetAsync(cacheKey, request, TimeSpan.FromMinutes(30));
            
            return request;
        }

        /// <summary>
        /// Sends a resource mobilization request for approval
        /// </summary>
        /// <param name="reqId">ID of the request to send</param>
        /// <param name="actionBy">ID of the user performing the action</param>
        /// <returns>Updated resource mobilization request</returns>
        public async Task<ResourceMobilizationReqs> SendResourceMobilizationRequest(int reqId, int actionBy)
        {
            // Validate user role - only Resource Manager can send resource mobilization requests
            if (!_helperService.IsInRole(actionBy, RoleConstValue.RESOURCE_MANAGER))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            // Find the resource mobilization request
            var request = await _context.ResourceMobilizationReqs
                .FirstOrDefaultAsync(r => r.Id == reqId && !r.Deleted);

            if (request == null)
            {
                throw new KeyNotFoundException(Message.ResourceRequestMessage.REQUEST_NOT_FOUND);
            }

            // Verify the user is the creator
            if (request.Creator != actionBy)
            {
                throw new UnauthorizedAccessException(Message.ResourceRequestMessage.ONLY_CREATOR_CAN_SEND);
            }

            // Check if the request is in a state that can be sent
            if (request.Status != RequestStatus.Draft && request.Status != RequestStatus.Reject)
            {
                throw new InvalidOperationException(Message.ResourceRequestMessage.ONLY_DRAFT_CAN_BE_SENT);
            }

            // Begin transaction
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Update request status
                request.Status = RequestStatus.WaitManagerApproval;
                request.UpdatedAt = DateTime.UtcNow;
                request.Updater = actionBy;

                _context.ResourceMobilizationReqs.Update(request);
                await _context.SaveChangesAsync();
                
                await transaction.CommitAsync();
                
                // Invalidate cache
                await InvalidateResourceMobilizationReqCache(request.Id, request.ProjectId);

                return request;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Approves a resource mobilization request, processing resource allocation accordingly
        /// </summary>
        /// <param name="reqId">ID of the request to approve</param>
        /// <param name="comments">Approval comments</param>
        /// <param name="actionBy">ID of the user performing the action</param>
        /// <returns>Updated resource mobilization request</returns>
        public async Task<ResourceMobilizationReqs> ApproveResourceMobilizationRequest(int reqId, string comments, int actionBy)
        {
            // Check if the user has the appropriate role to approve the request
            // First level approval is by Technical Department, second level by Executive Board
            bool isTechnicalDepartment = _helperService.IsInRole(actionBy, RoleConstValue.TECHNICAL_MANAGER);
            bool isExecutiveBoard = _helperService.IsInRole(actionBy, RoleConstValue.EXECUTIVE_BOARD);

            if (!isTechnicalDepartment && !isExecutiveBoard)
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            // Find the resource mobilization request
            var request = await _context.ResourceMobilizationReqs
                .Include(r => r.Project)
                .FirstOrDefaultAsync(r => r.Id == reqId && !r.Deleted);

            if (request == null)
            {
                throw new KeyNotFoundException(Message.ResourceRequestMessage.REQUEST_NOT_FOUND);
            }

            // Begin transaction
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                // Update request status based on approver role
                if (isTechnicalDepartment)
                {
                    // Technical Manager can only approve requests in WaitManagerApproval status
                    if (request.Status != RequestStatus.WaitManagerApproval)
                    {
                        throw new InvalidOperationException(Message.ResourceRequestMessage.NOT_WAITING_FOR_APPROVAL);
                    }

                    // Update to ManagerApproved status
                    request.Status = RequestStatus.ManagerApproved;
                    request.UpdatedAt = DateTime.UtcNow;
                    request.Updater = actionBy;
                    
                    // Store approval comments if any
                    if (!string.IsNullOrEmpty(comments))
                    {
                        request.Description = request.Description + "\n\nTechnical Manager Approval Comments: " + comments;
                    }
                    
                    _context.ResourceMobilizationReqs.Update(request);
                    await _context.SaveChangesAsync();
                }
                else if (isExecutiveBoard)
                {
                    // Executive Board can only approve requests that have been approved by Technical Manager first
                    if (request.Status != RequestStatus.ManagerApproved)
                    {
                        throw new InvalidOperationException(Message.ResourceRequestMessage.NOT_WAITING_FOR_APPROVAL);
                    }

                    // Update to BodApproved status
                    request.Status = RequestStatus.BodApproved;
                    request.UpdatedAt = DateTime.UtcNow;
                    request.Updater = actionBy;
                    
                    // Store approval comments if any
                    if (!string.IsNullOrEmpty(comments))
                    {
                        request.Description = request.Description + "\n\nExecutive Board Approval Comments: " + comments;
                    }
                    
                    // Process resources based on request type - Only after Executive Board approval
                    switch (request.RequestType)
                    {
                        case MobilizationRequestType.SupplyMore:
                            // Supply resources from company pool to project
                            await UpdateInventoryResources(request, actionBy);
                            break;
                            
                        case MobilizationRequestType.AddNew:
                            // Add new resources to company pool
                            await AddNewResources(request, actionBy);
                            break;
                            
                        default:
                            throw new InvalidOperationException(Message.ResourceRequestMessage.INVALID_REQUEST_TYPE);
                    }
                    
                    _context.ResourceMobilizationReqs.Update(request);
                    await _context.SaveChangesAsync();
                }
                
                // Commit transaction
                await transaction.CommitAsync();
                
                // Invalidate cache
                await InvalidateResourceMobilizationReqCache(request.Id, request.ProjectId);
                
                return request;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new DbUpdateException($"Failed to approve resource mobilization request: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Rejects a resource mobilization request
        /// </summary>
        /// <param name="reqId">ID of the request to reject</param>
        /// <param name="reason">Rejection reason</param>
        /// <param name="actionBy">ID of the user performing the action</param>
        /// <returns>Updated resource mobilization request</returns>
        public async Task<ResourceMobilizationReqs> RejectResourceMobilizationRequest(int reqId, string reason, int actionBy)
        {
            // Check if the user has the appropriate role to reject the request
            bool isTechnicalDepartment = _helperService.IsInRole(actionBy, RoleConstValue.TECHNICAL_MANAGER);
            bool isExecutiveBoard = _helperService.IsInRole(actionBy, RoleConstValue.EXECUTIVE_BOARD);

            if (!isTechnicalDepartment && !isExecutiveBoard)
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            // Find the resource mobilization request
            var request = await _context.ResourceMobilizationReqs
                .FirstOrDefaultAsync(r => r.Id == reqId && !r.Deleted);

            if (request == null)
            {
                throw new KeyNotFoundException(Message.ResourceRequestMessage.REQUEST_NOT_FOUND);
            }

            // Begin transaction
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Technical Manager can only reject requests waiting for their approval
                if (isTechnicalDepartment)
                {
                    if (request.Status != RequestStatus.WaitManagerApproval)
                    {
                        throw new InvalidOperationException(Message.ResourceRequestMessage.NOT_WAITING_FOR_APPROVAL);
                    }

                    // Update request status
                    request.Status = RequestStatus.Reject;
                    
                    // Store rejection reason in the description
                    if (!string.IsNullOrEmpty(reason))
                    {
                        request.Description = request.Description + "\n\nTechnical Manager Rejection Reason: " + reason;
                    }
                }
                // Executive Board can only reject requests waiting for their approval
                else if (isExecutiveBoard)
                {
                    if (request.Status != RequestStatus.ManagerApproved)
                    {
                        throw new InvalidOperationException(Message.ResourceRequestMessage.NOT_WAITING_FOR_APPROVAL);
                    }

                    // Update request status
                    request.Status = RequestStatus.Reject;
                    
                    // Store rejection reason in the description
                    if (!string.IsNullOrEmpty(reason))
                    {
                        request.Description = request.Description + "\n\nExecutive Board Rejection Reason: " + reason;
                    }
                }

                request.UpdatedAt = DateTime.UtcNow;
                request.Updater = actionBy;

                _context.ResourceMobilizationReqs.Update(request);
                await _context.SaveChangesAsync();
                
                await transaction.CommitAsync();
                
                // Invalidate cache
                await InvalidateResourceMobilizationReqCache(request.Id, request.ProjectId);

                return request;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Adds new resources based on resource mobilization request details
        /// </summary>
        /// <param name="request">The approved resource mobilization request</param>
        /// <param name="actionBy">ID of the user performing the action</param>
        private async Task AddNewResources(ResourceMobilizationReqs request, int actionBy)
        {
            // Create a dictionary to group resources by ResourceId, ProjectId, and ResourceType
            var resourceGroups = new Dictionary<(int? ResourceId, int ProjectId, ResourceType ResourceType), 
                (string Name, int Quantity, string Unit)>();
            
            foreach (var detail in request.ResourceMobilizationDetails)
            {
                // Different handling based on resource type
                string resourceName = "";
                
                switch (detail.ResourceType)
                {
                    case ResourceType.MATERIAL:
                        // For materials, add to the main company resource catalog
                        var material = await _context.Materials.FirstOrDefaultAsync(m => m.Id == detail.ResourceId && !m.Deleted);
                        if (material != null && request.RequestType == MobilizationRequestType.AddNew)
                        {
                            // Increase inventory in the Material table (company resource)
                            material.Inventory = (material.Inventory ?? 0) + detail.Quantity;
                            material.UpdatedAt = DateTime.UtcNow;
                            material.Updater = actionBy;
                            _context.Materials.Update(material);
                        }
                        resourceName = material?.MaterialName ?? $"Material {detail.ResourceId}";
                        break;
                        
                    case ResourceType.MACHINE:
                        // For vehicles, add a new vehicle to the main company resource catalog
                        var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.Id == detail.ResourceId && !v.Deleted);
                        if (vehicle != null && request.RequestType == MobilizationRequestType.SupplyMore)
                        {
                            // Only supply vehicles with Available status
                            if (vehicle.Status != VehicleStatus.Available)
                            {
                                continue; // Skip this vehicle as it's not available
                            }
                        }
                        resourceName = vehicle?.LicensePlate ?? $"Vehicle {detail.ResourceId}";
                        break;
                        
                    case ResourceType.HUMAN:
                        // For construction teams, handle appropriately
                        var team = await _context.ConstructionTeams.FirstOrDefaultAsync(t => t.Id == detail.ResourceId && !t.Deleted);
                        resourceName = team?.TeamName ?? $"Team {detail.ResourceId}";
                        break;
                        
                    default:
                        resourceName = $"Resource {detail.ResourceId}";
                        break;
                }
                
                // If it's SupplyMore request type, add resources to the project's inventory
                if (request.RequestType == MobilizationRequestType.SupplyMore)
                {
                    // Create a key for grouping
                    var key = (detail.ResourceId, request.ProjectId, detail.ResourceType);
                    
                    // If the key already exists in the dictionary, update the quantity
                    if (resourceGroups.ContainsKey(key))
                    {
                        var existing = resourceGroups[key];
                        resourceGroups[key] = (existing.Name, existing.Quantity + detail.Quantity, existing.Unit);
                    }
                    else
                    {
                        // Otherwise, add a new entry
                        resourceGroups[key] = (resourceName, detail.Quantity, detail.Unit ?? "Unit");
                    }
                }
            }
            
            // Begin transaction for atomic operations
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                // Process each resource group for SupplyMore request type
                if (request.RequestType == MobilizationRequestType.SupplyMore)
                {
                    foreach (var group in resourceGroups)
                    {
                        var key = group.Key;
                        var value = group.Value;
                        
                        // First check if it already exists in inventory
                        var existingResource = await _context.ResourceInventory
                            .FirstOrDefaultAsync(r => 
                                r.ResourceType == key.ResourceType && 
                                r.ResourceId == key.ResourceId &&
                                r.ProjectId == key.ProjectId &&
                                !r.Deleted);
                            
                        if (existingResource != null)
                        {
                            // Update existing resource
                            existingResource.Quantity += value.Quantity;
                            existingResource.UpdatedAt = DateTime.UtcNow;
                            existingResource.Updater = actionBy;
                            
                            _context.ResourceInventory.Update(existingResource);
                        }
                        else
                        {
                            // Create new inventory resource
                            var newResource = new ResourceInventory
                            {
                                Name = value.Name,
                                ResourceId = key.ResourceId,
                                ProjectId = key.ProjectId,
                                ResourceType = key.ResourceType,
                                Quantity = value.Quantity,
                                Unit = value.Unit,
                                Status = true,
                                Creator = actionBy,
                                Updater = actionBy,
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow
                            };
                            
                            await _context.ResourceInventory.AddAsync(newResource);
                        }
                    }
                }
                
                // Save all changes
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                
                // Invalidate inventory cache
                await _cacheService.DeleteByPatternAsync(RedisCacheKey.RESOURCE_INVENTORY_CACHE_KEY);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new DbUpdateException($"Failed to add new resources: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Updates inventory resources based on resource mobilization request details
        /// </summary>
        /// <param name="request">The approved resource mobilization request</param>
        /// <param name="actionBy">ID of the user performing the action</param>
        private async Task UpdateInventoryResources(ResourceMobilizationReqs request, int actionBy)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                if (request.RequestType == MobilizationRequestType.SupplyMore)
                {
                    foreach (var detail in request.ResourceMobilizationDetails)
                    {
                        // Handle resource based on type
                        switch (detail.ResourceType)
                        {
                            case ResourceType.MATERIAL:
                                // Move material from company resource to project resource
                                var material = await _context.Materials
                                    .FirstOrDefaultAsync(m => m.Id == detail.ResourceId && !m.Deleted);
                                
                                if (material != null)
                                {
                                    // Ensure there's enough inventory in the company resource
                                    if ((material.Inventory ?? 0) >= detail.Quantity)
                                    {
                                        // Reduce company inventory
                                        material.Inventory = (material.Inventory ?? 0) - detail.Quantity;
                                        material.UpdatedAt = DateTime.UtcNow;
                                        material.Updater = actionBy;
                                        _context.Materials.Update(material);
                                        
                                        // Add to project inventory
                                        await AddResourceToProjectInventory(
                                            detail.ResourceId,
                                            request.ProjectId,
                                            detail.ResourceType,
                                            detail.Quantity,
                                            material.MaterialName,
                                            material.Unit ?? "Unit",
                                            actionBy
                                        );
                                    }
                                }
                                break;
                                
                            case ResourceType.MACHINE:
                                // Move vehicle from company resource to project resource
                                var vehicle = await _context.Vehicles
                                    .FirstOrDefaultAsync(v => v.Id == detail.ResourceId && !v.Deleted);
                                
                                if (vehicle != null && vehicle.Status == VehicleStatus.Available)
                                {
                                    // Mark vehicle as unavailable since it's now assigned to a project
                                    vehicle.Status = VehicleStatus.Unavailable;
                                    vehicle.UpdatedAt = DateTime.UtcNow;
                                    vehicle.Updater = actionBy;
                                    _context.Vehicles.Update(vehicle);
                                    
                                    // Add to project inventory
                                    await AddResourceToProjectInventory(
                                        detail.ResourceId,
                                        request.ProjectId,
                                        detail.ResourceType,
                                        detail.Quantity,
                                        vehicle.VehicleName,
                                        "Unit",
                                        actionBy
                                    );
                                }
                                break;
                                
                            case ResourceType.HUMAN:
                                // Handle construction team assignment
                                var team = await _context.ConstructionTeams
                                    .FirstOrDefaultAsync(t => t.Id == detail.ResourceId && !t.Deleted);
                                
                                if (team != null)
                                {
                                    // Add to project inventory
                                    await AddResourceToProjectInventory(
                                        detail.ResourceId,
                                        request.ProjectId,
                                        detail.ResourceType,
                                        detail.Quantity,
                                        team.TeamName,
                                        "Team",
                                        actionBy
                                    );
                                }
                                break;
                        }
                    }
                }
                else if (request.RequestType == MobilizationRequestType.AddNew)
                {
                    // This is handled in AddNewResources method for materials
                    // For vehicles, we need to add them as new vehicles to the system
                    foreach (var detail in request.ResourceMobilizationDetails)
                    {
                        if (detail.ResourceType == ResourceType.MACHINE)
                        {
                            // Assuming necessary details are provided in the description or elsewhere
                            // This would typically be handled through a separate vehicle registration flow
                            // but we include a basic implementation here
                            if (!string.IsNullOrEmpty(detail.Name) && !string.IsNullOrEmpty(detail.Description))
                            {
                                var vehicleParts = detail.Description.Split("|");
                                if (vehicleParts.Length >= 3)
                                {
                                    var newVehicle = new Vehicle
                                    {
                                        VehicleName = detail.Name,
                                        LicensePlate = vehicleParts[0],
                                        Brand = vehicleParts[1],
                                        VehicleType = vehicleParts[2],
                                        YearOfManufacture = DateTime.UtcNow.Year,
                                        CountryOfManufacture = "Unknown",
                                        ChassisNumber = "N/A",
                                        EngineNumber = "N/A",
                                        Image = "default.jpg",
                                        Status = VehicleStatus.Available,
                                        Driver = 0, // No driver assigned yet
                                        Color = "Unknown",
                                        FuelType = "Unknown",
                                        Description = detail.Description,
                                        FuelTankVolume = 0,
                                        FuelUnit = "L",
                                        Attachment = "{}",
                                        Creator = actionBy,
                                        Updater = actionBy,
                                        CreatedAt = DateTime.UtcNow,
                                        UpdatedAt = DateTime.UtcNow
                                    };
                                    
                                    await _context.Vehicles.AddAsync(newVehicle);
                                }
                            }
                        }
                    }
                }
                
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                
                // Invalidate relevant caches
                await _cacheService.DeleteByPatternAsync(RedisCacheKey.RESOURCE_INVENTORY_CACHE_KEY);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new DbUpdateException($"Failed to update inventory resources: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Helper method to add a resource to the project inventory
        /// </summary>
        private async Task AddResourceToProjectInventory(
            int? resourceId, 
            int projectId, 
            ResourceType resourceType, 
            int quantity, 
            string name, 
            string unit, 
            int actionBy)
        {
            // Check if resource already exists in project inventory
            var existingResource = await _context.ResourceInventory
                .FirstOrDefaultAsync(r =>
                    r.ResourceType == resourceType &&
                    r.ResourceId == resourceId &&
                    r.ProjectId == projectId &&
                    !r.Deleted);
            
            if (existingResource != null)
            {
                // Update existing inventory
                existingResource.Quantity += quantity;
                existingResource.UpdatedAt = DateTime.UtcNow;
                existingResource.Updater = actionBy;
                
                _context.ResourceInventory.Update(existingResource);
            }
            else
            {
                // Create new inventory entry
                var newResource = new ResourceInventory
                {
                    Name = name,
                    ResourceId = resourceId,
                    ProjectId = projectId,
                    ResourceType = resourceType,
                    Quantity = quantity,
                    Unit = unit,
                    Status = true,
                    Creator = actionBy,
                    Updater = actionBy,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                
                await _context.ResourceInventory.AddAsync(newResource);
            }
        }

        #endregion

        #region Resource Inventory Methods

        /// <summary>
        /// Retrieves inventory resources with optional filtering
        /// </summary>
        /// <param name="type">Optional filter by resource type</param>
        /// <param name="projectId">Optional filter by project ID</param>
        /// <param name="query">BaseQuery object containing pagination parameters</param>
        /// <returns>List of inventory resources</returns>
        public async Task<List<ResourceInventoryDTO>> ViewInventoryResources(
            ResourceType? type, 
            int? projectId,
            BaseQuery query)
        {
            // Check authorization - allow access for:
            // 1. Resource Managers
            // 2. Executive Board members (can see all)
            // 3. Users who are part of projects for which inventory exists
            bool isExecutiveBoard = _helperService.IsInRole(query.ActionBy, RoleConstValue.EXECUTIVE_BOARD);
            
            if (!isExecutiveBoard)
            {
                // Get user's projects from ProjectUser table
                var userProjects = await _context.ProjectUsers
                    .Where(pu => pu.UserId == query.ActionBy && !pu.Deleted)
                    .Select(pu => pu.ProjectId)
                    .ToListAsync();
                
                // If user isn't part of any projects, deny access
                if (!userProjects.Any())
                {
                    throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
                }
                
                // If a specific project was requested, verify user has access to it
                if (projectId.HasValue && !userProjects.Contains(projectId.Value))
                {
                    throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED_PROJECT);
                }
            }

            // Build cache key based on parameters - include user ID and projectId to avoid cache conflicts
            string cacheKey;
            string projectPart = projectId.HasValue ? $":PROJECT:{projectId}" : "";
            
            if (type.HasValue && type.Value != ResourceType.NONE)
            {
                cacheKey = string.Format(RedisCacheKey.RESOURCE_INVENTORY_BY_TYPE_CACHE_KEY, type.Value) + 
                    $"{projectPart}:USER:{query.ActionBy}:PAGE:{query.PageIndex}:SIZE:{query.PageSize}";
            }
            else
            {
                cacheKey = $"{RedisCacheKey.RESOURCE_INVENTORY_CACHE_KEY}{projectPart}:USER:{query.ActionBy}:PAGE:{query.PageIndex}:SIZE:{query.PageSize}";
            }

            // Try to get from cache first
            var cachedResult = await _cacheService.GetAsync<(int Total, List<ResourceInventoryDTO> Items)>(cacheKey);
            if (cachedResult.Items != null)
            {
                query.Total = cachedResult.Total;
                return cachedResult.Items;
            }

            // Start with base query
            var dbQuery = _context.ResourceInventory
                .Where(r => !r.Deleted);

            // If filtering by project ID
            if (projectId.HasValue)
            {
                dbQuery = dbQuery.Where(r => r.ProjectId == projectId.Value);
            }
            // If not Executive Board, filter by user's projects
            else if (!isExecutiveBoard)
            {
                // Get user's projects from ProjectUser table
                var userProjects = await _context.ProjectUsers
                    .Where(pu => pu.UserId == query.ActionBy && !pu.Deleted)
                    .Select(pu => pu.ProjectId)
                    .ToListAsync();
                
                // Only show resources for projects the user is part of
                dbQuery = dbQuery.Where(r => r.ProjectId.HasValue && userProjects.Contains(r.ProjectId.Value));
            }

            // Apply filters if provided
            if (type.HasValue && type.Value != ResourceType.NONE)
            {
                dbQuery = dbQuery.Where(r => r.ResourceType == type.Value);
            }

            // Get total count for pagination
            query.Total = await dbQuery.CountAsync();

            // Apply pagination and projection to DTO
            var items = await dbQuery
                .OrderBy(r => r.ResourceType)
                .ThenBy(r => r.Name)
                .Skip(query.Skip)
                .Take(query.PageSize)
                .Select(r => new ResourceInventoryDTO
                {
                    Id = r.Id,
                    Name = r.Name,
                    ResourceId = r.ResourceId,
                    ProjectId = r.ProjectId,
                    ResourceType = r.ResourceType,
                    Quantity = r.Quantity,
                    Unit = r.Unit,
                    Status = r.Status,
                    CreatedAt = r.CreatedAt,
                    UpdatedAt = r.UpdatedAt
                })
                .ToListAsync();

            // Cache the result for 15 minutes
            await _cacheService.SetAsync(cacheKey, (query.Total, items), TimeSpan.FromMinutes(15));

            return items;
        }

        /// <summary>
        /// Gets a specific resource inventory item by ID
        /// </summary>
        /// <param name="id">The ID of the resource to retrieve</param>
        /// <returns>The resource inventory DTO if found</returns>
        public async Task<ResourceInventoryDTO> GetInventoryResourceById(int id)
        {
            // Check cache first
            string cacheKey = string.Format(RedisCacheKey.RESOURCE_INVENTORY_BY_ID_CACHE_KEY, id);
            var cachedResult = await _cacheService.GetAsync<ResourceInventoryDTO>(cacheKey);
            
            if (cachedResult != null)
            {
                return cachedResult;
            }
            
            // Get from database if not in cache
            var resource = await _context.ResourceInventory
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id && !r.Deleted);
            
            if (resource == null)
            {
                throw new KeyNotFoundException($"Resource with ID {id} not found");
            }
            
            // Map to DTO
            var resourceDto = new ResourceInventoryDTO
            {
                Id = resource.Id,
                Name = resource.Name,
                ResourceId = resource.ResourceId,
                ProjectId = resource.ProjectId,
                ResourceType = resource.ResourceType,
                Quantity = resource.Quantity,
                Unit = resource.Unit,
                Status = resource.Status,
                CreatedAt = resource.CreatedAt,
                UpdatedAt = resource.UpdatedAt
            };
            
            // Cache the result
            await _cacheService.SetAsync(cacheKey, resourceDto, TimeSpan.FromMinutes(30));
            
            return resourceDto;
        }

        /// <summary>
        /// Adds a new resource to inventory
        /// </summary>
        /// <param name="model">Data for the new inventory resource</param>
        /// <param name="actionBy">ID of the user performing the action</param>
        /// <returns>The created inventory resource</returns>
        public async Task<ResourceInventory> AddInventoryResource(AddResourceInventoryDTO model, int actionBy)
        {
            // Validate user role - only Resource Manager can add inventory resources
            if (!_helperService.IsInRole(actionBy, RoleConstValue.RESOURCE_MANAGER))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            var errors = new List<ResponseError>();

            // Validate resource type
            if (model.ResourceType == ResourceType.NONE)
            {
                errors.Add(new ResponseError { Field = "ResourceType", Message = Message.ResourceRequestMessage.INVALID_RESOURCE_TYPE });
            }

            // Validate name
            if (string.IsNullOrWhiteSpace(model.Name))
            {
                errors.Add(new ResponseError { Field = "Name", Message = Message.ResourceRequestMessage.NAME_REQUIRED });
            }

            // Validate quantity
            if (model.Quantity < 0)
            {
                errors.Add(new ResponseError { Field = "Quantity", Message = Message.ResourceRequestMessage.NEGATIVE_QUANTITY });
            }

            // If there are validation errors, throw exception
            if (errors.Any())
            {
                throw new ValidationException(errors);
            }

            // Begin transaction
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Create new resource inventory
                var newResource = new ResourceInventory
                {
                    Name = model.Name,
                    ResourceId = model.ResourceId,
                    ProjectId = model.ProjectId,
                    ResourceType = model.ResourceType,
                    Quantity = model.Quantity,
                    Unit = model.Unit,
                    Status = model.Status,
                    
                    // Set audit fields
                    Creator = actionBy,
                    Updater = actionBy,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _context.ResourceInventory.AddAsync(newResource);
                await _context.SaveChangesAsync();
                
                await transaction.CommitAsync();
                
                // Invalidate cache
                await InvalidateResourceInventoryCache(newResource.Id, newResource.ResourceType);

                return newResource;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Updates an existing inventory resource
        /// </summary>
        /// <param name="model">Data for updating the inventory resource</param>
        /// <param name="actionBy">ID of the user performing the action</param>
        /// <returns>The updated inventory resource</returns>
        public async Task<ResourceInventory> UpdateInventoryResource(UpdateResourceInventoryDTO model, int actionBy)
        {
            // Validate user role - only Resource Manager can update inventory resources
            if (!_helperService.IsInRole(actionBy, RoleConstValue.RESOURCE_MANAGER))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            var errors = new List<ResponseError>();

            // Validate resource type
            if (model.ResourceType == ResourceType.NONE)
            {
                errors.Add(new ResponseError { Field = "ResourceType", Message = Message.ResourceRequestMessage.INVALID_RESOURCE_TYPE });
            }

            // Validate name
            if (string.IsNullOrWhiteSpace(model.Name))
            {
                errors.Add(new ResponseError { Field = "Name", Message = Message.ResourceRequestMessage.NAME_REQUIRED });
            }

            // Validate quantity
            if (model.Quantity < 0)
            {
                errors.Add(new ResponseError { Field = "Quantity", Message = Message.ResourceRequestMessage.NEGATIVE_QUANTITY });
            }

            // If there are validation errors, throw exception
            if (errors.Any())
            {
                throw new ValidationException(errors);
            }

            // Begin transaction
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Find the resource inventory
                var resource = await _context.ResourceInventory
                    .FirstOrDefaultAsync(r => r.Id == model.Id && !r.Deleted);

                if (resource == null)
                {
                    throw new KeyNotFoundException(Message.ResourceRequestMessage.INVENTORY_NOT_FOUND);
                }

                // Store old resource type for cache invalidation
                var oldResourceType = resource.ResourceType;

                // Update properties
                resource.Name = model.Name;
                resource.ResourceId = model.ResourceId;
                resource.ProjectId = model.ProjectId;
                resource.ResourceType = model.ResourceType;
                resource.Quantity = model.Quantity;
                resource.Unit = model.Unit;
                resource.Status = model.Status;
                
                // Update audit fields
                resource.UpdatedAt = DateTime.UtcNow;
                resource.Updater = actionBy;

                _context.ResourceInventory.Update(resource);
                await _context.SaveChangesAsync();
                
                await transaction.CommitAsync();
                
                // Invalidate cache for both old and new resource type
                await InvalidateResourceInventoryCache(resource.Id, resource.ResourceType);
                if (oldResourceType != resource.ResourceType)
                {
                    await _cacheService.DeleteAsync(string.Format(RedisCacheKey.RESOURCE_INVENTORY_BY_TYPE_CACHE_KEY, oldResourceType));
                }

                return resource;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Deletes an inventory resource (soft delete)
        /// </summary>
        /// <param name="resourceId">ID of the resource to delete</param>
        /// <param name="actionBy">ID of the user performing the action</param>
        /// <returns>True if deletion was successful</returns>
        public async Task<bool> DeleteInventoryResource(int resourceId, int actionBy)
        {
            // Validate user role - only Resource Manager can delete inventory resources
            if (!_helperService.IsInRole(actionBy, RoleConstValue.RESOURCE_MANAGER))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            // Find the resource inventory
            var resource = await _context.ResourceInventory
                .FirstOrDefaultAsync(r => r.Id == resourceId && !r.Deleted);

            if (resource == null)
            {
                throw new KeyNotFoundException(Message.ResourceRequestMessage.INVENTORY_NOT_FOUND);
            }

            // Begin transaction
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Perform soft delete
                resource.Deleted = true;
                resource.UpdatedAt = DateTime.UtcNow;
                resource.Updater = actionBy;

                _context.ResourceInventory.Update(resource);
                await _context.SaveChangesAsync();
                
                await transaction.CommitAsync();
                
                // Invalidate cache
                await InvalidateResourceInventoryCache(resource.Id, resource.ResourceType);

                return true;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private async Task InvalidateResourceInventoryCache(int resourceId, ResourceType resourceType)
        {
            // Delete specific resource cache
            await _cacheService.DeleteAsync(string.Format(RedisCacheKey.RESOURCE_INVENTORY_BY_ID_CACHE_KEY, resourceId));
            
            // Delete resource type cache and related patterns
            string typeKeyBase = string.Format(RedisCacheKey.RESOURCE_INVENTORY_BY_TYPE_CACHE_KEY, resourceType);
            await _cacheService.DeleteAsync(typeKeyBase);
            await _cacheService.DeleteByPatternAsync(typeKeyBase);
            
            // Delete all resources cache and patterns
            await _cacheService.DeleteAsync(RedisCacheKey.RESOURCE_INVENTORY_CACHE_KEY);
            await _cacheService.DeleteByPatternAsync(RedisCacheKey.RESOURCE_INVENTORY_CACHE_KEY);
        }

        #endregion

        #region Resource Allocation Methods

        /// <summary>
        /// Retrieves all resource allocation requests with optional filtering
        /// </summary>
        /// <param name="fromProjectId">Optional filter by source project ID</param>
        /// <param name="toProjectId">Optional filter by destination project ID</param>
        /// <param name="status">Optional filter by request status</param>
        /// <param name="requestType">Optional filter by request type (1: Project to Project, 2: Project to Task, 3: Task to Task)</param>
        /// <param name="searchTerm">Optional search by request code or request name</param>
        /// <param name="query">BaseQuery object containing pagination parameters</param>
        /// <returns>List of resource allocation requests</returns>
        public async Task<List<ResourceAllocationReqs>> ViewResourceAllocationRequests(
            int? fromProjectId, int? toProjectId, RequestStatus? status, int? requestType, string? searchTerm, BaseQuery query)
        {
            // Check authorization - this method is accessible by both Resource Manager, Technical Department, and Executive Board
            if (!_helperService.IsInRole(query.ActionBy, RoleConstValue.RESOURCE_MANAGER) && 
                !_helperService.IsInRole(query.ActionBy, RoleConstValue.TECHNICAL_MANAGER) &&
                !_helperService.IsInRole(query.ActionBy, RoleConstValue.EXECUTIVE_BOARD))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            // Build cache key based on parameters
            string cacheKey = $"{RedisCacheKey.ALLOCATION_REQS_LIST_CACHE_KEY}";
            
            if (fromProjectId.HasValue)
            {
                cacheKey = string.Format(RedisCacheKey.ALLOCATION_REQS_BY_FROM_PROJECT_LIST_CACHE_KEY, fromProjectId.Value);
            }
            
            if (toProjectId.HasValue)
            {
                cacheKey = cacheKey == $"{RedisCacheKey.ALLOCATION_REQS_LIST_CACHE_KEY}" ? 
                    string.Format(RedisCacheKey.ALLOCATION_REQS_BY_TO_PROJECT_LIST_CACHE_KEY, toProjectId.Value) :
                    $"{cacheKey}:TO:{toProjectId.Value}";
            }
            
            if (status.HasValue)
            {
                cacheKey = $"{cacheKey}:STATUS:{status.Value}";
            }

            if (requestType.HasValue)
            {
                cacheKey = $"{cacheKey}:TYPE:{requestType.Value}";
            }

            if (!string.IsNullOrEmpty(searchTerm))
            {
                cacheKey = $"{cacheKey}:SEARCH:{searchTerm}";
            }
            
            cacheKey = $"{cacheKey}:PAGE:{query.PageIndex}:SIZE:{query.PageSize}";

            // Try to get from cache first
            var cachedResult = await _cacheService.GetAsync<(int Total, List<ResourceAllocationReqs> Items)>(cacheKey);
            if (cachedResult.Items != null)
            {
                query.Total = cachedResult.Total;
                return cachedResult.Items;
            }

            // Start with base query
            var dbQuery = _context.ResourceAllocationReqs
                .Include(r => r.FromProject)
                .Include(r => r.ToProject)
                .Where(r => !r.Deleted);

            // Apply filters if provided
            if (fromProjectId.HasValue)
            {
                dbQuery = dbQuery.Where(r => r.FromProjectId == fromProjectId.Value);
            }

            if (toProjectId.HasValue)
            {
                dbQuery = dbQuery.Where(r => r.ToProjectId == toProjectId.Value);
            }

            if (status.HasValue)
            {
                dbQuery = dbQuery.Where(r => r.Status == status.Value);
            }

            if (requestType.HasValue)
            {
                dbQuery = dbQuery.Where(r => r.RequestType == requestType.Value);
            }

            if (!string.IsNullOrEmpty(searchTerm))
            {
                var searchTermLower = searchTerm.ToLower();
                dbQuery = dbQuery.Where(r => 
                    r.RequestCode.ToLower().Contains(searchTermLower) || 
                    (r.RequestName != null && r.RequestName.ToLower().Contains(searchTermLower)));
            }

            // Get total count for pagination
            query.Total = await dbQuery.CountAsync();

            // Apply pagination
            var items = await dbQuery
                .OrderByDescending(r => r.CreatedAt)
                .Skip(query.Skip)
                .Take(query.PageSize)
                .ToListAsync();

            // Cache the result for 15 minutes
            await _cacheService.SetAsync(cacheKey, (query.Total, items), TimeSpan.FromMinutes(15));

            return items;
        }

        /// <summary>
        /// Gets a specific resource allocation request by ID
        /// </summary>
        /// <param name="id">The ID of the request to retrieve</param>
        /// <returns>The resource allocation request if found</returns>
        public async Task<ResourceAllocationReqs> GetResourceAllocationRequestById(int id)
        {
            // Check cache first
            string cacheKey = string.Format(RedisCacheKey.RESOURCE_ALLOCATION_REQ_BY_ID_CACHE_KEY, id);
            var cachedResult = await _cacheService.GetAsync<ResourceAllocationReqs>(cacheKey);
            
            if (cachedResult != null)
            {
                return cachedResult;
            }
            
            // Get from database if not in cache
            var request = await _context.ResourceAllocationReqs
                .Include(r => r.FromProject)
                .Include(r => r.ToProject)
                .Include(r => r.Requester)
                .Include(r => r.Approver)
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id && !r.Deleted);
            
            if (request == null)
            {
                throw new KeyNotFoundException($"Resource allocation request with ID {id} not found");
            }
            
            // Cache the result
            await _cacheService.SetAsync(cacheKey, request, TimeSpan.FromMinutes(30));
            
            return request;
        }

        /// <summary>
        /// Sends a resource allocation request for approval
        /// </summary>
        /// <param name="reqId">ID of the request to send</param>
        /// <param name="actionBy">ID of the user performing the action</param>
        /// <returns>Updated resource allocation request</returns>
        public async Task<ResourceAllocationReqs> SendResourceAllocationRequest(int reqId, int actionBy)
        {
            // Validate user role - only Resource Manager can send resource allocation requests
            if (!_helperService.IsInRole(actionBy, RoleConstValue.RESOURCE_MANAGER))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            // Find the resource allocation request
            var request = await _context.ResourceAllocationReqs
                .FirstOrDefaultAsync(r => r.Id == reqId && !r.Deleted);

            if (request == null)
            {
                throw new KeyNotFoundException(Message.ResourceRequestMessage.REQUEST_NOT_FOUND);
            }

            // Verify the user is the creator
            if (request.Creator != actionBy)
            {
                throw new UnauthorizedAccessException(Message.ResourceRequestMessage.ONLY_CREATOR_CAN_SEND);
            }

            // Check if the request is in a state that can be sent
            if (request.Status != RequestStatus.Draft && request.Status != RequestStatus.Reject)
            {
                throw new InvalidOperationException(Message.ResourceRequestMessage.ONLY_DRAFT_CAN_BE_SENT);
            }

            // Begin transaction
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Update request status
                request.Status = RequestStatus.WaitManagerApproval;
                request.UpdatedAt = DateTime.UtcNow;
                request.Updater = actionBy;

                _context.ResourceAllocationReqs.Update(request);
                await _context.SaveChangesAsync();
                
                await transaction.CommitAsync();
                
                // Invalidate cache
                await InvalidateResourceAllocationReqCache(request.Id, request.FromProjectId, request.ToProjectId);

                return request;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Approves a resource allocation request
        /// </summary>
        /// <param name="reqId">ID of the request to approve</param>
        /// <param name="comments">Approval comments</param>
        /// <param name="actionBy">ID of the user performing the action</param>
        /// <returns>Updated resource allocation request</returns>
        public async Task<ResourceAllocationReqs> ApproveResourceAllocationRequest(int reqId, string comments, int actionBy)
        {
            // Check if the user has the appropriate role to approve the request
            // First level approval is by Technical Department, second level by Executive Board
            bool isTechnicalDepartment = _helperService.IsInRole(actionBy, RoleConstValue.TECHNICAL_MANAGER);
            bool isExecutiveBoard = _helperService.IsInRole(actionBy, RoleConstValue.EXECUTIVE_BOARD);

            if (!isTechnicalDepartment && !isExecutiveBoard)
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            // Find the resource allocation request
            var request = await _context.ResourceAllocationReqs
                .FirstOrDefaultAsync(r => r.Id == reqId && !r.Deleted);

            if (request == null)
            {
                throw new KeyNotFoundException(Message.ResourceRequestMessage.REQUEST_NOT_FOUND);
            }

            // Check if the user has the correct role for the current state of the request
            if (isTechnicalDepartment && request.Status != RequestStatus.WaitManagerApproval)
            {
                throw new InvalidOperationException(Message.ResourceRequestMessage.NOT_WAITING_FOR_APPROVAL);
            }

            // Fix: Only check this condition when the user is Executive Board but not also Technical Department
            // This handles cases where a user might have multiple roles
            if (isExecutiveBoard && !isTechnicalDepartment && request.Status != RequestStatus.ManagerApproved)
            {
                throw new InvalidOperationException(Message.ResourceRequestMessage.NOT_WAITING_FOR_APPROVAL);
            }

            // Begin transaction
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Update request status based on who is approving
                if (isTechnicalDepartment && request.Status == RequestStatus.WaitManagerApproval)
                {
                    request.Status = RequestStatus.ManagerApproved;
                }
                else if (isExecutiveBoard && request.Status == RequestStatus.ManagerApproved)
                {
                    request.Status = RequestStatus.BodApproved;
                }

                // Store approval comments in the description or as an additional field
                if (!string.IsNullOrEmpty(comments))
                {
                    request.Description = request.Description + "\n\nApproval Comments: " + comments;
                }

                request.UpdatedAt = DateTime.UtcNow;
                request.Updater = actionBy;

                _context.ResourceAllocationReqs.Update(request);
                await _context.SaveChangesAsync();
                
                await transaction.CommitAsync();
                
                // Invalidate cache
                await InvalidateResourceAllocationReqCache(request.Id, request.FromProjectId, request.ToProjectId);

                return request;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Rejects a resource allocation request
        /// </summary>
        /// <param name="reqId">ID of the request to reject</param>
        /// <param name="reason">Rejection reason</param>
        /// <param name="actionBy">ID of the user performing the action</param>
        /// <returns>Updated resource allocation request</returns>
        public async Task<ResourceAllocationReqs> RejectResourceAllocationRequest(int reqId, string reason, int actionBy)
        {
            // Check if the user has the appropriate role to reject the request
            bool isTechnicalDepartment = _helperService.IsInRole(actionBy, RoleConstValue.TECHNICAL_MANAGER);
            bool isExecutiveBoard = _helperService.IsInRole(actionBy, RoleConstValue.EXECUTIVE_BOARD);

            if (!isTechnicalDepartment && !isExecutiveBoard)
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            // Find the resource allocation request
            var request = await _context.ResourceAllocationReqs
                .FirstOrDefaultAsync(r => r.Id == reqId && !r.Deleted);

            if (request == null)
            {
                throw new KeyNotFoundException(Message.ResourceRequestMessage.REQUEST_NOT_FOUND);
            }

            // Check if the request is in a state that can be rejected
            if (isTechnicalDepartment && request.Status != RequestStatus.WaitManagerApproval)
            {
                throw new InvalidOperationException(Message.ResourceRequestMessage.NOT_WAITING_FOR_APPROVAL);
            }

            // Fix: Only check this condition when the user is Executive Board but not also Technical Department
            if (isExecutiveBoard && !isTechnicalDepartment && request.Status != RequestStatus.ManagerApproved)
            {
                throw new InvalidOperationException(Message.ResourceRequestMessage.NOT_WAITING_FOR_APPROVAL);
            }

            // Begin transaction
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Update request status
                request.Status = RequestStatus.Reject;
                
                // Store rejection reason in the description or as an additional field
                if (!string.IsNullOrEmpty(reason))
                {
                    request.Description = request.Description + "\n\nRejection Reason: " + reason;
                }

                request.UpdatedAt = DateTime.UtcNow;
                request.Updater = actionBy;

                _context.ResourceAllocationReqs.Update(request);
                await _context.SaveChangesAsync();
                
                await transaction.CommitAsync();
                
                // Invalidate cache
                await InvalidateResourceAllocationReqCache(request.Id, request.FromProjectId, request.ToProjectId);

                return request;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        #endregion

        /// <summary>
        /// Invalidates the mobilization cache for a specific project
        /// </summary>
        /// <param name="projectId">The project ID</param>
        /// <param name="actionBy">ID of the user performing the action</param>
        public async Task InvalidateMobilizationCacheForProject(int projectId)
        {
            int actionBy = 0;
            // Use HTTP context accessor to get current user ID if available
            var httpContext = new HttpContextAccessor().HttpContext;
            if (httpContext != null && httpContext.User.Identity.IsAuthenticated)
            {
                var userIdClaim = httpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                {
                    actionBy = userId;
                }
            }

            // Validate user role - only Resource Manager or Technical Manager can invalidate caches
            if (!_helperService.IsInRole(actionBy, RoleConstValue.RESOURCE_MANAGER) && 
                !_helperService.IsInRole(actionBy, RoleConstValue.TECHNICAL_MANAGER))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            // Delete project-related cache
            await _cacheService.DeleteAsync(string.Format(RedisCacheKey.MOBILIZATION_REQ_BY_PROJECT_CACHE_KEY, projectId));
            
            // Use pattern-based deletion for project-specific and paginated caches
            string projectPattern = string.Format(RedisCacheKey.MOBILIZATION_REQS_BY_PROJECT_LIST_CACHE_KEY, projectId);
            await _cacheService.DeleteByPatternAsync(projectPattern);
        }

        /// <summary>
        /// Invalidates all mobilization caches
        /// </summary>
        /// <param name="actionBy">ID of the user performing the action</param>
        public async Task InvalidateAllMobilizationCaches()
        {
            int actionBy = 0;
            // Use HTTP context accessor to get current user ID if available
            var httpContext = new HttpContextAccessor().HttpContext;
            if (httpContext != null && httpContext.User.Identity.IsAuthenticated)
            {
                var userIdClaim = httpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                {
                    actionBy = userId;
                }
            }

            // Validate user role - only Resource Manager or Technical Manager can invalidate caches
            if (!_helperService.IsInRole(actionBy, RoleConstValue.RESOURCE_MANAGER) && 
                !_helperService.IsInRole(actionBy, RoleConstValue.TECHNICAL_MANAGER))
            {
                throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
            }

            // Delete list caches
            await _cacheService.DeleteAsync(RedisCacheKey.MOBILIZATION_REQS_LIST_CACHE_KEY);
            
            // Delete all status-based caches using pattern matching
            await _cacheService.DeleteByPatternAsync(RedisCacheKey.MOBILIZATION_REQS_BY_STATUS_LIST_CACHE_KEY);
            
            // Clear all paginated caches for mobilization requests using pattern matching
            await _cacheService.DeleteByPatternAsync(RedisCacheKey.MOBILIZATION_REQS_LIST_CACHE_KEY);
            
            // Clear all resource mobilization request IDs
            await _cacheService.DeleteByPatternAsync(RedisCacheKey.MOBILIZATION_REQ_CACHE_KEY);
            await _cacheService.DeleteByPatternAsync(RedisCacheKey.RESOURCE_MOBILIZATION_REQ_BY_ID_CACHE_KEY);
            await _cacheService.DeleteByPatternAsync(RedisCacheKey.MOBILIZATION_REQ_BY_PROJECT_CACHE_KEY);
        }
    }
}
