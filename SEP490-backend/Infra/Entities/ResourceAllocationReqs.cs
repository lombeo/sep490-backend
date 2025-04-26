using Microsoft.EntityFrameworkCore;
using Sep490_Backend.DTO.ResourceReqs;
using Sep490_Backend.Infra.Enums;
using System.Text.Json;

namespace Sep490_Backend.Infra.Entities
{
    public class ResourceAllocationReqs : CommonEntity
    {
        public int Id { get; set; }
        public string RequestCode { get; set; } //unique
        public int RequestType { get; set; } // Added to match frontend: PROJECT_TO_PROJECT(1), PROJECT_TO_TASK(2), TASK_TO_TASK(3)
        public int FromProjectId { get; set; } //project
        public int ToProjectId { get; set; } //project
        public int? FromTaskId { get; set; } // Added to support task allocation
        public int? ToTaskId { get; set; } // Added to support task allocation
        public string? RequestName { get; set; }
        public List<RequestDetails> ResourceAllocationDetails { get; set; } //jsonb
        public string? Description { get; set; }
        public PriorityLevel PriorityLevel { get; set; }
        public RequestStatus Status { get; set; }
        public JsonDocument? Attachments { get; set; }
        public DateTime RequestDate { get; set; }

        // Navigation properties
        public virtual Project FromProject { get; set; }
        public virtual Project ToProject { get; set; }
        public virtual User Requester { get; set; }
        public virtual User Approver { get; set; }
    }

    public static class ResourceAllocationReqsConfiguration
    {
        public static void Config(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ResourceAllocationReqs>(entity =>
            {
                entity.ToTable("ResourceAllocationReqs");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.RequestCode)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(e => e.RequestType)
                    .IsRequired()
                    .HasDefaultValue(1); // Default to PROJECT_TO_PROJECT

                entity.Property(e => e.FromProjectId)
                    .IsRequired();

                entity.Property(e => e.ToProjectId)
                    .IsRequired();

                entity.Property(e => e.FromTaskId)
                    .IsRequired(false);

                entity.Property(e => e.ToTaskId)
                    .IsRequired(false);

                entity.Property(e => e.RequestName)
                    .HasMaxLength(200);

                entity.Property(e => e.ResourceAllocationDetails)
                    .HasColumnType("jsonb");

                entity.Property(e => e.Description)
                    .HasColumnType("text");

                entity.Property(e => e.PriorityLevel)
                    .IsRequired();

                entity.Property(e => e.Status)
                    .IsRequired();

                entity.Property(e => e.Attachments)
                    .HasColumnType("jsonb");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("timestamp without time zone");

                entity.Property(e => e.UpdatedAt)
                    .HasColumnType("timestamp without time zone");

                entity.HasIndex(e => e.RequestCode).HasFilter("\"Deleted\" = false")
                    .IsUnique();
                entity.HasIndex(e => e.FromProjectId);
                entity.HasIndex(e => e.ToProjectId);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.RequestType);
                entity.HasIndex(e => e.FromTaskId);
                entity.HasIndex(e => e.ToTaskId);

                // Relationships
                entity.HasOne(e => e.FromProject)
                    .WithMany()
                    .HasForeignKey(e => e.FromProjectId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("FK_ResourceAllocationReqs_FromProject");

                entity.HasOne(e => e.ToProject)
                    .WithMany()
                    .HasForeignKey(e => e.ToProjectId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("FK_ResourceAllocationReqs_ToProject");

                // User relationships
                entity.HasOne(e => e.Requester)
                    .WithMany(u => u.RequestedAllocations)
                    .HasForeignKey(e => e.Creator)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("FK_ResourceAllocationReqs_Requester");

                entity.HasOne(e => e.Approver)
                    .WithMany(u => u.ApprovedAllocations)
                    .HasForeignKey(e => e.Updater)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("FK_ResourceAllocationReqs_Approver");
            });
        }
    }
}
