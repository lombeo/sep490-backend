using System;

namespace Sep490_Backend.DTO.Material
{
    public class MaterialDetailDTO
    {
        public int Id { get; set; }
        public string MaterialCode { get; set; } = string.Empty;
        public string MaterialName { get; set; } = string.Empty;
        public string? Unit { get; set; }
        public string? Branch { get; set; }
        public string? MadeIn { get; set; }
        public string? ChassisNumber { get; set; }
        public decimal? WholesalePrice { get; set; }
        public decimal? RetailPrice { get; set; }
        public int? Inventory { get; set; }
        public string? Attachment { get; set; }
        public DateTime? ExpireDate { get; set; }
        public DateTime? ProductionDate { get; set; }
        public string? Description { get; set; }
        public bool CanRollBack { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int? Creator { get; set; }
        public int? Updater { get; set; }
    }
} 