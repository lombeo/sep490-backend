using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using Sep490_Backend.Infra.Entities;

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
            //OnModelCreatingPartial(modelBuilder);
        }

        //partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
                CancellationToken cancellationToken = new CancellationToken())
        {
            OnBeforeSaving();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        protected virtual void OnBeforeSaving()
        {
            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry != null && entry.Entity is CommonEntity commonEntity)
                {
                    if (entry.State == EntityState.Added)
                    {
                        ((CommonEntity)entry.Entity).CreatedAt = DateTime.UtcNow;
                    }

                    ((CommonEntity)entry.Entity).UpdatedAt = DateTime.UtcNow;
                }

            }
        }
    }
}
