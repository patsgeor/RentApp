using API.Data.Contexts;
using API.DTOs.Report;
using API.Errors;
using API.Extensions;
using API.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using static API.Entities.Enums;

namespace API.Services.Reports;

/// <summary>
/// Αντλεί τα δεδομένα των αναφορών. Σκοπίμως δεν γνωρίζει τίποτα για Excel —
/// η μορφοποίηση ζει στον ExcelWorkbookBuilder, ώστε τα ερωτήματα να μπορούν
/// να ελεγχθούν αυτόνομα και η βιβλιοθήκη παραγωγής να αλλάξει χωρίς να τα αγγίξει.
///
/// Όλα τα ερωτήματα είναι unpaged και AsNoTracking. Η απομόνωση ενοίκου
/// επιβάλλεται αυτόματα από τα global query filters του AppDbContext — δεν
/// χρησιμοποιείται πουθενά IgnoreQueryFilters.
/// </summary>
public class ReportService(AppDbContext context, IOptions<ReportSettings> settings) : IReportService
{
    private readonly ReportSettings _limits = settings.Value;

    // ── Public API ──────────────────────────────────────────────────────────

    public async Task<ReportPreviewDto> PreviewAsync(ReportRequestDto request)
    {
        var (from, to) = NormalizePeriod(request);
        var datasets = Requested(request);

        var rows = new List<ReportPreviewRowDto>();

        foreach (var ds in datasets)
        {
            // Το KPI είναι μία γραμμή ανά δείκτη — δεν κινδυνεύει από όγκο.
            var count = ds == ReportDataset.Kpi ? 1 : await CountAsync(ds, from, to, request.AssetMode);

            // Προσθέτει μία γραμμή για κάθε σύνολο δεδομένων, με τον αριθμό γραμμών και αν ξεπερνάει το όριο ανά φύλλο.
            rows.Add(new ReportPreviewRowDto
            {
                Dataset           = ds,
                Label             = Label(ds),
                RowCount          = count,
                ExceedsSheetLimit = count > _limits.MaxRowsPerSheet // το οριο ανά φύλλο. το παίρνει απο το ReportSettings.
            });
        }

        var total = rows.Sum(r => r.RowCount);
        var exceeds = total > _limits.MaxRowsTotal || rows.Any(r => r.ExceedsSheetLimit);

        return new ReportPreviewDto
        {
            Rows            = rows,
            TotalRows       = total,
            MaxRowsPerSheet = _limits.MaxRowsPerSheet,
            MaxRowsTotal    = _limits.MaxRowsTotal,
            ExceedsLimit    = exceeds,
            Message         = exceeds ? LimitMessage(rows, total) : null
        };
    }

    public async Task<ReportDataDto> LoadAsync(ReportRequestDto request)
    {
        var (from, to) = NormalizePeriod(request);
        var datasets = Requested(request);

        // Ο server επαναλαμβάνει τον έλεγχο ορίων: το UI μπορεί να παρακαμφθεί.
        var preview = await PreviewAsync(request);
        if (preview.ExceedsLimit)
            throw new BadRequestException(preview.Message!);

        var data = new ReportDataDto { From = from, To = to, AssetMode = request.AssetMode };

        if (datasets.Contains(ReportDataset.Kpi))
            data.Kpi = await BuildKpiAsync(from, to);

        if (datasets.Contains(ReportDataset.Income))
            data.Income = await PaymentsAsync(from, to, TransactionType.Income);

        if (datasets.Contains(ReportDataset.Expenses))
            data.Expenses = await PaymentsAsync(from, to, TransactionType.Expense);

        if (datasets.Contains(ReportDataset.Contracts))
            data.Contracts = await ContractsAsync(from, to);

        if (datasets.Contains(ReportDataset.Assets))
        {
            data.Assets = await AssetsAsync(from, to, request.AssetMode);
            data.AssetAttributes = await AssetAttributesAsync(data.Assets.Select(a => a.Id).ToList());
        }

        if (datasets.Contains(ReportDataset.Installments))
            data.Installments = await InstallmentsAsync(from, to);

        if (datasets.Contains(ReportDataset.Customers))
            data.Customers = await CustomersAsync(from, to);

        return data;
    }

    // ── Period handling ─────────────────────────────────────────────────────

    /// <summary>
    /// Η εφαρμογή αποθηκεύει UTC. Το άνω όριο γίνεται αποκλειστικό στην αρχή της
    /// επόμενης ημέρας, ώστε να μη χάνονται οι εγγραφές της τελευταίας ημέρας.
    /// </summary>
    private (DateTime from, DateTime to) NormalizePeriod(ReportRequestDto request)
    {
        var from = DateTime.SpecifyKind(request.DateFrom.Date, DateTimeKind.Utc);
        var to   = DateTime.SpecifyKind(request.DateTo.Date, DateTimeKind.Utc).AddDays(1);

        if (to <= from)
            throw new BadRequestException("Η ημερομηνία λήξης πρέπει να είναι μετά την έναρξη.");

        if (to > from.AddMonths(_limits.MaxPeriodMonths))
            throw new BadRequestException(
                $"Η μέγιστη περίοδος εξαγωγής είναι {_limits.MaxPeriodMonths} μήνες. Περιορίστε το χρονικό διάστημα.");

        return (from, to);
    }

    private static List<ReportDataset> Requested(ReportRequestDto request)
    {
        var datasets = request.Datasets.Distinct().ToList();

        if (datasets.Count == 0)
            throw new BadRequestException("Επιλέξτε τουλάχιστον ένα σύνολο δεδομένων προς εξαγωγή.");

        return datasets;
    }

    private string LimitMessage(List<ReportPreviewRowDto> rows, int total)
    {
        var over = rows.Where(r => r.ExceedsSheetLimit).Select(r => r.Label).ToList();

        if (over.Count > 0)
            return $"Τα φύλλα {string.Join(", ", over)} ξεπερνούν το όριο των " +
                   $"{_limits.MaxRowsPerSheet:N0} γραμμών. Περιορίστε την περίοδο ή τα επιλεγμένα δεδομένα.";

        return $"Η εξαγωγή περιλαμβάνει {total:N0} γραμμές, πάνω από το συνολικό όριο των " +
               $"{_limits.MaxRowsTotal:N0}. Περιορίστε την περίοδο ή τα επιλεγμένα δεδομένα.";
    }

    // ── Counting (φθηνό: μόνο COUNT, χωρίς φόρτωση δεδομένων) ───────────────

    private Task<int> CountAsync(ReportDataset ds, DateTime from, DateTime to, AssetPeriodMode mode) => ds switch
    {
        ReportDataset.Income       => PaymentsQuery(from, to, TransactionType.Income).CountAsync(),
        ReportDataset.Expenses     => PaymentsQuery(from, to, TransactionType.Expense).CountAsync(),
        ReportDataset.Contracts    => ContractsQuery(from, to).CountAsync(),
        ReportDataset.Assets       => AssetsQuery(from, to, mode).CountAsync(),
        ReportDataset.Installments => InstallmentsQuery(from, to).CountAsync(),
        ReportDataset.Customers    => CustomersQuery(from, to).CountAsync(),
        _                          => Task.FromResult(0)
    };

    // ── Queries ─────────────────────────────────────────────────────────────

    private IQueryable<Entities.Payment> PaymentsQuery(DateTime from, DateTime to, TransactionType type)
        => context.Payments.AsNoTracking()
            .Where(p => p.TransactionType == type && p.PaymentDate >= from && p.PaymentDate < to);

    private IQueryable<Entities.Contract> ContractsQuery(DateTime from, DateTime to)
        => context.Contracts.AsNoTracking()
            .ExcludeCancelled()
            // Συμβόλαιο «της περιόδου» = όσα ήταν ενεργά έστω και μέρος της.
            .Where(c => c.StartDate < to && c.EndDate >= from);

    private IQueryable<Entities.Asset> AssetsQuery(DateTime from, DateTime to, AssetPeriodMode mode)
    {
        var query = context.Assets.AsNoTracking();

        return mode == AssetPeriodMode.Registered
            ? query.Where(a => a.CreatedAt >= from && a.CreatedAt < to)
            // Ίδιο μοτίβο επικάλυψης με το GetCalendarAsync του AssetRepository.
            : query.Where(a => a.ContractAssets.Any(ca =>
                  ca.Contract.Status != RentalStatus.Cancelled &&
                  ca.StartDate < to && ca.EndDate > from));
    }

    private IQueryable<Entities.Installment> InstallmentsQuery(DateTime from, DateTime to)
        => context.Installments.AsNoTracking()
            .ExcludeCancelledContracts()
            .Where(i => i.DueDate >= from && i.DueDate < to);

    private IQueryable<Entities.Customer> CustomersQuery(DateTime from, DateTime to)
        => context.Customers.AsNoTracking()
            .Where(c => c.CreatedAt >= from && c.CreatedAt < to);

    // ── Projections (επίπεδες, χωρίς ένθετες λίστες) ────────────────────────

    private async Task<List<PaymentRowDto>> PaymentsAsync(DateTime from, DateTime to, TransactionType type)
        => await PaymentsQuery(from, to, type)
            .OrderBy(p => p.PaymentDate)
            .Select(p => new PaymentRowDto
            {
                PaymentDate       = p.PaymentDate,
                Amount            = p.Amount,
                PaymentMethod     = p.PaymentMethod == PaymentMethod.Cash ? "Μετρητά"
                                  : p.PaymentMethod == PaymentMethod.Card ? "Κάρτα"
                                  : "Τραπεζική",
                CustomerName      = p.Allocations
                                        .Select(a => a.Installment.Contract.Customer.Name)
                                        .FirstOrDefault(),
                ReferenceCode     = p.TenantReferenceCode,
                Description       = p.Description,
                Notes             = p.Notes,
                UnallocatedAmount = p.UnallocatedAmount
            })
            .ToListAsync();

    private async Task<List<ContractRowDto>> ContractsAsync(DateTime from, DateTime to)
        => await ContractsQuery(from, to)
            .OrderBy(c => c.StartDate)
            .Select(c => new ContractRowDto
            {
                ReferenceCode     = c.ReferenceCode,
                CustomerName      = c.Customer.Name,
                StartDate         = c.StartDate,
                EndDate           = c.EndDate,
                TotalAmount       = c.TotalAmount,
                PaidAmount        = c.Installments.Sum(i => (decimal?)i.AllocatedAmount) ?? 0m,
                OutstandingAmount = c.Installments
                                        .Where(i => i.Status != InstallmentStatus.Paid
                                                 && i.Status != InstallmentStatus.Cancelled)
                                        .Sum(i => (decimal?)(i.TotalAmount - i.AllocatedAmount)) ?? 0m,
                Status            = c.Status == RentalStatus.Pending   ? "Εκκρεμές"
                                  : c.Status == RentalStatus.Active    ? "Ενεργό"
                                  : c.Status == RentalStatus.Completed ? "Ολοκληρωμένο"
                                  : "Ακυρωμένο",
                CreatedAt         = c.CreatedAt
            })
            .ToListAsync();

    private async Task<List<AssetRowDto>> AssetsAsync(DateTime from, DateTime to, AssetPeriodMode mode)
        => await AssetsQuery(from, to, mode)
            .OrderBy(a => a.Name)
            .Select(a => new AssetRowDto
            {
                Id            = a.Id,
                Name          = a.Name,
                AssetTypeName = a.AssetType.Name,
                Cost          = a.Cost,
                Status        = a.Status == AssetStatus.Active           ? "Διαθέσιμο"
                              : a.Status == AssetStatus.UnderMaintenance ? "Συντήρηση"
                              : a.Status == AssetStatus.Damaged          ? "Κατεστραμμένο"
                              : "Αποσυρμένο",
                CreatedAt     = a.CreatedAt
            })
            .ToListAsync();

    /// <summary>
    /// Τα δυναμικά χαρακτηριστικά EAV σε μορφή μακρού πίνακα. Πλατύς πίνακας με
    /// μία στήλη ανά πεδίο θα ήταν αραιός και απρόβλεπτος όταν συνυπάρχουν
    /// πολλοί τύποι παγίων στην ίδια εξαγωγή.
    /// </summary>
    private async Task<List<AssetAttributeRowDto>> AssetAttributesAsync(List<Guid> assetIds)
    {
        if (assetIds.Count == 0) return [];

        return await context.AssetAttributeValues.AsNoTracking()
            .Where(v => assetIds.Contains(v.AssetId))
            .OrderBy(v => v.Asset.Name).ThenBy(v => v.AssetTypeField.DisplayOrder)
            .Select(v => new AssetAttributeRowDto
            {
                AssetName     = v.Asset.Name,
                AssetTypeName = v.Asset.AssetType.Name,
                Field         = v.AssetTypeField.Label,
                Value         = v.StringValue != null      ? v.StringValue
                              : v.DecimalValue != null     ? v.DecimalValue.ToString()!
                              : v.DateValue != null        ? v.DateValue.ToString()!
                              : v.BoolValue != null        ? (v.BoolValue.Value ? "Ναι" : "Όχι")
                              : ""
            })
            .ToListAsync();
    }

    private async Task<List<InstallmentRowDto>> InstallmentsAsync(DateTime from, DateTime to)
        => await InstallmentsQuery(from, to)
            .OrderBy(i => i.DueDate)
            .Select(i => new InstallmentRowDto
            {
                ContractReferenceCode = i.Contract.ReferenceCode,
                CustomerName          = i.Contract.Customer.Name,
                InstallmentNumber     = i.InstallmentNumber,
                DueDate               = i.DueDate,
                TotalAmount           = i.TotalAmount,
                AllocatedAmount       = i.AllocatedAmount,
                OutstandingAmount     = i.TotalAmount - i.AllocatedAmount,
                Status                = i.Status == InstallmentStatus.Pending       ? "Εκκρεμής"
                                      : i.Status == InstallmentStatus.PartiallyPaid ? "Μερικώς"
                                      : i.Status == InstallmentStatus.Paid          ? "Εξοφλημένη"
                                      : i.Status == InstallmentStatus.Overdue       ? "Ληξιπρόθεσμη"
                                      : "Ακυρωμένη"
            })
            .ToListAsync();

    private async Task<List<CustomerRowDto>> CustomersAsync(DateTime from, DateTime to)
        => await CustomersQuery(from, to)
            .OrderBy(c => c.Name)
            .Select(c => new CustomerRowDto
            {
                Name      = c.Name,
                Type      = c.Type == CustomerType.Person ? "Φυσικό πρόσωπο" : "Εταιρεία",
                Afm       = c.Afm,
                Dou       = c.Dou,
                Address   = c.Address,
                CreatedAt = c.CreatedAt
            })
            .ToListAsync();

    // ── KPI ─────────────────────────────────────────────────────────────────

    private async Task<PeriodKpiDto> BuildKpiAsync(DateTime from, DateTime to)
    {
        var now = DateTime.UtcNow;

        var income   = await PaymentsQuery(from, to, TransactionType.Income).SumAsync(p => (decimal?)p.Amount) ?? 0m;
        var expenses = await PaymentsQuery(from, to, TransactionType.Expense).SumAsync(p => (decimal?)p.Amount) ?? 0m;

        var contractsCreated = await context.Contracts.AsNoTracking().ExcludeCancelled()
            .CountAsync(c => c.CreatedAt >= from && c.CreatedAt < to);
        var contractsStarted = await context.Contracts.AsNoTracking().ExcludeCancelled()
            .CountAsync(c => c.StartDate >= from && c.StartDate < to);
        var contractsEnded = await context.Contracts.AsNoTracking().ExcludeCancelled()
            .CountAsync(c => c.EndDate >= from && c.EndDate < to);

        var assetsRegistered    = await context.Assets.AsNoTracking()
            .CountAsync(a => a.CreatedAt >= from && a.CreatedAt < to);
        var customersRegistered = await context.Customers.AsNoTracking()
            .CountAsync(c => c.CreatedAt >= from && c.CreatedAt < to);

        var dueQuery = InstallmentsQuery(from, to);
        var installmentsDue       = await dueQuery.CountAsync();
        var installmentsDueAmount = await dueQuery.SumAsync(i => (decimal?)i.TotalAmount) ?? 0m;

        // Εισπράχθηκε στην περίοδο = κατανομές πληρωμών με ημερομηνία πληρωμής εντός.
        var collected = await context.PaymentInstallments.AsNoTracking()
            .Where(a => a.Payment.PaymentDate >= from && a.Payment.PaymentDate < to
                     && a.Installment.Contract.Status != RentalStatus.Cancelled)
            .SumAsync(a => (decimal?)a.AllocatedAmount) ?? 0m;

        var outstandingQuery = context.Installments.AsNoTracking().Outstanding();
        var outstandingNow = await outstandingQuery
            .SumAsync(i => (decimal?)(i.TotalAmount - i.AllocatedAmount)) ?? 0m;

        var overdueQuery = outstandingQuery.Where(i => i.DueDate < now);
        var overdueCount  = await overdueQuery.CountAsync();
        var overdueAmount = await overdueQuery
            .SumAsync(i => (decimal?)(i.TotalAmount - i.AllocatedAmount)) ?? 0m;

        return new PeriodKpiDto
        {
            Income                = income,
            Expenses              = expenses,
            Net                   = income - expenses,
            ContractsCreated      = contractsCreated,
            ContractsStarted      = contractsStarted,
            ContractsEnded        = contractsEnded,
            AssetsRegistered      = assetsRegistered,
            CustomersRegistered   = customersRegistered,
            InstallmentsDue       = installmentsDue,
            InstallmentsDueAmount = installmentsDueAmount,
            CollectedInPeriod     = collected,
            OutstandingNow        = outstandingNow,
            OverdueCountNow       = overdueCount,
            OverdueAmountNow      = overdueAmount,
            TotalAssets           = await context.Assets.AsNoTracking().CountAsync(),
            ActiveContractsNow    = await context.Contracts.AsNoTracking()
                                        .CountAsync(c => c.Status == RentalStatus.Active)
        };
    }

    private static string Label(ReportDataset ds) => ds switch
    {
        ReportDataset.Kpi          => "Σύνοψη",
        ReportDataset.Income       => "Έσοδα",
        ReportDataset.Expenses     => "Έξοδα",
        ReportDataset.Contracts    => "Συμβόλαια",
        ReportDataset.Assets       => "Πάγια",
        ReportDataset.Installments => "Δόσεις",
        ReportDataset.Customers    => "Πελάτες",
        _                          => ds.ToString()
    };
}
