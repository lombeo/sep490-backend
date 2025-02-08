using Microsoft.EntityFrameworkCore;

namespace Sep490_Backend.Infra.Entities
{
    public class SiteSurvey : CommonEntity
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
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
        public string? Attachments { get; set; }
        public DateTime SurveyDate { get; set; }
    }

    public static class SiteSurveyConfiguration
    {
        public static void Config(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SiteSurvey>(entity =>
            {
                // Đặt tên bảng
                entity.ToTable("SiteSurveys");

                // Khóa chính
                entity.HasKey(e => e.Id);

                // Nếu cần, định nghĩa mối quan hệ với bảng Project (khóa ngoại)
                entity.HasOne<Project>()
                      .WithMany()
                      .HasForeignKey(e => e.ProjectId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Cấu hình các thuộc tính kiểu DateTime sang "timestamp without time zone"
                entity.Property(e => e.SurveyDate)
                      .IsRequired()
                      .HasColumnType("timestamp without time zone");

                // Các trường thuộc lớp CommonEntity
                entity.Property(e => e.CreatedAt)
                      .HasColumnType("timestamp without time zone");
                entity.Property(e => e.UpdatedAt)
                      .HasColumnType("timestamp without time zone");

                // Cấu hình cho các thuộc tính kiểu decimal (sử dụng numeric(18,2) trong PostgreSQL)
                entity.Property(e => e.EstimatedExpenses)
                      .HasColumnType("numeric(18,2)");
                entity.Property(e => e.EstimatedProfits)
                      .HasColumnType("numeric(18,2)");
                entity.Property(e => e.TenderPackagePrice)
                      .HasColumnType("numeric(18,2)");
                entity.Property(e => e.TotalBidPrice)
                      .HasColumnType("numeric(18,2)");
                entity.Property(e => e.ProjectCost)
                      .HasColumnType("numeric(18,2)");
                entity.Property(e => e.FinalProfit)
                      .HasColumnType("numeric(18,2)");

                // Cấu hình cho các thuộc tính kiểu double
                entity.Property(e => e.BidWinProb)
                      .HasColumnType("double precision");
                entity.Property(e => e.DiscountRate)
                      .HasColumnType("double precision");

                // Cấu hình cho các thuộc tính kiểu string (sử dụng "text" cho trường dài)
                entity.Property(e => e.ConstructionRequirements)
                      .HasColumnType("text");
                entity.Property(e => e.EquipmentRequirements)
                      .HasColumnType("text");
                entity.Property(e => e.HumanResourceCapacity)
                      .HasColumnType("text");
                entity.Property(e => e.RiskAssessment)
                      .HasColumnType("text");
                entity.Property(e => e.ProfitAssessment)
                      .HasColumnType("text");
                entity.Property(e => e.Comments)
                      .HasColumnType("text");
                entity.Property(e => e.Attachments)
                      .HasColumnType("text");

                // Các thuộc tính kiểu int (như BiddingDecision, Status, v.v.) không cần cấu hình thêm
            });
        }
    }
}
