using Sep490_Backend.DTO.InspectionReport;

namespace Sep490_Backend.Services.InspectionReportService
{
    public interface IInspectionReportService
    {
        Task<InspectionReportDTO> Save(SaveInspectionReportDTO model, int actionBy);
        Task<int> Delete(int id, int actionBy);
        Task<List<InspectionReportDTO>> List(SearchInspectionReportDTO model);
        Task<InspectionReportDTO> Detail(int id, int actionBy);
        Task<List<InspectionReportDTO>> GetByProject(int projectId, int actionBy);
    }
} 