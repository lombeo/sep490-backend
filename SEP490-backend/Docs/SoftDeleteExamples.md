# Soft Delete Examples for Service Classes

This document provides examples of how to implement soft delete in service classes using the new soft delete functionality.

## Basic Soft Delete Implementation

For a simple entity with no related entities:

```csharp
public async Task<bool> DeleteEntity(int id, int actionBy)
{
    // Authorization check
    if (!_helperService.IsInRole(actionBy, RoleConstValue.REQUIRED_ROLE))
    {
        throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
    }

    // Find the entity
    var entity = await _context.Entities
        .FirstOrDefaultAsync(e => e.Id == id && !e.Deleted);

    if (entity == null)
    {
        throw new KeyNotFoundException(Message.CommonMessage.NOT_FOUND);
    }

    // Use the extension method for soft delete
    await _context.SoftDeleteAsync(entity, actionBy);

    // Invalidate cache if needed
    await _cacheService.DeleteAsync(RedisCacheKey.ENTITY_CACHE_KEY);

    return true;
}
```

## Cascade Soft Delete with Related Entities

For an entity with related entities that should be deleted together:

```csharp
public async Task<bool> DeleteEntityWithRelations(int id, int actionBy)
{
    // Authorization check
    if (!_helperService.IsInRole(actionBy, RoleConstValue.REQUIRED_ROLE))
    {
        throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
    }

    // Find the entity with its related entities
    var entity = await _context.Entities
        .Include(e => e.RelatedEntities) // Include all related entities you want to cascade delete
        .FirstOrDefaultAsync(e => e.Id == id && !e.Deleted);

    if (entity == null)
    {
        throw new KeyNotFoundException(Message.CommonMessage.NOT_FOUND);
    }

    // Use the extension method for cascade soft delete
    await _context.SoftDeleteAsync(entity, actionBy);

    // Invalidate cache if needed
    await _cacheService.DeleteAsync(RedisCacheKey.ENTITY_CACHE_KEY);

    return true;
}
```

## Using IncludeRelatedEntities Extension Method

For an entity where you want to automatically include all related collections:

```csharp
public async Task<bool> DeleteEntityWithAllRelations(int id, int actionBy)
{
    // Authorization check
    if (!_helperService.IsInRole(actionBy, RoleConstValue.REQUIRED_ROLE))
    {
        throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
    }

    // Find the entity with all related entities
    var entity = await _context.Entities
        .IncludeRelatedEntities() // Automatically includes all collection navigation properties
        .FirstOrDefaultAsync(e => e.Id == id && !e.Deleted);

    if (entity == null)
    {
        throw new KeyNotFoundException(Message.CommonMessage.NOT_FOUND);
    }

    // Use the extension method for cascade soft delete
    await _context.SoftDeleteAsync(entity, actionBy);

    // Invalidate cache if needed
    await _cacheService.DeleteAsync(RedisCacheKey.ENTITY_CACHE_KEY);

    return true;
}
```

## Direct Delete by ID

For simple cases where you don't need to load the entity first:

```csharp
public async Task<bool> DeleteEntityById(int id, int actionBy)
{
    // Authorization check
    if (!_helperService.IsInRole(actionBy, RoleConstValue.REQUIRED_ROLE))
    {
        throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
    }

    // Use the extension method to directly delete by ID
    var result = await _context.SoftDeleteByIdAsync<Entity>(id, actionBy);
    
    if (!result)
    {
        throw new KeyNotFoundException(Message.CommonMessage.NOT_FOUND);
    }

    // Invalidate cache if needed
    await _cacheService.DeleteAsync(RedisCacheKey.ENTITY_CACHE_KEY);

    return true;
}
```

## Pre-delete Validation

When you need to validate before deleting:

```csharp
public async Task<bool> DeleteEntityWithValidation(int id, int actionBy)
{
    // Authorization check
    if (!_helperService.IsInRole(actionBy, RoleConstValue.REQUIRED_ROLE))
    {
        throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
    }

    // Find the entity with its relationships
    var entity = await _context.Entities
        .Include(e => e.RelatedItems)
        .FirstOrDefaultAsync(e => e.Id == id && !e.Deleted);

    if (entity == null)
    {
        throw new KeyNotFoundException(Message.CommonMessage.NOT_FOUND);
    }

    // Validate that entity can be deleted
    if (entity.RelatedItems != null && entity.RelatedItems.Any(i => i.IsInUse))
    {
        throw new InvalidOperationException("Cannot delete entity because it has items in use");
    }

    // Use the extension method for soft delete
    await _context.SoftDeleteAsync(entity, actionBy);

    // Invalidate cache if needed
    await _cacheService.DeleteAsync(RedisCacheKey.ENTITY_CACHE_KEY);

    return true;
}
```

## Handling External Resources

When you need to delete external resources (like files):

```csharp
public async Task<bool> DeleteEntityWithExternalResources(int id, int actionBy)
{
    // Authorization check
    if (!_helperService.IsInRole(actionBy, RoleConstValue.REQUIRED_ROLE))
    {
        throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
    }

    // Find the entity
    var entity = await _context.Entities
        .FirstOrDefaultAsync(e => e.Id == id && !e.Deleted);

    if (entity == null)
    {
        throw new KeyNotFoundException(Message.CommonMessage.NOT_FOUND);
    }

    // Delete associated external resources
    if (entity.Attachments != null)
    {
        try
        {
            var attachments = JsonSerializer.Deserialize<List<AttachmentInfo>>(entity.Attachments.RootElement.ToString());
            if (attachments != null && attachments.Any())
            {
                var linksToDelete = attachments.Select(a => a.WebContentLink).ToList();
                await _externalStorageService.DeleteFilesByLinks(linksToDelete);
            }
        }
        catch (Exception ex)
        {
            // Log error but continue with the delete operation
            Console.WriteLine($"Failed to delete external resources: {ex.Message}");
        }
    }

    // Use the extension method for soft delete
    await _context.SoftDeleteAsync(entity, actionBy);

    // Invalidate cache if needed
    await _cacheService.DeleteAsync(RedisCacheKey.ENTITY_CACHE_KEY);

    return true;
}
```

## Soft Delete with Cache Invalidation

For more efficient cache invalidation with soft delete:

```csharp
public async Task<bool> DeleteEntityWithCacheInvalidation(int id, int actionBy)
{
    // Authorization check
    if (!_helperService.IsInRole(actionBy, RoleConstValue.REQUIRED_ROLE))
    {
        throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
    }

    // Find the entity
    var entity = await _context.Entities
        .FirstOrDefaultAsync(e => e.Id == id && !e.Deleted);

    if (entity == null)
    {
        throw new KeyNotFoundException(Message.CommonMessage.NOT_FOUND);
    }

    // Use the extension method that combines soft delete and cache invalidation
    await _context.SoftDeleteWithCacheInvalidationAsync(
        entity, 
        actionBy, 
        _cacheService,
        RedisCacheKey.ENTITY_CACHE_KEY,
        $"ENTITY:{id}",
        RedisCacheKey.RELATED_ENTITY_CACHE_KEY
    );

    return true;
}
```

## Soft Delete with Pattern-Based Cache Invalidation

For invalidating multiple related caches with patterns:

```csharp
public async Task<bool> DeleteEntityWithPatternCacheInvalidation(int id, int actionBy)
{
    // Authorization check
    if (!_helperService.IsInRole(actionBy, RoleConstValue.REQUIRED_ROLE))
    {
        throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
    }

    // Find the entity
    var entity = await _context.Entities
        .FirstOrDefaultAsync(e => e.Id == id && !e.Deleted);

    if (entity == null)
    {
        throw new KeyNotFoundException(Message.CommonMessage.NOT_FOUND);
    }

    // Use the extension method that combines soft delete and pattern-based cache invalidation
    await _context.SoftDeleteWithCachePatternInvalidationAsync(
        entity,
        actionBy,
        _cacheService,
        "ENTITY:*",                  // All entity-related caches
        $"RELATED_ENTITY:{id}:*"     // All related entity caches for this entity
    );

    return true;
}
```

## Comprehensive Cache Invalidation Strategy

For a comprehensive approach that combines specific keys and patterns:

```csharp
public async Task<bool> DeleteEntityWithComprehensiveCacheInvalidation(int id, int actionBy)
{
    // Authorization check
    if (!_helperService.IsInRole(actionBy, RoleConstValue.REQUIRED_ROLE))
    {
        throw new UnauthorizedAccessException(Message.CommonMessage.NOT_ALLOWED);
    }

    // Find the entity with related entities
    var entity = await _context.Entities
        .Include(e => e.RelatedEntities)
        .FirstOrDefaultAsync(e => e.Id == id && !e.Deleted);

    if (entity == null)
    {
        throw new KeyNotFoundException(Message.CommonMessage.NOT_FOUND);
    }

    // Check if the entity can be deleted
    if (entity.RelatedEntities?.Any(r => r.IsInUse) == true)
    {
        throw new InvalidOperationException("Cannot delete entity with in-use related entities");
    }

    // Perform external resource cleanup if needed
    if (entity.Attachments != null)
    {
        try
        {
            // Clean up code here...
        }
        catch (Exception ex)
        {
            // Log but continue
            Console.WriteLine($"Error cleaning up: {ex.Message}");
        }
    }

    // 1. Soft delete with specific key invalidation
    await _context.SoftDeleteWithCacheInvalidationAsync(
        entity, 
        actionBy, 
        _cacheService,
        RedisCacheKey.ENTITY_CACHE_KEY,
        RedisCacheKey.SUMMARY_CACHE_KEY
    );

    // 2. Follow up with pattern-based cache invalidation
    await _cacheService.DeleteByPatternAsync($"ENTITY:{id}:*");
    await _cacheService.DeleteByPatternAsync("DASHBOARD:*");
    
    // 3. Handle special cache cases
    var relatedIds = entity.RelatedEntities?.Select(r => r.Id).ToList() ?? new List<int>();
    foreach (var relatedId in relatedIds)
    {
        await _cacheService.DeleteAsync($"RELATED:{relatedId}");
    }

    return true;
}
``` 