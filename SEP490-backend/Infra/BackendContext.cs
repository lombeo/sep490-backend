using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Reflection;
using Sep490_Backend.Infra.Entities;
using System.Linq.Expressions;
using System;

namespace Sep490_Backend.Infra
{
    public partial class BackendContext : DbContext
    {
        public BackendContext()
        {
        }

        public BackendContext(DbContextOptions<BackendContext> options)
             : base(options)
        {
        }

        public virtual DbSet<User> Users { get; set; }
        public virtual DbSet<RefreshToken> RefreshTokens { get; set; }
        public virtual DbSet<EmailTemplate> EmailTemplates { get; set; }
        public virtual DbSet<Project> Projects { get; set; }
        public virtual DbSet<ProjectUser> ProjectUsers { get; set; }
        public virtual DbSet<SiteSurvey> SiteSurveys { get; set; }
        public virtual DbSet<Contract> Contracts { get; set; }
        public virtual DbSet<ContractDetail> ContractDetails { get; set; }
        public virtual DbSet<Vehicle> Vehicles { get; set; }
        public virtual DbSet<Customer> Customers { get; set; }
        public virtual DbSet<ActionLog> ActionLogs { get; set; }
        public virtual DbSet<ConstructionPlan> ConstructionPlans { get; set; }
        public virtual DbSet<ConstructionTeam> ConstructionTeams { get; set; }
        public virtual DbSet<ConstructPlanItem> ConstructPlanItems { get; set; }
        public virtual DbSet<ConstructPlanItemDetail> ConstructPlanItemDetails { get; set; }
        public virtual DbSet<ResourceAllocationReqs> ResourceAllocationReqs { get; set; }
        public virtual DbSet<ResourceMobilizationReqs> ResourceMobilizationReqs { get; set; }
        public virtual DbSet<ResourceInventory> ResourceInventory { get; set; }
        public virtual DbSet<Material> Materials { get; set; }
        public virtual DbSet<ConstructionLog> ConstructionLogs { get; set; }
        public virtual DbSet<ConstructionProgress> ConstructionProgresses { get; set; }
        public virtual DbSet<ConstructionProgressItem> ConstructionProgressItems { get; set; }
        public virtual DbSet<ConstructionProgressItemDetail> ConstructionProgressItemDetails { get; set; }
        public virtual DbSet<InspectionReport> InspectionReports { get; set; }
        public virtual DbSet<PlanEditLock> PlanEditLocks { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
            UserAuthenConfiguration.Config(modelBuilder);
            RefreshTokenConfiguration.Config(modelBuilder);
            EmailTemplateConfiguration.Config(modelBuilder);
            ProjectConfiguration.Config(modelBuilder);
            ProjectUserConfiguration.Config(modelBuilder);
            SiteSurveyConfiguration.Config(modelBuilder);
            ContractConfiguration.Config(modelBuilder);
            ContractDetailConfiguration.Config(modelBuilder);
            VehicleConfiguration.Config(modelBuilder);
            CustomerConfiguration.Config(modelBuilder);
            ActionLogConfiguration.Config(modelBuilder);
            ConstructionPlanConfiguration.Config(modelBuilder);
            ConstructionTeamConfiguration.Config(modelBuilder);
            ConstructPlanItemConfiguration.Config(modelBuilder);
            ConstructPlanItemDetailConfiguration.Config(modelBuilder);
            ResourceAllocationReqsConfiguration.Config(modelBuilder);
            ResourceMobilizationReqsConfiguration.Config(modelBuilder);
            ResourceInventoryConfiguration.Config(modelBuilder);
            MaterialConfiguration.Config(modelBuilder);
            ConstructionLogConfiguration.Config(modelBuilder);
            ConstructionProgressConfiguration.Config(modelBuilder);
            InspectionReportConfiguration.Config(modelBuilder);
            PlanEditLockConfiguration.Config(modelBuilder);
            
            // Apply global query filters for soft delete to all entities that inherit from CommonEntity
            ApplyGlobalFilters(modelBuilder);
        }

        private void ApplyGlobalFilters(ModelBuilder modelBuilder)
        {
            // Get all entity types that inherit from CommonEntity
            var entityTypes = modelBuilder.Model.GetEntityTypes()
                .Where(t => typeof(CommonEntity).IsAssignableFrom(t.ClrType));

            foreach (var entityType in entityTypes)
            {
                // Skip types that don't inherit from CommonEntity
                if (!typeof(CommonEntity).IsAssignableFrom(entityType.ClrType))
                    continue;

                // Create the filter expression: x => !x.Deleted
                var parameter = Expression.Parameter(entityType.ClrType, "x");
                var deletedProperty = Expression.Property(parameter, nameof(CommonEntity.Deleted));
                var notDeletedExpression = Expression.Not(deletedProperty);
                var lambda = Expression.Lambda(notDeletedExpression, parameter);

                // Apply filter via model builder's HasQueryFilter directly
                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
            }
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
                CancellationToken cancellationToken = new CancellationToken())
        {
            OnBeforeSaving();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

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
            // Find all navigation properties that are collections of entities that inherit from CommonEntity
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
                
                // For each related entity in the collection, mark it as deleted
                var collectionType = property.PropertyType;
                var elementType = collectionType.GetGenericArguments()[0];
                
                var enumerableValue = value as System.Collections.IEnumerable;
                if (enumerableValue == null) continue;
                
                foreach (var item in enumerableValue)
                {
                    if (item is CommonEntity relatedEntity && !relatedEntity.Deleted)
                    {
                        relatedEntity.Deleted = true;
                        relatedEntity.UpdatedAt = DateTime.UtcNow;
                        
                        // Also apply cascade soft delete to this related entity
                        CascadeSoftDelete(relatedEntity);
                    }
                }
            }
        }
    }
}
