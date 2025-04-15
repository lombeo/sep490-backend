using Microsoft.EntityFrameworkCore;
using Sep490_Backend.Infra.Entities;
using Sep490_Backend.Services.CacheService;

namespace Sep490_Backend.Infra.Helps
{
    /// <summary>
    /// Extension methods for entity operations, particularly focused on soft delete functionality
    /// </summary>
    public static class EntityExtensions
    {
        /// <summary>
        /// Marks an entity as soft deleted and sets the updater information
        /// </summary>
        /// <typeparam name="T">Entity type that inherits from CommonEntity</typeparam>
        /// <param name="entity">The entity to mark as deleted</param>
        /// <param name="userId">The ID of the user performing the deletion</param>
        /// <returns>The entity with updated deletion state</returns>
        public static T SoftDelete<T>(this T entity, int userId) where T : CommonEntity
        {
            entity.Deleted = true;
            entity.UpdatedAt = DateTime.UtcNow;
            entity.Updater = userId;
            return entity;
        }

        /// <summary>
        /// Extension method for the DbContext to soft delete an entity and save changes
        /// </summary>
        /// <typeparam name="T">Entity type that inherits from CommonEntity</typeparam>
        /// <param name="context">The database context</param>
        /// <param name="entity">The entity to soft delete</param>
        /// <param name="userId">The ID of the user performing the deletion</param>
        /// <returns>Task representing the async operation</returns>
        public static async Task<bool> SoftDeleteAsync<T>(this DbContext context, T entity, int userId) where T : CommonEntity
        {
            entity.SoftDelete(userId);
            context.Update(entity);
            await context.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// Extension method for the DbContext to soft delete an entity by its ID and save changes
        /// </summary>
        /// <typeparam name="T">Entity type that inherits from CommonEntity</typeparam>
        /// <param name="context">The database context</param>
        /// <param name="id">The ID of the entity to soft delete</param>
        /// <param name="userId">The ID of the user performing the deletion</param>
        /// <returns>Task representing the async operation, returning true if successful</returns>
        public static async Task<bool> SoftDeleteByIdAsync<T>(this DbContext context, int id, int userId) where T : CommonEntity
        {
            var entity = await context.Set<T>().FindAsync(id);
            if (entity == null)
                return false;
                
            return await context.SoftDeleteAsync(entity, userId);
        }
        
        /// <summary>
        /// Extension method to include all common related entities for cascade soft delete
        /// </summary>
        /// <typeparam name="T">Entity type that inherits from CommonEntity</typeparam>
        /// <param name="query">The initial queryable</param>
        /// <returns>Queryable with includes for all navigation properties that are collections of CommonEntity types</returns>
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

        /// <summary>
        /// Extension method for the DbContext to soft delete an entity, save changes, and invalidate related caches
        /// </summary>
        /// <typeparam name="T">Entity type that inherits from CommonEntity</typeparam>
        /// <param name="context">The database context</param>
        /// <param name="entity">The entity to soft delete</param>
        /// <param name="userId">The ID of the user performing the deletion</param>
        /// <param name="cacheService">The cache service to invalidate caches</param>
        /// <param name="cacheKeys">Array of cache keys to invalidate</param>
        /// <returns>Task representing the async operation, returning true if successful</returns>
        public static async Task<bool> SoftDeleteWithCacheInvalidationAsync<T>(
            this DbContext context, 
            T entity, 
            int userId,
            ICacheService cacheService,
            params string[] cacheKeys) where T : CommonEntity
        {
            // Soft delete the entity
            entity.SoftDelete(userId);
            context.Update(entity);
            await context.SaveChangesAsync();
            
            // Invalidate specified caches
            if (cacheService != null && cacheKeys != null && cacheKeys.Length > 0)
            {
                foreach (var cacheKey in cacheKeys)
                {
                    if (!string.IsNullOrEmpty(cacheKey))
                    {
                        await cacheService.DeleteAsync(cacheKey);
                    }
                }
            }
            
            return true;
        }

        /// <summary>
        /// Extension method for the DbContext to soft delete an entity, save changes, and invalidate caches by pattern
        /// </summary>
        /// <typeparam name="T">Entity type that inherits from CommonEntity</typeparam>
        /// <param name="context">The database context</param>
        /// <param name="entity">The entity to soft delete</param>
        /// <param name="userId">The ID of the user performing the deletion</param>
        /// <param name="cacheService">The cache service to invalidate caches</param>
        /// <param name="cachePatterns">Array of cache key patterns to invalidate using pattern matching</param>
        /// <returns>Task representing the async operation, returning true if successful</returns>
        public static async Task<bool> SoftDeleteWithCachePatternInvalidationAsync<T>(
            this DbContext context, 
            T entity, 
            int userId,
            ICacheService cacheService,
            params string[] cachePatterns) where T : CommonEntity
        {
            // Soft delete the entity
            entity.SoftDelete(userId);
            context.Update(entity);
            await context.SaveChangesAsync();
            
            // Invalidate specified cache patterns
            if (cacheService != null && cachePatterns != null && cachePatterns.Length > 0)
            {
                foreach (var pattern in cachePatterns)
                {
                    if (!string.IsNullOrEmpty(pattern))
                    {
                        await cacheService.DeleteByPatternAsync(pattern);
                    }
                }
            }
            
            return true;
        }
    }
} 