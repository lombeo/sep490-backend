﻿using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Sep490_Backend.Infra.Entities
{
    public class SiteSurvey : CommonEntity
    {
        public SiteSurvey()
        {
            SiteSurveyName = string.Empty;
        }
        
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public string SiteSurveyName { get; set; }
        public string? ConstructionRequirements { get; set; }
        public string? EquipmentRequirements { get; set; }
        public string? HumanResourceCapacity { get; set; }
        public string? RiskAssessment { get; set; }
        public int BiddingDecision { get; set; }
        public string? ProfitAssessment { get; set; }
        public double BidWinProb { get; set; }
        public decimal EstimatedExpenses { get; set; }
        public decimal EstimatedProfits { get; set; }
        public decimal TenderPackagePrice { get; set; }
        public decimal TotalBidPrice { get; set; }
        public double DiscountRate { get; set; }
        public decimal ProjectCost { get; set; }
        public decimal FinalProfit { get; set; }
        public int Status { get; set; }
        public string? Comments { get; set; }
        public JsonDocument? Attachments { get; set; }
        public DateTime SurveyDate { get; set; }

        // Navigation properties
        public virtual Project? Project { get; set; }
        public virtual User? Conductor { get; set; }
        public virtual User? Approver { get; set; }
    }

    public static class SiteSurveyConfiguration
    {
        public static void Config(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SiteSurvey>(entity =>
            {
                entity.ToTable("SiteSurveys");

                entity.HasKey(e => e.Id);

                entity.Property(e => e.SurveyDate)
                      .IsRequired()
                      .HasColumnType("timestamp without time zone");

                entity.Property(e => e.CreatedAt)
                      .HasColumnType("timestamp without time zone");
                entity.Property(e => e.UpdatedAt)
                      .HasColumnType("timestamp without time zone");

                entity.Property(e => e.EstimatedExpenses)
                      .HasColumnType("numeric(18,2)");
                entity.Property(e => e.EstimatedProfits)
                      .HasColumnType("numeric(18,2)");
                entity.Property(e => e.TenderPackagePrice)
                      .HasColumnType("numeric(18,2)");
                entity.Property(e => e.TotalBidPrice)
                      .HasColumnType("numeric(18,2)");
                entity.Property(e => e.ProjectCost)
                      .HasColumnType("numeric(18,2)");
                entity.Property(e => e.FinalProfit)
                      .HasColumnType("numeric(18,2)");

                entity.Property(e => e.BidWinProb)
                      .HasColumnType("double precision");
                entity.Property(e => e.DiscountRate)
                      .HasColumnType("double precision");

                entity.Property(e => e.ConstructionRequirements)
                      .HasColumnType("text");
                entity.Property(e => e.EquipmentRequirements)
                      .HasColumnType("text");
                entity.Property(e => e.HumanResourceCapacity)
                      .HasColumnType("text");
                entity.Property(e => e.RiskAssessment)
                      .HasColumnType("text");
                entity.Property(e => e.ProfitAssessment)
                      .HasColumnType("text");
                entity.Property(e => e.Comments)
                      .HasColumnType("text");
                entity.Property(e => e.Attachments)
                      .HasColumnType("jsonb");
                entity.Property(e => e.SiteSurveyName)
                      .HasMaxLength(200)
                      .IsRequired();

                // Relationship with Project
                entity.HasOne(e => e.Project)
                      .WithMany(p => p.SiteSurveys)
                      .HasForeignKey(e => e.ProjectId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Relationships with User
                entity.HasOne(e => e.Conductor)
                      .WithMany(u => u.ConductedSurveys)
                      .HasForeignKey(e => e.Creator)
                      .OnDelete(DeleteBehavior.Restrict)
                      .HasConstraintName("FK_SiteSurvey_Conductor");

                entity.HasOne(e => e.Approver)
                      .WithMany(u => u.ApprovedSurveys)
                      .HasForeignKey(e => e.Updater)
                      .OnDelete(DeleteBehavior.Restrict)
                      .HasConstraintName("FK_SiteSurvey_Approver");
            });
        }
    }
}
