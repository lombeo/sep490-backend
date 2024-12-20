using Microsoft.EntityFrameworkCore;

namespace Sep490_Backend.Infra.Entities
{
    public class UserProfile : CommonEntity
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string FullName { get; set; }
        public string Phone { get; set; }
        public bool Gender { get; set; }
    }

    public static class UserProfileConfiguration
    {
        public static void Config(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserProfile>(entity =>
            {
                entity.ToTable("UserProfiles");

                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id)
                    .ValueGeneratedOnAdd()
                    .HasColumnName("id");

                entity.Property(e => e.UserId)
                    .IsRequired()
                    .HasColumnName("user_id");

                entity.Property(e => e.FullName)
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnName("full_name");

                entity.Property(e => e.Phone)
                    .HasMaxLength(15)
                    .HasColumnName("phone");

                entity.Property(e => e.Gender)
                    .IsRequired()
                    .HasColumnName("gender");

                entity.Property(e => e.CreatedAt)
                    .HasColumnName("created_at")
                    .HasColumnType("timestamp without time zone")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.UpdatedAt)
                    .HasColumnName("updated_at")
                    .HasColumnType("timestamp with time zone")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.Deleted)
                    .IsRequired()
                    .HasColumnName("deleted")
                    .HasDefaultValue(false);

                entity.HasIndex(e => e.UserId).HasDatabaseName("idx_userprofile_userid");
            });
        }
    }
}
