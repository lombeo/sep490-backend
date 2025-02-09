namespace Sep490_Backend.DTO.SiteSurveyDTO
{
    public class SearchSiteSurveyDTO : BaseQueryDTO
    {
        public string? SiteSurveyName { get; set; }
        public int? Status { get; set; }
    }
}
