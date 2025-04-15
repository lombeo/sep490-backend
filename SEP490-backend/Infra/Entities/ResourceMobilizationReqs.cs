using Microsoft.EntityFrameworkCore;
using Sep490_Backend.DTO.ResourceReqs;
using Sep490_Backend.Infra.Enums;
using System.Text.Json;

namespace Sep490_Backend.Infra.Entities
{
    public class ResourceMobilizationReqs : CommonEntity
    {
        public ResourceMobilizationReqs()
        {
            RequestCode = string.Empty;
            ResourceMobilizationDetails = new List<RequestDetails>();
        }
        
        public int Id { get; set; }
        public string RequestCode { get; set; } //unique
        public int ProjectId { get; set; } //project
        public string? RequestName { get; set; }
        public List<RequestDetails> ResourceMobilizationDetails { get; set; } //jsonb
        public string? Description { get; set; }
        public PriorityLevel PriorityLevel { get; set; }
        public RequestStatus Status { get; set; }
        public JsonDocument? Attachments { get; set; }
        public DateTime RequestDate { get; set; }

        // Navigation properties
        public virtual Project? Project { get; set; }
        public virtual User? Requester { get; set; }
        public virtual User? Approver { get; set; }
    }

    public static class ResourceMobilizationReqsConfiguration
    {
        public static void Config(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ResourceMobilizationReqs>(entity =>
            {
                entity.ToTable("ResourceMobilizationReqs");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.RequestCode)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(e => e.ProjectId)
                    .IsRequired();

                entity.Property(e => e.RequestName)
                    .HasMaxLength(200);

                entity.Property(e => e.ResourceMobilizationDetails)
                    .HasColumnType("jsonb");

                entity.Property(e => e.Description)
                    .HasColumnType("text");

                entity.Property(e => e.PriorityLevel)
                    .IsRequired();

                entity.Property(e => e.Status)
                    .IsRequired();

                entity.Property(e => e.Attachments)
                    .HasColumnType("jsonb");

                entity.Property(e => e.RequestDate)
                    .IsRequired()
                    .HasColumnType("timestamp without time zone");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("timestamp without time zone");

                entity.Property(e => e.UpdatedAt)
                    .HasColumnType("timestamp without time zone");

                entity.HasIndex(e => e.RequestCode).HasFilter("\"Deleted\" = false")
                    .IsUnique();
                entity.HasIndex(e => e.ProjectId);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.RequestDate);

                // Relationships
                entity.HasOne(e => e.Project)
                    .WithMany(p => p.ResourceMobilizationReqs)
                    .HasForeignKey(e => e.ProjectId)
                    .OnDelete(DeleteBehavior.Restrict);

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
