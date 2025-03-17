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
        public int FromProjectId { get; set; } //project
        public int ToProjectId { get; set; } //project
        public string? RequestName { get; set; }
        public List<RequestDetails> ResourceAllocationDetails { get; set; } //jsonb
        public string? Description { get; set; }
        public PriorityLevel PriorityLevel { get; set; }
        public RequestStatus Status { get; set; }
        public JsonDocument? Attachments { get; set; }

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

                entity.Property(e => e.FromProjectId)
                    .IsRequired();

                entity.Property(e => e.ToProjectId)
                    .IsRequired();

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

                entity.HasIndex(e => e.RequestCode)
                    .IsUnique();
                entity.HasIndex(e => e.FromProjectId);
                entity.HasIndex(e => e.ToProjectId);
                entity.HasIndex(e => e.Status);

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

                entity.HasOne(e => e.Requester)
                    .WithMany()
                    .HasForeignKey(e => e.Creator)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Approver)
                    .WithMany()
                    .HasForeignKey(e => e.Updater)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
