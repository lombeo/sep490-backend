using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using Api_Project_Prn.Infra.Entities;

namespace Api_Project_Prn.Infra
{
    public partial class PrnProjectApiContext : DbContext
    {
        public PrnProjectApiContext()
        {
        }

        public PrnProjectApiContext(DbContextOptions<PrnProjectApiContext> options)
             : base(options)
        {
        }

        public virtual DbSet<User> Users { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
            UserAuthenConfiguration.Config(modelBuilder);
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
            //foreach (var entry in ChangeTracker.Entries())
            //{
            //    if (entry.State == EntityState.Added)
            //    {
            //        ((CommonEntity)entry.Entity).CreatedAt = DateTime.UtcNow;
            //    }

            //    ((CommonEntity)entry.Entity).UpdatedAt = DateTime.UtcNow;
            //}
        }
    }
}
