namespace API.Services.Reports;

/// <summary>
/// Όρια εξαγωγής, ρυθμιζόμενα από το appsettings ώστε να προσαρμόζονται στο
/// μηχάνημα χωρίς νέο deployment. Το ClosedXML κρατά ολόκληρο το workbook στη
/// μνήμη, οπότε το όριο γραμμών είναι η ουσιαστική προστασία από OutOfMemory.
/// </summary>
public class ReportSettings
{
    public int MaxRowsPerSheet { get; set; } = 25_000;
    public int MaxRowsTotal { get; set; } = 100_000;
    public int MaxConcurrentExports { get; set; } = 2;

    /// <summary>Δευτερόλεπτα αναμονής για ελεύθερη θέση εξαγωγής πριν επιστραφεί 503.</summary>
    public int ConcurrencyWaitSeconds { get; set; } = 20;

    /// <summary>Μέγιστο εύρος περιόδου σε μήνες.</summary>
    public int MaxPeriodMonths { get; set; } = 12;
}
