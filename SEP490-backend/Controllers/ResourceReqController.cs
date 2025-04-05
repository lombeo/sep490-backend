using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sep490_Backend.DTO;
using Sep490_Backend.DTO.Common;
using Sep490_Backend.DTO.ResourceReqs;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Infra.Enums;
using Sep490_Backend.Services.ResourceReqService;

namespace Sep490_Backend.Controllers
{
    [ApiController]
    [Route(RouteApiConstant.BASE_PATH + "/resourcereq")]
    [Authorize]
    public class ResourceReqController : BaseAPIController
    {
        private readonly IResourceReqService _resourceReqService;

        public ResourceReqController(IResourceReqService resourceReqService)
        {
            _resourceReqService = resourceReqService;
        }

        #region Resource Mobilization APIs

        /// <summary>
        /// Creates or updates a resource mobilization request
        /// </summary>
        /// <param name="model">Data for the resource mobilization request</param>
        /// <returns>The saved resource mobilization request</returns>
        [HttpPost("mobilization/save")]
        public async Task<ResponseDTO<ResourceMobilizationReqs>> SaveResourceMobilizationReq([FromBody] SaveResourceMobilizationReqDTO model)
        {
            return await HandleException(
                _resourceReqService.SaveResourceMobilizationReq(model, UserId),
                Message.CommonMessage.ACTION_SUCCESS
            );
        }

        /// <summary>
        /// Deletes a resource mobilization request
        /// </summary>
        /// <param name="id">ID of the resource mobilization request to delete</param>
        /// <returns>True if deletion was successful</returns>
        [HttpDelete("mobilization/delete/{id}")]
        public async Task<ResponseDTO<bool>> DeleteResourceMobilizationReq(int id)
        {
            return await HandleException(
                _resourceReqService.DeleteResourceMobilizationReq(id, UserId),
                Message.CommonMessage.ACTION_SUCCESS
            );
        }

        /// <summary>
        /// Gets all resource mobilization requests with optional filtering
        /// </summary>
        /// <param name="projectId">Optional filter by project ID</param>
        /// <param name="status">Optional filter by request status</param>
        /// <param name="searchTerm">Optional search by request code or request name</param>
        /// <param name="pageIndex">Page number for pagination (default: 1)</param>
        /// <param name="pageSize">Page size for pagination (default: 10)</param>
        /// <returns>List of resource mobilization requests</returns>
        [HttpGet("mobilization/list")]
        public async Task<ResponseDTO<List<ResourceMobilizationReqs>>> GetResourceMobilizationRequests(
            [FromQuery] int projectId = 0,
            [FromQuery] RequestStatus? status = null,
            [FromQuery] string? searchTerm = null,
            [FromQuery] int pageIndex = 1,
            [FromQuery] int pageSize = 10)
        {
            var query = new BaseQuery
            {
                ActionBy = UserId,
                PageIndex = pageIndex,
                PageSize = pageSize
            };
            
            var result = await HandleException(
                _resourceReqService.ViewResourceMobilizationRequests(projectId, status, searchTerm, query),
                Message.CommonMessage.ACTION_SUCCESS
            );
            
            result.Meta = new ResponseMeta 
            { 
                Total = query.Total, 
                Index = pageIndex, 
                PageSize = pageSize 
            };
            
            return result;
        }

        /// <summary>
        /// Sends a resource mobilization request for approval
        /// </summary>
        /// <param name="id">ID of the request to send</param>
        /// <returns>Updated resource mobilization request</returns>
        [HttpPut("mobilization/send/{id}")]
        public async Task<ResponseDTO<ResourceMobilizationReqs>> SendResourceMobilizationRequest(int id)
        {
            return await HandleException(
                _resourceReqService.SendResourceMobilizationRequest(id, UserId),
                Message.CommonMessage.ACTION_SUCCESS
            );
        }

        /// <summary>
        /// Approves a resource mobilization request
        /// </summary>
        /// <param name="id">ID of the request to approve</param>
        /// <param name="model">Approval data</param>
        /// <returns>Updated resource mobilization request</returns>
        [HttpPut("mobilization/approve/{id}")]
        public async Task<ResponseDTO<ResourceMobilizationReqs>> ApproveResourceMobilizationRequest(
            int id, [FromBody] ApproveRequestDTO model)
        {
            return await HandleException(
                _resourceReqService.ApproveResourceMobilizationRequest(id, model.Comments, UserId),
                Message.CommonMessage.ACTION_SUCCESS
            );
        }

        /// <summary>
        /// Rejects a resource mobilization request
        /// </summary>
        /// <param name="id">ID of the request to reject</param>
        /// <param name="model">Rejection data</param>
        /// <returns>Updated resource mobilization request</returns>
        [HttpPut("mobilization/reject/{id}")]
        public async Task<ResponseDTO<ResourceMobilizationReqs>> RejectResourceMobilizationRequest(
            int id, [FromBody] RejectRequestDTO model)
        {
            return await HandleException(
                _resourceReqService.RejectResourceMobilizationRequest(id, model.Reason, UserId),
                Message.CommonMessage.ACTION_SUCCESS
            );
        }

        /// <summary>
        /// Gets a specific resource mobilization request by ID
        /// </summary>
        /// <param name="id">ID of the request to retrieve</param>
        /// <returns>The resource mobilization request details</returns>
        [HttpGet("mobilization/{id}")]
        public async Task<ResponseDTO<ResourceMobilizationReqs>> GetResourceMobilizationRequestById(int id)
        {
            return await HandleException(
                _resourceReqService.GetResourceMobilizationRequestById(id),
                Message.CommonMessage.ACTION_SUCCESS
            );
        }

        #endregion

        #region Resource Inventory APIs

        /// <summary>
        /// Gets all inventory resources with optional filtering
        /// </summary>
        /// <param name="type">Optional filter by resource type</param>
        /// <param name="pageIndex">Page number for pagination (default: 1)</param>
        /// <param name="pageSize">Page size for pagination (default: 10)</param>
        /// <returns>List of inventory resources</returns>
        [HttpGet("inventory/list")]
        public async Task<ResponseDTO<List<ResourceInventoryDTO>>> GetInventoryResources(
            [FromQuery] ResourceType? type = null,
            [FromQuery] int pageIndex = 1,
            [FromQuery] int pageSize = 10)
        {
            var query = new BaseQuery
            {
                ActionBy = UserId,
                PageIndex = pageIndex,
                PageSize = pageSize
            };
            
            var result = await HandleException(
                _resourceReqService.ViewInventoryResources(type, query),
                Message.CommonMessage.ACTION_SUCCESS
            );
            
            result.Meta = new ResponseMeta 
            { 
                Total = query.Total, 
                Index = pageIndex, 
                PageSize = pageSize 
            };
            
            return result;
        }

        /// <summary>
        /// Adds a new resource to inventory
        /// </summary>
        /// <param name="model">Data for the new inventory resource</param>
        /// <returns>The created inventory resource</returns>
        [HttpPost("inventory/save")]
        public async Task<ResponseDTO<ResourceInventory>> AddInventoryResource([FromBody] AddResourceInventoryDTO model)
        {
            return await HandleException(
                _resourceReqService.AddInventoryResource(model, UserId),
                Message.CommonMessage.ACTION_SUCCESS
            );
        }

        /// <summary>
        /// Updates an existing inventory resource
        /// </summary>
        /// <param name="model">Data for updating the inventory resource</param>
        /// <returns>The updated inventory resource</returns>
        [HttpPut("inventory/update")]
        public async Task<ResponseDTO<ResourceInventory>> UpdateInventoryResource([FromBody] UpdateResourceInventoryDTO model)
        {
            return await HandleException(
                _resourceReqService.UpdateInventoryResource(model, UserId),
                Message.CommonMessage.ACTION_SUCCESS
            );
        }

        /// <summary>
        /// Deletes an inventory resource
        /// </summary>
        /// <param name="id">ID of the resource to delete</param>
        /// <returns>True if deletion was successful</returns>
        [HttpDelete("inventory/delete/{id}")]
        public async Task<ResponseDTO<bool>> DeleteInventoryResource(int id)
        {
            return await HandleException(
                _resourceReqService.DeleteInventoryResource(id, UserId),
                Message.CommonMessage.ACTION_SUCCESS
            );
        }

        /// <summary>
        /// Gets a specific inventory resource by ID
        /// </summary>
        /// <param name="id">ID of the resource to retrieve</param>
        /// <returns>The resource inventory details</returns>
        [HttpGet("inventory/{id}")]
        public async Task<ResponseDTO<ResourceInventoryDTO>> GetInventoryResourceById(int id)
        {
            return await HandleException(
                _resourceReqService.GetInventoryResourceById(id),
                Message.CommonMessage.ACTION_SUCCESS
            );
        }

        #endregion

        #region Resource Allocation APIs

        /// <summary>
        /// Creates or updates a resource allocation request
        /// </summary>
        /// <param name="model">Data for the resource allocation request</param>
        /// <returns>The saved resource allocation request</returns>
        [HttpPost("allocation/save")]
        public async Task<ResponseDTO<ResourceAllocationReqs>> SaveResourceAllocationReq([FromBody] SaveResourceAllocationReqDTO model)
        {
            return await HandleException(
                _resourceReqService.SaveResourceAllocationReq(model, UserId),
                Message.CommonMessage.ACTION_SUCCESS
            );
        }

        /// <summary>
        /// Deletes a resource allocation request
        /// </summary>
        /// <param name="id">ID of the resource allocation request to delete</param>
        /// <returns>True if deletion was successful</returns>
        [HttpDelete("allocation/delete/{id}")]
        public async Task<ResponseDTO<bool>> DeleteResourceAllocationReq(int id)
        {
            return await HandleException(
                _resourceReqService.DeleteResourceAllocationReq(id, UserId),
                Message.CommonMessage.ACTION_SUCCESS
            );
        }

        /// <summary>
        /// Gets all resource allocation requests with optional filtering
        /// </summary>
        /// <param name="fromProjectId">Optional filter by source project ID</param>
        /// <param name="toProjectId">Optional filter by destination project ID</param>
        /// <param name="status">Optional filter by request status</param>
        /// <param name="searchTerm">Optional search by request code or request name</param>
        /// <param name="pageIndex">Page number for pagination (default: 1)</param>
        /// <param name="pageSize">Page size for pagination (default: 10)</param>
        /// <returns>List of resource allocation requests</returns>
        [HttpGet("allocation/list")]
        public async Task<ResponseDTO<List<ResourceAllocationReqs>>> GetResourceAllocationRequests(
            [FromQuery] int? fromProjectId = null,
            [FromQuery] int? toProjectId = null,
            [FromQuery] RequestStatus? status = null,
            [FromQuery] string? searchTerm = null,
            [FromQuery] int pageIndex = 1,
            [FromQuery] int pageSize = 10)
        {
            var query = new BaseQuery
            {
                ActionBy = UserId,
                PageIndex = pageIndex,
                PageSize = pageSize
            };
            
            var result = await HandleException(
                _resourceReqService.ViewResourceAllocationRequests(fromProjectId, toProjectId, status, searchTerm, query),
                Message.CommonMessage.ACTION_SUCCESS
            );
            
            result.Meta = new ResponseMeta 
            { 
                Total = query.Total, 
                Index = pageIndex, 
                PageSize = pageSize 
            };
            
            return result;
        }

        /// <summary>
        /// Sends a resource allocation request for approval
        /// </summary>
        /// <param name="id">ID of the request to send</param>
        /// <returns>Updated resource allocation request</returns>
        [HttpPut("allocation/send/{id}")]
        public async Task<ResponseDTO<ResourceAllocationReqs>> SendResourceAllocationRequest(int id)
        {
            return await HandleException(
                _resourceReqService.SendResourceAllocationRequest(id, UserId),
                Message.CommonMessage.ACTION_SUCCESS
            );
        }

        /// <summary>
        /// Approves a resource allocation request
        /// </summary>
        /// <param name="id">ID of the request to approve</param>
        /// <param name="model">Approval data</param>
        /// <returns>Updated resource allocation request</returns>
        [HttpPut("allocation/approve/{id}")]
        public async Task<ResponseDTO<ResourceAllocationReqs>> ApproveResourceAllocationRequest(
            int id, [FromBody] ApproveRequestDTO model)
        {
            return await HandleException(
                _resourceReqService.ApproveResourceAllocationRequest(id, model.Comments, UserId),
                Message.CommonMessage.ACTION_SUCCESS
            );
        }

        /// <summary>
        /// Rejects a resource allocation request
        /// </summary>
        /// <param name="id">ID of the request to reject</param>
        /// <param name="model">Rejection data</param>
        /// <returns>Updated resource allocation request</returns>
        [HttpPut("allocation/reject/{id}")]
        public async Task<ResponseDTO<ResourceAllocationReqs>> RejectResourceAllocationRequest(
            int id, [FromBody] RejectRequestDTO model)
        {
            return await HandleException(
                _resourceReqService.RejectResourceAllocationRequest(id, model.Reason, UserId),
                Message.CommonMessage.ACTION_SUCCESS
            );
        }

        /// <summary>
        /// Gets a specific resource allocation request by ID
        /// </summary>
        /// <param name="id">ID of the request to retrieve</param>
        /// <returns>The resource allocation request details</returns>
        [HttpGet("allocation/{id}")]
        public async Task<ResponseDTO<ResourceAllocationReqs>> GetResourceAllocationRequestById(int id)
        {
            return await HandleException(
                _resourceReqService.GetResourceAllocationRequestById(id),
                Message.CommonMessage.ACTION_SUCCESS
            );
        }

        #endregion
    }
} 