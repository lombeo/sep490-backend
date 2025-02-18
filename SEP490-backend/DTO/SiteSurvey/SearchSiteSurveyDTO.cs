namespace Sep490_Backend.DTO.SiteSurvey
{
    public class SearchSiteSurveyDTO : BaseQuery
    {
        public string? SiteSurveyName { get; set; }
        public int? Status { get; set; }
    }
}
