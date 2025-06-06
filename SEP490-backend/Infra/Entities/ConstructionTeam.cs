﻿using Microsoft.EntityFrameworkCore;

namespace Sep490_Backend.Infra.Entities
{
    public class ConstructionTeam : CommonEntity
    {
        public int Id { get; set; }
        public string TeamName { get; set; }
        public int TeamManager { get; set; } //UserId
        public string? Description { get; set; }

        // Navigation properties
        public virtual User Manager { get; set; }
        public virtual ICollection<User> Members { get; set; }
        public virtual ICollection<ConstructPlanItem> ConstructPlanItems { get; set; }
    }

    public static class ConstructionTeamConfiguration
    {
        public static void Config(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ConstructionTeam>(entity =>
            {
                entity.ToTable("ConstructionTeams");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.TeamName)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(e => e.TeamManager)
                    .IsRequired();

                entity.Property(e => e.Description)
                    .HasColumnType("text");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("timestamp without time zone");

                entity.Property(e => e.UpdatedAt)
                    .HasColumnType("timestamp without time zone");

                entity.HasIndex(e => e.TeamName);
                entity.HasIndex(e => e.TeamManager).HasFilter("\"Deleted\" = false");

                // Relationships
                // One-to-many with Manager (one user can manage multiple teams)
                entity.HasOne(e => e.Manager)
                    .WithMany(u => u.ManagedTeams)
                    .HasForeignKey(e => e.TeamManager)
                    .OnDelete(DeleteBehavior.Restrict);

                // Many-to-many with Member Users (one user in only one team)
                entity.HasMany(e => e.Members)
                    .WithOne(u => u.Team)
                    .HasForeignKey(u => u.TeamId)
                    .IsRequired(false)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
