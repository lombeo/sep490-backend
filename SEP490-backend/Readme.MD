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