using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace Sep490_Backend.Infra.Entities
{
    public class User : CommonEntity
    {
        public User()
        {
            Username = string.Empty;
            Email = string.Empty;
            PasswordHash = string.Empty;
            Role = string.Empty;
            FullName = string.Empty;
            Phone = string.Empty;
            RefreshTokens = new List<RefreshToken>();
            ProjectUsers = new List<ProjectUser>();
            ResourceAllocations = new List<ConstructPlanItemDetail>();
            ReviewedPlans = new List<ConstructionPlan>();
            ConductedSurveys = new List<SiteSurvey>();
            ApprovedSurveys = new List<SiteSurvey>();
            RequestedMobilizations = new List<ResourceMobilizationReqs>();
            ApprovedMobilizations = new List<ResourceMobilizationReqs>();
            RequestedAllocations = new List<ResourceAllocationReqs>();
            ApprovedAllocations = new List<ResourceAllocationReqs>();
        }

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
        public int? TeamId { get; set; }  // Foreign key for membership in a team
        public string? PicProfile { get; set; } // Profile picture URL
        public string? Address { get; set; } // User address
        
        // Navigation properties
        public virtual ICollection<RefreshToken> RefreshTokens { get; set; }
        public virtual ICollection<ProjectUser> ProjectUsers { get; set; }
        public virtual Vehicle? Vehicle { get; set; }
        public virtual ConstructionTeam? Team { get; set; }
        public virtual ICollection<ConstructPlanItemDetail> ResourceAllocations { get; set; }
        public virtual ICollection<ConstructionPlan> ReviewedPlans { get; set; }
        public virtual ICollection<SiteSurvey> ConductedSurveys { get; set; }
        public virtual ICollection<SiteSurvey> ApprovedSurveys { get; set; }
        public virtual ICollection<ResourceMobilizationReqs> RequestedMobilizations { get; set; }
        public virtual ICollection<ResourceMobilizationReqs> ApprovedMobilizations { get; set; }
        public virtual ICollection<ResourceAllocationReqs> RequestedAllocations { get; set; }
        public virtual ICollection<ResourceAllocationReqs> ApprovedAllocations { get; set; }
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
                entity.Property(e => e.PicProfile)
                      .IsRequired(false);
                entity.Property(e => e.Address)
                      .IsRequired(false);
                entity.Property(e => e.Creator);
                entity.Property(e => e.Updater);

                // Relationships
                entity.HasMany(e => e.RefreshTokens)
                      .WithOne()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(e => e.ProjectUsers)
                      .WithOne()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Vehicle)
                      .WithOne(v => v.User)
                      .HasForeignKey<Vehicle>(v => v.Driver)
                      .OnDelete(DeleteBehavior.Restrict);

                // Single Construction Team relationship
                entity.HasOne(e => e.Team)
                      .WithMany(ct => ct.Members)
                      .HasForeignKey(u => u.TeamId)
                      .IsRequired(false)
                      .OnDelete(DeleteBehavior.Restrict);

                // Property configuration for nullable foreign key
                entity.Property(e => e.TeamId)
                      .IsRequired(false);

                // Construction Plan relationships - ReviewedPlans (many-to-many)
                entity.HasMany(e => e.ReviewedPlans)
                      .WithMany(cp => cp.Reviewers)
                      .UsingEntity<Dictionary<string, object>>(
                        "ConstructionPlanReviewers",
                        j => j
                            .HasOne<ConstructionPlan>()
                            .WithMany()
                            .HasForeignKey("ReviewedPlanId")
                            .HasConstraintName("FK_ConstructionPlanReviewers_ConstructionPlans_ReviewedPlanId")
                            .OnDelete(DeleteBehavior.Cascade),
                        j => j
                            .HasOne<User>()
                            .WithMany()
                            .HasForeignKey("ReviewerId")
                            .HasConstraintName("FK_ConstructionPlanReviewers_Users_ReviewerId")
                            .OnDelete(DeleteBehavior.Cascade),
                        j => 
                        {
                            j.HasKey("ReviewedPlanId", "ReviewerId");
                            j.ToTable("ConstructionPlanReviewers");
                        });

                // Many-to-many relationship with User for ResourceAllocations
                entity.HasMany(u => u.ResourceAllocations)
                    .WithOne(d => d.User)
                    .HasForeignKey(d => d.ResourceId)
                    .HasConstraintName("FK_ConstructPlanItemDetails_Users_ResourceId")
                    .HasPrincipalKey(u => u.Id)
                    .OnDelete(DeleteBehavior.SetNull);

                // Resource Mobilization relationships
                entity.HasMany(e => e.RequestedMobilizations)
                      .WithOne(rm => rm.Requester)
                      .HasForeignKey(rm => rm.Creator)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(e => e.ApprovedMobilizations)
                      .WithOne(rm => rm.Approver)
                      .HasForeignKey(rm => rm.Updater)
                      .OnDelete(DeleteBehavior.Restrict);

                // Resource Allocation relationships
                entity.HasMany(e => e.RequestedAllocations)
                      .WithOne(ra => ra.Requester)
                      .HasForeignKey(ra => ra.Creator)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(e => e.ApprovedAllocations)
                      .WithOne(ra => ra.Approver)
                      .HasForeignKey(ra => ra.Updater)
                      .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
