﻿using Microsoft.EntityFrameworkCore;
using Sep490_Backend.Infra.Enums;
using System.Text.Json;

namespace Sep490_Backend.Infra.Entities
{
    public class Contract : CommonEntity
    {
        public int Id { get; set; }
        public string ContractCode { get; set; }
        public string ContractName { get; set; }
        public int ProjectId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int EstimatedDays { get; set; }
        public ContractStatusEnum Status { get; set; }
        public decimal Tax { get; set; }
        public DateTime SignDate { get; set; }
        public JsonDocument? Attachments { get; set; }

        // Navigation properties
        public virtual Project Project { get; set; }
        public virtual ICollection<ContractDetail> ContractDetails { get; set; }
    }

    public static class ContractConfiguration
    {
        public static void Config(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Contract>(entity =>
            {
                entity.ToTable("Contracts");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ContractCode).IsRequired();
                entity.Property(e => e.ContractName).IsRequired();
                entity.Property(e => e.ProjectId).IsRequired();
                entity.Property(e => e.StartDate).IsRequired();
                entity.Property(e => e.EndDate).IsRequired();
                entity.Property(e => e.EstimatedDays).IsRequired();
                entity.Property(e => e.Status).IsRequired();
                entity.Property(e => e.Tax).IsRequired();
                entity.Property(e => e.SignDate).IsRequired();            
                entity.Property(e => e.Attachments).HasColumnType("jsonb");
                entity.Property(e => e.CreatedAt).IsRequired();
                entity.Property(e => e.Creator).IsRequired();
                entity.Property(e => e.UpdatedAt).IsRequired();
                entity.Property(e => e.Updater).IsRequired();
                entity.Property(e => e.Deleted).IsRequired().HasDefaultValue(false);

                entity.HasOne(e => e.Project)
                    .WithMany(p => p.Contracts)
                    .HasForeignKey(e => e.ProjectId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(e => e.ContractDetails)
                    .WithOne(cd => cd.Contract)
                    .HasForeignKey(cd => cd.ContractId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
