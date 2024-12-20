using Microsoft.EntityFrameworkCore;
using Sep490_Backend.Infra.Enums;

namespace Sep490_Backend.Infra.Entities
{
    public class OTP
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public ReasonOTP Reason { get; set; }
        public string Code { get; set; }
        public DateTime ExpiryTime { get; set; }
    }

    //public static class OtpConfiguration
    //{
    //    public static void Config(ModelBuilder modelBuilder)
    //    {
    //        modelBuilder.Entity<OTP>(entity =>
    //        {
    //            entity.ToTable("otps");

    //            entity.HasKey(e => e.Id);

    //            entity.Property(e => e.Id)
    //                .HasColumnName("id")
    //                .IsRequired();

    //            entity.Property(e => e.Reason)
    //                .HasColumnName("reason")
    //                .IsRequired();

    //            entity.Property(e => e.UserId)
    //                .HasColumnName("user_id")
    //                .IsRequired();

    //            entity.Property(e => e.Code)
    //                .HasColumnName("code")
    //                .HasMaxLength(12)
    //                .IsRequired();

    //            entity.Property(e => e.ExpiryTime)
    //                .HasColumnName("expiry_time")
    //                .HasColumnType("timestamp with time zone")
    //                .IsRequired();

    //            entity.HasIndex(e => e.UserId)
    //                .HasName("ix_user_id");
    //        });
    //    }
    //}
}
