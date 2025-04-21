namespace Sep490_Backend.Infra.Enums
{
    public enum InspectionReportStatus
    {
        Draft = 0,
        Submitted = 1,
        Approved = 2,
        Rejected = 3
    }

    public enum InspectionDecision
    {
        None = 0,
        Pass = 1,
        PassWithRemarks = 2,
        Fail = 3
    }
} 