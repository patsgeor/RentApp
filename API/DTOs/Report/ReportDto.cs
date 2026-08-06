using System;

namespace API.DTOs.Report;

/// <summary>Τα σύνολα δεδομένων που μπορεί να ζητήσει ο χρήστης προς εξαγωγή.</summary>
public enum ReportDataset
{
    Kpi          = 0,
    Income       = 1,
    Expenses     = 2,
    Contracts    = 3,
    Assets       = 4,
    Installments = 5,
    Customers    = 6
}

/// <summary>
/// «Πάγια της περιόδου» είναι διφορούμενο: άλλο πράγμα όσα καταχωρήθηκαν στο
/// μητρώο και άλλο όσα απέδωσαν έσοδα. Ο χρήστης το επιλέγει ρητά.
/// </summary>
public enum AssetPeriodMode
{
    Registered = 0,
    Rented     = 1
}

public class ReportRequestDto
{
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public List<ReportDataset> Datasets { get; set; } = [];
    public AssetPeriodMode AssetMode { get; set; } = AssetPeriodMode.Registered;
}

/// <summary>Πλήθος γραμμών ανά σύνολο, ώστε ο χρήστης να ξέρει τι θα κατεβάσει.</summary>
public class ReportPreviewRowDto
{
    public ReportDataset Dataset { get; set; }
    public string Label { get; set; } = "";
    public int RowCount { get; set; }
    public bool ExceedsSheetLimit { get; set; }
}

public class ReportPreviewDto
{
    public List<ReportPreviewRowDto> Rows { get; set; } = [];
    public int TotalRows { get; set; }
    public int MaxRowsPerSheet { get; set; }
    public int MaxRowsTotal { get; set; }
    public bool ExceedsLimit { get; set; }
    public string? Message { get; set; }
}

/// <summary>
/// Συγκεντρωτικά της επιλεγμένης περιόδου. Διαφέρει σκόπιμα από το KpiDto του
/// Πίνακα Ελέγχου: εκείνο είναι καρφωμένο σε «τρέχων μήνας/έτος» και αναμειγνύει
/// στιγμιότυπα με μεγέθη περιόδου.
/// </summary>
public class PeriodKpiDto
{
    public decimal Income { get; set; }
    public decimal Expenses { get; set; }
    public decimal Net { get; set; }

    public int ContractsCreated { get; set; }
    public int ContractsStarted { get; set; }
    public int ContractsEnded { get; set; }

    public int AssetsRegistered { get; set; }
    public int CustomersRegistered { get; set; }

    public int InstallmentsDue { get; set; }
    public decimal InstallmentsDueAmount { get; set; }
    public decimal CollectedInPeriod { get; set; }

    // Στιγμιότυπα «τώρα» — δεν αφορούν την περίοδο αλλά δίνουν πλαίσιο.
    public decimal OutstandingNow { get; set; }
    public int OverdueCountNow { get; set; }
    public decimal OverdueAmountNow { get; set; }
    public int TotalAssets { get; set; }
    public int ActiveContractsNow { get; set; }
}

// ── Επίπεδες γραμμές εξαγωγής ────────────────────────────────────────────────
// Σκοπίμως χωρίς ένθετες λίστες: σε φύλλο Excel δεν έχουν νόημα, ενώ στο SQL
// προκαλούν πολλαπλασιασμό γραμμών και επιβαρύνουν τη μνήμη.

public class PaymentRowDto
{
    public DateTime PaymentDate { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = "";
    public string? CustomerName { get; set; }
    public string? ReferenceCode { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }
    public decimal UnallocatedAmount { get; set; }
}

public class ContractRowDto
{
    public string? ReferenceCode { get; set; }
    public string CustomerName { get; set; } = "";
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal OutstandingAmount { get; set; }
    public string Status { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public class AssetRowDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string AssetTypeName { get; set; } = "";
    public decimal Cost { get; set; }
    public string Status { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public class AssetAttributeRowDto
{
    public string AssetName { get; set; } = "";
    public string AssetTypeName { get; set; } = "";
    public string Field { get; set; } = "";
    public string Value { get; set; } = "";
}

public class InstallmentRowDto
{
    public string? ContractReferenceCode { get; set; }
    public string CustomerName { get; set; } = "";
    public int InstallmentNumber { get; set; }
    public DateTime DueDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AllocatedAmount { get; set; }
    public decimal OutstandingAmount { get; set; }
    public string Status { get; set; } = "";
}

public class CustomerRowDto
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string? Afm { get; set; }
    public string? Dou { get; set; }
    public string? Address { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Όλα τα δεδομένα ενός workbook, ήδη φορτωμένα και επίπεδα.</summary>
public class ReportDataDto
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public AssetPeriodMode AssetMode { get; set; }

    public PeriodKpiDto? Kpi { get; set; }
    public List<PaymentRowDto>? Income { get; set; }
    public List<PaymentRowDto>? Expenses { get; set; }
    public List<ContractRowDto>? Contracts { get; set; }
    public List<AssetRowDto>? Assets { get; set; }
    public List<AssetAttributeRowDto>? AssetAttributes { get; set; }
    public List<InstallmentRowDto>? Installments { get; set; }
    public List<CustomerRowDto>? Customers { get; set; }
}
