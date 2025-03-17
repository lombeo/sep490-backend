using Microsoft.EntityFrameworkCore;
using Sep490_Backend.Infra.Enums;

namespace Sep490_Backend.Infra.Entities
{
    public class ActionLog : CommonEntity
    {
        public int Id { get; set; }
        public ActionLogType LogType { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
    }

    public static class ActionLogConfiguration
    {
        public static void Config(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ActionLog>(entity =>
            {
                entity.ToTable("ActionLogs");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.LogType)
                    .IsRequired();

                entity.Property(e => e.Title)
                    .HasMaxLength(200);

                entity.Property(e => e.Description)
                    .HasColumnType("text");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("timestamp without time zone");

                entity.Property(e => e.UpdatedAt)
                    .HasColumnType("timestamp without time zone");

                entity.HasIndex(e => e.LogType);
                entity.HasIndex(e => e.CreatedAt);
            });
        }
    }
}
