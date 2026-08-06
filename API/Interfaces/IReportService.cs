using API.DTOs.Report;

namespace API.Interfaces;

public interface IReportService
{
    /// <summary>Πλήθος γραμμών ανά σύνολο δεδομένων, χωρίς φόρτωση των ίδιων των δεδομένων.</summary>
    Task<ReportPreviewDto> PreviewAsync(ReportRequestDto request);

    /// <summary>Παράγει το workbook. Πετά BadRequestException αν παραβιάζονται τα όρια.</summary>
    Task<ReportDataDto> LoadAsync(ReportRequestDto request);
}
