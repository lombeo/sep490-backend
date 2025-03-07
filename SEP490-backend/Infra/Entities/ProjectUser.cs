using Microsoft.EntityFrameworkCore;

namespace Sep490_Backend.Infra.Entities
{
    public class ProjectUser : CommonEntity
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public int UserId { get; set; }
        public bool IsCreator { get; set; } // True: Người tạo, False: Người chỉ được quyền xem

        // Navigation properties
        public virtual User User { get; set; }
        public virtual Project Project { get; set; }
    }

    public static class ProjectUserConfiguration
    {
        public static void Config(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ProjectUser>(entity =>
            {
                entity.ToTable("ProjectUsers");

                entity.HasKey(e => e.Id);

                entity.Property(e => e.ProjectId)
                      .IsRequired();

                entity.Property(e => e.UserId)
                      .IsRequired();

                entity.Property(e => e.IsCreator)
                      .IsRequired()
                      .HasDefaultValue(false);

                entity.Property(e => e.CreatedAt)
                      .HasColumnType("timestamp without time zone");

                entity.Property(e => e.UpdatedAt)
                      .HasColumnType("timestamp without time zone");

                entity.Property(e => e.Deleted)
                      .HasDefaultValue(false);

                // Tạo index để tối ưu hóa tìm kiếm
                entity.HasIndex(e => new { e.ProjectId, e.UserId });

                // Relationships
                entity.HasOne(e => e.User)
                      .WithMany(u => u.ProjectUsers)
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Project)
                      .WithMany(p => p.ProjectUsers)
                      .HasForeignKey(e => e.ProjectId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}