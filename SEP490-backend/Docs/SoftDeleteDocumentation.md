# Soft Delete Implementation

This document explains the soft delete implementation in the SEP490 Backend application.

## Overview

Soft delete allows entities to be marked as "deleted" without physically removing them from the database. This implementation includes:

1. Global query filters to automatically filter out soft-deleted entities
2. Cascade soft delete for related entities
3. Extension methods to simplify soft delete operations

## How It Works

### Base Implementation

All entities that need soft delete functionality inherit from `CommonEntity`, which includes the `Deleted` flag:

```csharp
public class CommonEntity
{
    public DateTime? UpdatedAt { get; set; }
    public DateTime? CreatedAt { get; set; }
    public bool Deleted { get; set; } = false;
    public int Creator { get; set; }
    public int Updater { get; set; }
}
```

### Global Query Filters

The `BackendContext` applies global query filters to all entities inheriting from `CommonEntity`, automatically excluding soft-deleted entities from query results:

```csharp
private void ApplyGlobalFilters(ModelBuilder modelBuilder)
{
    var entityTypes = modelBuilder.Model.GetEntityTypes()
        .Where(t => typeof(CommonEntity).IsAssignableFrom(t.ClrType));

    foreach (var entityType in entityTypes)
    {
        // Skip types that don't inherit from CommonEntity
        if (!typeof(CommonEntity).IsAssignableFrom(entityType.ClrType))
            continue;

        // Apply filter: x => !x.Deleted
        var parameter = Expression.Parameter(entityType.ClrType, "x");
        var deletedProperty = Expression.Property(parameter, nameof(CommonEntity.Deleted));
        var notDeletedExpression = Expression.Not(deletedProperty);
        var lambda = Expression.Lambda(notDeletedExpression, parameter);

        var method = typeof(EntityTypeBuilderExtensions)
            .GetMethods()
            .First(m => m.Name == nameof(EntityTypeBuilderExtensions.HasQueryFilter) && m.IsGenericMethod);
            
        var genericMethod = method.MakeGenericMethod(entityType.ClrType);
        var entityTypeBuilder = modelBuilder.Entity(entityType.ClrType);
        genericMethod.Invoke(null, new object[] { entityTypeBuilder, lambda });
    }
}
```

### Cascade Soft Delete

When an entity is soft-deleted, all related entities are automatically soft-deleted too:

```csharp
protected virtual void OnBeforeSaving()
{
    var entries = ChangeTracker.Entries<CommonEntity>().ToList();
    
    foreach (var entry in entries)
    {
        if (entry.State == EntityState.Added)
        {
            entry.Entity.CreatedAt = DateTime.UtcNow;
        }

        entry.Entity.UpdatedAt = DateTime.UtcNow;
        
        // Handle cascade soft delete for related entities
        if (entry.State == EntityState.Modified && 
            entry.Entity.Deleted && 
            entry.OriginalValues[nameof(CommonEntity.Deleted)] is bool originalDeleted && 
            !originalDeleted)
        {
            CascadeSoftDelete(entry.Entity);
        }
    }
}

private void CascadeSoftDelete(CommonEntity entity)
{
    // Find all navigation properties that are collections of entities inheriting from CommonEntity
    var entityType = entity.GetType();
    var navigationProperties = entityType.GetProperties()
        .Where(p => 
            p.PropertyType.IsGenericType && 
            (p.PropertyType.GetGenericTypeDefinition() == typeof(ICollection<>) || 
             p.PropertyType.GetGenericTypeDefinition() == typeof(List<>)) &&
            typeof(CommonEntity).IsAssignableFrom(p.PropertyType.GetGenericArguments()[0])
        );
        
    foreach (var property in navigationProperties)
    {
        // Get the navigation collection
        var value = property.GetValue(entity);
        if (value == null) continue;
        
        var enumerableValue = value as System.Collections.IEnumerable;
        if (enumerableValue == null) continue;
        
        foreach (var item in enumerableValue)
        {
            if (item is CommonEntity relatedEntity && !relatedEntity.Deleted)
            {
                relatedEntity.Deleted = true;
                relatedEntity.UpdatedAt = DateTime.UtcNow;
                
                // Recursively apply cascade soft delete
                CascadeSoftDelete(relatedEntity);
            }
        }
    }
}
```

## Extension Methods

To simplify soft delete operations, we've implemented extension methods in `EntityExtensions.cs`:

```csharp
// Marks an entity as soft deleted
public static T SoftDelete<T>(this T entity, int userId) where T : CommonEntity
{
    entity.Deleted = true;
    entity.UpdatedAt = DateTime.UtcNow;
    entity.Updater = userId;
    return entity;
}

// Soft delete an entity and save changes
public static async Task<bool> SoftDeleteAsync<T>(this DbContext context, T entity, int userId) where T : CommonEntity
{
    entity.SoftDelete(userId);
    context.Update(entity);
    await context.SaveChangesAsync();
    return true;
}

// Soft delete an entity by ID
public static async Task<bool> SoftDeleteByIdAsync<T>(this DbContext context, int id, int userId) where T : CommonEntity
{
    var entity = await context.Set<T>().FindAsync(id);
    if (entity == null)
        return false;
        
    return await context.SoftDeleteAsync(entity, userId);
}

// Include all related entities for cascade delete
public static IQueryable<T> IncludeRelatedEntities<T>(this IQueryable<T> query) where T : CommonEntity
{
    var entityType = typeof(T);
    var properties = entityType.GetProperties()
        .Where(p => 
            p.PropertyType.IsGenericType && 
            (p.PropertyType.GetGenericTypeDefinition() == typeof(ICollection<>) || 
             p.PropertyType.GetGenericTypeDefinition() == typeof(List<>)) &&
            typeof(CommonEntity).IsAssignableFrom(p.PropertyType.GetGenericArguments()[0])
        );
        
    foreach (var property in properties)
    {
        query = query.Include(property.Name);
    }
    
    return query;
}
```

## Usage Examples

### Simple Soft Delete

```csharp
// Find the entity
var entity = await _context.Entities.FindAsync(id);

// Use extension method to soft delete
await _context.SoftDeleteAsync(entity, userId);
```

### Soft Delete with Cascade

```csharp
// Get entity with all related entities loaded
var entity = await _context.Entities
    .IncludeRelatedEntities()
    .FirstOrDefaultAsync(e => e.Id == id);
    
// Soft delete - related entities will be soft deleted automatically
await _context.SoftDeleteAsync(entity, userId);
```

### Soft Delete by ID

```csharp
// Directly soft delete by ID
await _context.SoftDeleteByIdAsync<Entity>(id, userId);
```

## Benefits

1. **Data Integrity**: Maintains data for historical purposes and potential recovery
2. **Automatic Filtering**: No need to manually filter deleted entities in queries
3. **Cascade Behavior**: Related entities are automatically soft-deleted
4. **Consistency**: Standardized approach across the application
5. **Maintainability**: Adding new related entities doesn't require changes to delete logic

## Important Notes

1. Always use the provided extension methods for soft delete operations
2. Remember to invalidate relevant caches after soft delete operations
3. If you need to include soft-deleted entities in a query, use `.IgnoreQueryFilters()` 

## Cache Invalidation with Soft Delete

When performing soft delete operations, it's important to invalidate any caches that might contain the deleted entity data. The following extension methods are provided for soft delete with integrated cache invalidation:

```csharp
// Soft delete an entity and invalidate specific cache keys
public static async Task<bool> SoftDeleteWithCacheInvalidationAsync<T>(
    this DbContext context, 
    T entity, 
    int userId,
    ICacheService cacheService,
    params string[] cacheKeys) where T : CommonEntity;

// Soft delete an entity and invalidate caches by pattern
public static async Task<bool> SoftDeleteWithCachePatternInvalidationAsync<T>(
    this DbContext context, 
    T entity, 
    int userId,
    ICacheService cacheService,
    params string[] cachePatterns) where T : CommonEntity;
```

### Example Usage

```csharp
// Soft delete with specific cache keys
await _context.SoftDeleteWithCacheInvalidationAsync(
    entity, 
    actionBy, 
    _cacheService,
    RedisCacheKey.CUSTOMER_CACHE_KEY,
    RedisCacheKey.PROJECT_CACHE_KEY,
    $"{RedisCacheKey.CUSTOMER_CACHE_KEY}:{entity.Id}"
);

// Soft delete with pattern-based cache invalidation
await _context.SoftDeleteWithCachePatternInvalidationAsync(
    entity,
    actionBy,
    _cacheService,
    "CUSTOMER:*",
    "PROJECT:*"
);
```

### Best Practices for Cache Invalidation

1. **Invalidate Specific Keys**: Always invalidate the specific cache keys related to the entity.
2. **Invalidate Parent Entities**: If the entity is a child, invalidate parent entity caches too.
3. **Invalidate Pattern-Based Keys**: For related dynamic keys, use pattern-based invalidation.
4. **Check Downstream Impact**: Consider what other entities might be affected by this deletion.
5. **Document Cache Dependencies**: Keep cache dependencies documented for each entity type.

### Cache Keys Reference

For a complete list of cache keys used in the system, refer to the `RedisCacheKey.cs` file. Here are some common patterns:

- Main entity collections: `ENTITY_TYPE_CACHE_KEY`
- Entity by ID: `ENTITY_TYPE:ID:{id}`
- Entity by relationship: `ENTITY_TYPE:RELATIONSHIP:{relationId}` 