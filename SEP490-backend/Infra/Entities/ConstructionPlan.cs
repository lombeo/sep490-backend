using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Sep490_Backend.Infra.Enums;
using Sep490_Backend.Infra.Helps;
using System.Text.Json.Serialization;

namespace Sep490_Backend.Infra.Entities
{
    public class ConstructionPlan : CommonEntity
    {
        public ConstructionPlan()
        {
            // Initialize non-nullable properties
            PlanName = string.Empty;
            ConstructPlanItems = new List<ConstructPlanItem>();
            Reviewers = new List<User>();
            // Project is a navigation property that will be initialized by EF Core
            // when the entity is loaded from the database
        }
        
        public int Id { get; set; }
        public string PlanName { get; set; }
        
        [JsonConverter(typeof(ReviewerDictionaryConverter))]
        public Dictionary<int, bool?>? Reviewer { get; set; } //UserId, isApproved (true, false, or null)
        public int ProjectId { get; set; }

        // Navigation properties
        public virtual Project Project { get; set; }
        public virtual ICollection<ConstructPlanItem> ConstructPlanItems { get; set; }
        public virtual ICollection<User> Reviewers { get; set; }
    }

    public static class ConstructionPlanConfiguration
    {
        public static void Config(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ConstructionPlan>(entity =>
            {
                entity.ToTable("ConstructionPlans");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.PlanName)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(e => e.Reviewer)
                    .HasColumnType("jsonb")
                    .HasConversion<string>(
                        new ReviewerDictionaryValueConverter(), 
                        new ValueComparer<Dictionary<int, bool?>>(
                            (c1, c2) => c1.Count == c2.Count && !c1.Except(c2).Any(),
                            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                            c => c.ToDictionary(entry => entry.Key, entry => entry.Value)
                        )
                    );

                entity.Property(e => e.ProjectId)
                    .IsRequired();

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("timestamp without time zone");

                entity.Property(e => e.UpdatedAt)
                    .HasColumnType("timestamp without time zone");

                entity.HasIndex(e => e.PlanName);
                entity.HasIndex(e => e.ProjectId);

                // Relationships
                entity.HasOne(e => e.Project)
                    .WithMany(p => p.ConstructionPlans)
                    .HasForeignKey(e => e.ProjectId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(e => e.ConstructPlanItems)
                    .WithOne(cpi => cpi.ConstructionPlan)
                    .HasForeignKey(cpi => cpi.PlanId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Many-to-many relationship with User for Reviewers
                entity.HasMany(e => e.Reviewers)
                    .WithMany(u => u.ReviewedPlans)
                    .UsingEntity<Dictionary<string, object>>(
                        "ConstructionPlanReviewers",
                        j => j
                            .HasOne<User>()
                            .WithMany()
                            .HasForeignKey("ReviewerId")
                            .HasConstraintName("FK_ConstructionPlanReviewers_Users_ReviewerId")
                            .OnDelete(DeleteBehavior.Cascade),
                        j => j
                            .HasOne<ConstructionPlan>()
                            .WithMany()
                            .HasForeignKey("ReviewedPlanId")
                            .HasConstraintName("FK_ConstructionPlanReviewers_ConstructionPlans_ReviewedPlanId")
                            .OnDelete(DeleteBehavior.Cascade),
                        j => 
                        {
                            j.HasKey("ReviewedPlanId", "ReviewerId");
                            j.ToTable("ConstructionPlanReviewers");
                        });
            });
        }
    }
}
