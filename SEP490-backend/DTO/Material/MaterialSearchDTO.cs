namespace Sep490_Backend.DTO.Material
{
    public class MaterialSearchDTO : BaseQuery
    {
        public string? Keyword { get; set; }
        public string? MaterialCode { get; set; }
        public string? MaterialName { get; set; }
        public string? Unit { get; set; }
    }
}
