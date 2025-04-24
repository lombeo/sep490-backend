# Contract and Project Relationship Changes

## One-to-One Relationship Between Contract and Project

The relationship between Contract and Project has been changed from one-to-many (1:n) to one-to-one (1:1). This means:

- Each Project can have at most one Contract
- Contracts are now accessed primarily by ProjectId rather than ContractId

## API Changes

The following API endpoints have been updated:

### Get Contract Detail

**Previous Endpoint:**
```
GET /api/contract/detail/{id}
```
where `id` was the ContractId

**Updated Endpoint:**
```
GET /api/contract/detail/{projectId}
```
where `projectId` is the ID of the project

### Delete Contract

**Previous Endpoint:**
```
DELETE /api/contract/delete-contract/{id}
```
where `id` was the ContractId

**Updated Endpoint:**
```
DELETE /api/contract/delete-contract/{projectId}
```
where `projectId` is the ID of the project

## Database Changes

- Added a unique index on the Contracts table for the ProjectId column (with filter for non-deleted records)
- Enforced the one-to-one relationship at both the application level and database level

## Implementation Details

1. Entity Relationship:
   - Modified Project entity to have a single Contract navigation property instead of a collection
   - Updated Contract entity relationship configuration
   - Added a unique index in Entity Framework configuration to enforce the one-to-one relationship

2. Service Changes:
   - Updated ContractService to identify contracts by ProjectId
   - Modified the Detail method to find contracts by ProjectId
   - Updated the Delete method to find and delete contracts by ProjectId
   - Added validation in Save method to prevent creating multiple contracts for the same project

3. Data Access:
   - Updated DataService.ListContract method to handle the one-to-one relationship
   - Modified caching strategies to reflect the relationship change

## Multi-Level Validation

The one-to-one relationship is now enforced at multiple levels:

1. **Database Level**: Through a unique index with a filter for non-deleted records
2. **Entity Framework Level**: Through the relationship configuration
3. **Service Level**: Through validation checks when creating or updating contracts
4. **Cache Management**: Through proper invalidation of cache when contracts change

This multi-level approach ensures data integrity while avoiding the need for database triggers.

## To Apply Changes 

To apply these changes to your database, you can do one of the following:

1. Create a new migration:
```
dotnet ef migrations add ContractOneToOneRelationship
dotnet ef database update
```

2. Or apply the index directly via SQL:
```sql
CREATE UNIQUE INDEX "IX_Contracts_ProjectId_Unique" 
ON "Contracts" ("ProjectId") 
WHERE "Deleted" = false;
```

# Circular Reference Handling

## Issue
The application had a circular reference issue when serializing entities with bi-directional navigation properties (e.g., ResourceMobilizationReqs -> Project -> ResourceMobilizationReqs). 

This caused the following error when calling endpoints that return these entities:
```
Newtonsoft.Json.JsonSerializationException: Self referencing loop detected with type 'Sep490_Backend.Infra.Entities.ResourceMobilizationReqs'. Path 'Item2[0].Project.ResourceMobilizationReqs'.
```

## Solution
The issue was fixed by configuring Newtonsoft.Json to ignore circular references during serialization:

1. Added the `Microsoft.AspNetCore.Mvc.NewtonsoftJson` package (version 8.0.3)
2. Configured JSON serialization options in `HostingExtensions.cs` to ignore circular references:
   ```csharp
   .AddNewtonsoftJson(options =>
   {
       options.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
   });
   ```
3. Updated `CacheService.SetAsync()` method to use the same settings when serializing objects for caching

## Entity Structure
The circular reference occurred because:
- `ResourceMobilizationReqs` entity has a navigation property to `Project`
- `Project` entity has a collection navigation property back to `ResourceMobilizationReqs`

This is a common pattern in EF Core relationships but requires special handling during serialization.

## Affected Endpoints
The fix resolves issues with the following endpoints:
- `GET /sep490/resourcereq/mobilization/list` - List resource mobilization requests
- Any other endpoint returning entities with circular navigation properties

# Soft Delete Implementation

## Overview

The application now features an enhanced soft delete mechanism that automatically handles cascade soft deletion for related entities and provides a consistent approach to soft delete operations.

Key improvements:

1. **Global Query Filters**: Automatically filters out soft-deleted entities from all queries
2. **Cascade Soft Delete**: When an entity is soft-deleted, all related entities are automatically soft-deleted too
3. **Extension Methods**: Simplified API for soft delete operations

## Features

- **Automatic Filtering**: No need to manually add `.Where(e => !e.Deleted)` to every query
- **Data Integrity**: All related entities are soft-deleted together, maintaining referential integrity
- **Simplified API**: Extension methods like `SoftDeleteAsync`, `SoftDeleteByIdAsync`, and `IncludeRelatedEntities`
- **Maintenance Friendly**: Adding new related entities doesn't require changes to delete logic

## Implementation

The soft delete functionality is implemented through:

1. Global query filters applied in the `BackendContext`
2. Cascade soft delete logic in the `OnBeforeSaving` method
3. Extension methods in the `EntityExtensions` class

For detailed implementation and usage examples, see the [Soft Delete Documentation](Docs/SoftDeleteDocumentation.md).

## Migration

Existing code that manually handles soft delete (by setting `Deleted = true` and updating the entity) should be updated to use the new extension methods:

```csharp
// Before
entity.Deleted = true;
entity.UpdatedAt = DateTime.UtcNow;
entity.Updater = actionBy;
_context.Update(entity);
await _context.SaveChangesAsync();

// After
await _context.SoftDeleteAsync(entity, actionBy);
```

## Note

Cache invalidation is still required after soft delete operations, as the global query filters only affect database queries, not in-memory cached data.

# Resource Allocation Enhancements

## Overview

The Resource Allocation system has been enhanced to support different types of resource allocation scenarios for construction site leveling projects:

1. **Project-to-Project Allocation**: Transfer resources between construction projects
2. **Project-to-Task Allocation**: Allocate resources from a project to a specific task
3. **Task-to-Task Allocation**: Transfer resources between specific tasks

## Entity Changes

The `ResourceAllocationReqs` entity has been extended with:

- `RequestType` (int): Indicates the type of allocation:
  - 1: Project-to-Project
  - 2: Project-to-Task
  - 3: Task-to-Task
- `FromTaskId` (int?): Optional source task ID for task-based allocations
- `ToTaskId` (int?): Optional destination task ID for task-based allocations

## API Changes

The resource allocation API endpoints have been updated to support the new allocation types:

### List Resource Allocation Requests

**Updated Endpoint:**
```
GET /sep490/resourcereq/allocation/list
```

**New Parameters:**
- `requestType` (int?): Filter by allocation type (1, 2, or 3)

### Create/Update Resource Allocation Request

**Endpoint:**
```
POST /sep490/resourcereq/allocation/save
```

**New Request Body Fields:**
- `requestType` (int): Type of allocation (Default: 1)
- `fromTaskId` (int?): Source task ID (Required for Task-to-Task)
- `toTaskId` (int?): Destination task ID (Required for Project-to-Task and Task-to-Task)

## Validation Logic

The system performs validation based on the request type:

1. **Project-to-Project**:
   - Cannot allocate from a project to itself
   - Task IDs are ignored

2. **Project-to-Task**:
   - Requires a valid destination task ID (`toTaskId`)
   - Source task ID is ignored

3. **Task-to-Task**:
   - Requires valid source and destination task IDs
   - Projects can be the same or different

## Database Migration

A migration (`AddResourceAllocationReqsFields`) has been added to implement these changes:

- Adds the `RequestType`, `FromTaskId`, and `ToTaskId` columns
- Creates indexes for the new columns for better performance
- Sets default value of `RequestType` to 1 (Project-to-Project) for backward compatibility

## Frontend Integration

The frontend already contains constants for the different allocation types:
- `PROJECT_TO_PROJECT = 1`
- `PROJECT_TO_TASK = 2`
- `TASK_TO_TASK = 3`

These are used in the UI to show the appropriate allocation type options and form fields.

## Implementation Notes

1. **Backward Compatibility**: Existing allocations are treated as Project-to-Project
2. **Performance**: Indexes have been added for efficient filtering by the new fields
3. **Caching**: Cache keys now include the request type parameter

To apply these changes, run:
```
dotnet ef database update
```

or manually apply the migration SQL.