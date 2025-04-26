using Microsoft.EntityFrameworkCore;

namespace Sep490_Backend.Infra.Entities
{
    public class RefreshToken
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Token { get; set; }
        public DateTime ExpiryDate { get; set; }
        public bool IsRevoked { get; set; }
        
        // Navigation property
        public virtual User? User { get; set; }
    }
    public static class RefreshTokenConfiguration
    {
        public static void Config(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RefreshToken>(entity =>
            {
                entity.ToTable("RefreshTokens");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id)
                   .HasColumnName("id")
                   .IsRequired();

                entity.Property(e => e.UserId)
                    .HasColumnName("user_id")
                    .IsRequired();

                entity.Property(e => e.Token)
                    .HasColumnName("token")
                    .HasMaxLength(256)
                    .IsRequired();

                entity.Property(e => e.ExpiryDate)
                    .HasColumnName("expiry_date")
                    .HasColumnType("timestamp without time zone")
                    .IsRequired();

                entity.Property(e => e.IsRevoked)
                    .HasColumnName("is_revoked")
                    .HasDefaultValue(false)
                    .IsRequired();

                entity.HasIndex(e => e.UserId)
                    .HasDatabaseName("ix_refresh_tokens_user_id");

                // Relationship - Making it optional to address global query filter warnings
                entity.HasOne(e => e.User)
                      .WithMany(u => u.RefreshTokens)
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade)
                      .IsRequired(false);

                // Add a query filter to match the one on User entity
                modelBuilder.Entity<RefreshToken>()
                    .HasQueryFilter(rt => rt.User == null || !rt.User.Deleted);
            });
        }
    }
}
