using Microsoft.EntityFrameworkCore;

namespace Sep490_Backend.Infra.Entities
{
    public class User : CommonEntity
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public string Role { get; set; }
        public string FullName { get; set; }
        public string Phone { get; set; }
        public bool Gender { get; set; }
        public DateTime Dob { get; set; }
        public bool IsVerify { get; set; }
    }

    public static class UserAuthenConfiguration
    {
        public static void Config(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("Users");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Deleted);
                entity.HasIndex(e => e.IsVerify);
                entity.Property(e => e.UpdatedAt)
                    .HasColumnType("timestamp without time zone");
                entity.Property(e => e.CreatedAt)
                    .HasColumnType("timestamp without time zone");
                entity.HasIndex(e => e.Username);
                entity.HasIndex(e => e.Email);
                entity.HasIndex(e => e.FullName);
                entity.HasIndex(e => e.Phone);
                entity.HasIndex(e => e.Gender);
                entity.Property(e => e.Dob)
                      .HasColumnType("timestamp without time zone");
                entity.Property(e => e.Creator);
                entity.Property(e => e.Updater);
            });
        }
    }
}
