namespace Sep490_Backend.DTO.Material
{
    public class MaterialCreateDTO : BaseQuery
    {
        public string MaterialCode { get; set; }  // Mã vật tư
        public string MaterialName { get; set; }  // Tên vật tư
        public string Unit { get; set; }  // Đơn vị tính
        public string Branch { get; set; }  // Chi nhánh phân phối
        public string MadeIn { get; set; }  // Xuất xứ 
        public string ChassisNumber { get; set; }  // Số khung ?
        public decimal WholesalePrice { get; set; }  // Giá sỉ
        public decimal RetailPrice { get; set; }  // Giá lẻ
        public int Inventory { get; set; }  // Tồn kho
        public string Attachment { get; set; }  // đường dẫn?
        public DateTime? ExpireDate { get; set; }  // Ngày hết hạn
        public DateTime? ProductionDate { get; set; }  // Ngày sản xuất
        public string Description { get; set; }  // Mô tả
    }
}
