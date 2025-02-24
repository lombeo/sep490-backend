using Microsoft.EntityFrameworkCore;
using Sep490_Backend.Infra.Entities;

namespace Sep490_Backend.Infra.Entities
{
    public class Material : CommonEntity
    {
        public int Id { get; set; } 
        public string MaterialCode { get; set; }  // Mã vật tư
        public string MaterialName { get; set; }  // Tên vật tư
        public string? Unit { get; set; }  // Đơn vị tính
        public string? Branch { get; set; }  // Chi nhánh phân phối
        public string? MadeIn { get; set; }  // Xuất xứ 
        public string? ChassisNumber { get; set; }  // Số khung ?
        public decimal? WholesalePrice { get; set; }  // Giá sỉ
        public decimal? RetailPrice { get; set; }  // Giá lẻ
        public int? Inventory { get; set; }  // Tồn kho
        public string? Attachment { get; set; }  // đường dẫn?
        public DateTime? ExpireDate { get; set; }  // Ngày hết hạn
        public DateTime? ProductionDate { get; set; }  // Ngày sản xuất
        public string Description { get; set; }  // Mô tả
    }
}

public static class MaterialConfiguration
{
    public static void Config(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Material>(entity =>
        {
            entity.ToTable("Materials");

            entity.HasKey(m => m.Id);

            entity.Property(m => m.MaterialCode)
                   .IsRequired()
                   .HasMaxLength(255);

            entity.Property(m => m.MaterialName)
                   .IsRequired()
                   .HasMaxLength(255);

            entity.Property(m => m.Unit)
                   .HasMaxLength(50);

            entity.Property(m => m.Branch)
                   .HasMaxLength(255);

            entity.Property(m => m.MadeIn)
                   .HasMaxLength(255);

            entity.Property(m => m.ChassisNumber)
                   .HasMaxLength(255);

            entity.Property(m => m.WholesalePrice)
                   .HasColumnType("decimal(18,2)");

            entity.Property(m => m.RetailPrice)
                   .HasColumnType("decimal(18,2)");

            entity.Property(m => m.Inventory)
                   .HasDefaultValue(0);

            entity.Property(m => m.Attachment)
                   .HasColumnType("text");

            entity.Property(m => m.ExpireDate)
                   .HasColumnType("timestamp without time zone");

            entity.Property(m => m.ProductionDate)
                   .HasColumnType("timestamp without time zone");

            entity.Property(m => m.Description)
                   .HasColumnType("text");

            entity.Property(e => e.CreatedAt)
                        .HasColumnType("timestamp without time zone");

            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp without time zone");
        });
    }
}

