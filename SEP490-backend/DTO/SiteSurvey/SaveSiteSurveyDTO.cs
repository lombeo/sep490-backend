using Microsoft.AspNetCore.Http;
using Sep490_Backend.DTO;
using Sep490_Backend.Infra.Entities;
using System.Text.Json;

namespace Sep490_Backend.DTO.SiteSurvey
{
    public class SaveSiteSurveyDTO
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public string SiteSurveyName { get; set; }
        public string? ConstructionRequirements { get; set; }
        public string? EquipmentRequirements { get; set; }
        public string? HumanResourceCapacity { get; set; }
        public string? RiskAssessment { get; set; }
        public int BiddingDecision { get; set; }
        public string? ProfitAssessment { get; set; }
        public double BidWinProb { get; set; }
        public decimal EstimatedExpenses { get; set; }
        public decimal EstimatedProfits { get; set; }
        public decimal TenderPackagePrice { get; set; }
        public decimal TotalBidPrice { get; set; }
        public double DiscountRate { get; set; }
        public decimal ProjectCost { get; set; }
        public decimal FinalProfit { get; set; }
        public int Status { get; set; }
        public string? Comments { get; set; }
        public IFormFile[]? Attachments { get; set; }
        public DateTime SurveyDate { get; set; }
    }
}