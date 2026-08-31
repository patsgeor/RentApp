using API.Data.Contexts;
using API.Entities;
using API.Errors;
using API.Interfaces;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using static API.Entities.Enums;

namespace API.Services.DataExport;

/// <summary>
/// Πλήρες Excel backup όλων των δεδομένων ενός tenant — ένα φύλλο ανά οντότητα.
/// Σε αντίθεση με το ReportService, δεν υπάρχει preview/επιλογή συνόλων: το backup
/// είναι πάντα «τα πάντα», οπότε δεν χρειάζεται ξεχωριστό επίπεδο DTO/builder —
/// το ClosedXML χτίζεται απευθείας εδώ.
///
/// Τα περισσότερα ερωτήματα βασίζονται στο global query filter του AppDbContext.
/// Τρεις οντότητες ΔΕΝ φιλτράρονται αυτόματα και χρειάζονται ρητό tenant check:
/// ContractAsset (δεν έχει καν TenantId), Photo (έχει TenantId αλλά δεν υλοποιεί
/// IMustHaveTenant), Member (δεν έχει TenantId — φτάνει μέσω του AppUser).
/// </summary>
public class BackupService(
    AppDbContext context,
    ITenantProvider tenantProvider,
    UserManager<AppUser> userManager,
    IFileStorage storage) : IBackupService
{
    // Ανώτατο συνολικό πλήθος γραμμών πριν αρνηθούμε το backup — προστασία από
    // OOM, καθαρά ClosedXML κρατά όλο το workbook στη μνήμη. Σε αντίθεση με τις
    // αναφορές, εδώ δεν κόβουμε δεδομένα: είτε βγαίνει ολόκληρο, είτε αρνούμαστε
    // ρητά — ποτέ σιωπηλή περικοπή σε κάτι που υπόσχεται να είναι "πλήρες".
    private const int MaxTotalRows = 500_000;

    public async Task<BackupResult> BuildAsync(CancellationToken ct)
    {
        var tenantId = tenantProvider.TenantId;

        var tenant = await context.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, ct)
            ?? throw new NotFoundException("Ο tenant δεν βρέθηκε.");

        var customers     = await CustomersAsync(ct);
        var assets        = await AssetsAsync(ct);
        var assetTypes    = await AssetTypesAsync(ct);
        var attributes    = await AssetAttributesAsync(ct);
        var contracts     = await ContractsAsync(ct);
        var contractLines = await ContractAssetsAsync(tenantId, ct);
        var installments  = await InstallmentsAsync(ct);
        var payments      = await PaymentsAsync(ct);
        var attachments   = await AttachmentsAsync(ct);
        var photos        = await PhotosAsync(tenantId, ct);
        var members       = await MembersAsync(tenantId, ct);

        var totalRows = customers.Count + assets.Count + assetTypes.Count + attributes.Count
            + contracts.Count + contractLines.Count + installments.Count + payments.Count
            + attachments.Count + photos.Count + members.Count;

        if (totalRows > MaxTotalRows)
            throw new BadRequestException(
                $"Το backup περιλαμβάνει {totalRows:N0} γραμμές, πάνω από το όριο ασφαλείας των " +
                $"{MaxTotalRows:N0}. Επικοινωνήστε μαζί μας για εξαγωγή σε τμήματα.");

        using var wb = new XLWorkbook();

        AddTenantSheet(wb, tenant);
        AddSheet(wb, "Πελάτες", ["Επωνυμία", "Τύπος", "ΑΦΜ", "ΔΟΥ", "Διεύθυνση", "Εκπρόσωπος", "Ημ. Καταχώρησης"],
            customers, c => [c.Name, c.Type == CustomerType.Person ? "Φυσικό πρόσωπο" : "Εταιρεία",
                c.Afm, c.Dou ?? "", c.Address ?? "", c.Representative ?? "", Date(c.CreatedAt)]);

        AddSheet(wb, "Κατηγορίες Παγίων", ["Όνομα", "Περιγραφή"],
            assetTypes, t => [t.Name, t.Description ?? ""]);

        AddSheet(wb, "Πάγια", ["Όνομα", "Κατηγορία", "Κόστος", "Μονάδα Χρέωσης", "Κατάσταση", "Σημειώσεις", "Ημ. Καταχώρησης"],
            assets, a => [a.Name, a.AssetTypeName, Money(a.Cost), a.RateUnit, a.Status, a.Notes ?? "", Date(a.CreatedAt)]);

        AddSheet(wb, "Χαρακτηριστικά Παγίων", ["Πάγιο", "Κατηγορία", "Πεδίο", "Τιμή"],
            attributes, a => [a.AssetName, a.AssetTypeName, a.Field, a.Value]);

        AddSheet(wb, "Συμβόλαια", ["Κωδικός", "Πελάτης", "Έναρξη", "Λήξη", "Σύνολο", "ΦΠΑ", "Έκπτωση", "Κατάσταση", "Υπογραφή", "Όροι", "Σημειώσεις", "Ημ. Καταχώρησης"],
            contracts, c => [c.ReferenceCode ?? "", c.CustomerName, Date(c.StartDate), Date(c.EndDate),
                Money(c.TotalAmount), Money(c.TaxAmount), Money(c.DiscountAmount), c.Status,
                c.SignedDate.HasValue ? Date(c.SignedDate.Value) : "", c.Terms ?? "", c.Notes ?? "", Date(c.CreatedAt)]);

        AddSheet(wb, "Συμβόλαια-Πάγια", ["Συμβόλαιο", "Πάγιο", "Έναρξη", "Λήξη", "Κόστος Μονάδας", "Ποσό"],
            contractLines, l => [l.ContractReferenceCode ?? "", l.AssetName, Date(l.StartDate), Date(l.EndDate),
                Money(l.UnitCost), Money(l.CalculatedAmount)]);

        AddSheet(wb, "Δόσεις", ["Συμβόλαιο", "Πελάτης", "Α/Α", "Λήξη", "Ποσό", "ΦΠΑ", "Σύνολο", "Εισπραχθέν", "Κατάσταση"],
            installments, i => [i.ContractReferenceCode ?? "", i.CustomerName, i.InstallmentNumber, Date(i.DueDate),
                Money(i.Amount), Money(i.TaxAmount), Money(i.TotalAmount), Money(i.AllocatedAmount), i.Status]);

        AddSheet(wb, "Πληρωμές", ["Ημερομηνία", "Ποσό", "Τρόπος", "Τύπος", "Παραστατικό", "Περιγραφή", "Σημειώσεις", "Αδιάθετο"],
            payments, p => [Date(p.PaymentDate), Money(p.Amount), p.PaymentMethod, p.TransactionType,
                p.TenantReferenceCode ?? "", p.Description ?? "", p.Notes ?? "", Money(p.UnallocatedAmount)]);

        AddSheet(wb, "Δικαιολογητικά", ["Τύπος Οντότητας", "Αρχείο", "Μέγεθος", "Ημ. Ανεβάσματος", "Σύνδεσμος (λήγει σε 30 λεπτά)"],
            attachments, a => [a.EntityType, a.FileName, $"{a.SizeBytes / 1024.0:N0} KB", Date(a.CreatedAt), a.Url ?? ""]);

        AddSheet(wb, "Φωτογραφίες Παγίων", ["Πάγιο", "Κύρια", "Σύνδεσμος (λήγει σε 30 λεπτά)"],
            photos, p => [p.AssetName, p.IsMain ? "Ναι" : "Όχι", p.Url ?? ""]);

        AddSheet(wb, "Μέλη", ["Όνομα", "Email", "Ρόλοι", "Ενεργός", "Εγγραφή", "Τελευταία Δραστηριότητα"],
            members, m => [m.DisplayName, m.Email ?? "", string.Join(", ", m.Roles), m.IsActive ? "Ναι" : "Όχι",
                Date(m.Created), Date(m.LastActive)]);

        var stream = new MemoryStream();
        wb.SaveAs(stream);
        stream.Position = 0;

        return new BackupResult(stream, tenant.Name);
    }

    // ── Queries ─────────────────────────────────────────────────────────────

    private async Task<List<CustomerRow>> CustomersAsync(CancellationToken ct) =>
        await context.Customers.AsNoTracking().OrderBy(c => c.Name)
            .Select(c => new CustomerRow(c.Name, c.Type, c.Afm, c.Dou, c.Address, c.Representative, c.CreatedAt))
            .ToListAsync(ct);

    private async Task<List<AssetRow>> AssetsAsync(CancellationToken ct) =>
        await context.Assets.AsNoTracking().OrderBy(a => a.Name)
            .Select(a => new AssetRow(a.Name, a.AssetType.Name, a.Cost, a.RateUnit.ToString(), a.Status.ToString(), a.Notes, a.CreatedAt))
            .ToListAsync(ct);

    private async Task<List<AssetTypeRow>> AssetTypesAsync(CancellationToken ct) =>
        await context.AssetTypes.AsNoTracking().OrderBy(t => t.Name)
            .Select(t => new AssetTypeRow(t.Name, t.Description))
            .ToListAsync(ct);

    private async Task<List<AttributeRow>> AssetAttributesAsync(CancellationToken ct) =>
        await context.AssetAttributeValues.AsNoTracking()
            .OrderBy(v => v.Asset.Name).ThenBy(v => v.AssetTypeField.DisplayOrder)
            .Select(v => new AttributeRow(
                v.Asset.Name, v.Asset.AssetType.Name, v.AssetTypeField.Label,
                v.StringValue != null  ? v.StringValue
                : v.DecimalValue != null ? v.DecimalValue.ToString()!
                : v.DateValue != null    ? v.DateValue.ToString()!
                : v.BoolValue != null    ? (v.BoolValue.Value ? "Ναι" : "Όχι")
                : ""))
            .ToListAsync(ct);

    private async Task<List<ContractRow>> ContractsAsync(CancellationToken ct) =>
        await context.Contracts.AsNoTracking().OrderBy(c => c.StartDate)
            .Select(c => new ContractRow(c.ReferenceCode, c.Customer.Name, c.StartDate, c.EndDate,
                c.TotalAmount, c.TaxAmount, c.DiscountAmount, c.Status.ToString(), c.SignedDate, c.Terms, c.Notes, c.CreatedAt))
            .ToListAsync(ct);

    // ContractAsset δεν έχει TenantId — φιλτράρεται ρητά μέσω του Contract.
    private async Task<List<ContractAssetRow>> ContractAssetsAsync(Guid tenantId, CancellationToken ct) =>
        await context.ContractAssets.AsNoTracking()
            .Where(ca => ca.Contract.TenantId == tenantId)
            .OrderBy(ca => ca.Contract.ReferenceCode)
            .Select(ca => new ContractAssetRow(ca.Contract.ReferenceCode, ca.Asset.Name, ca.StartDate, ca.EndDate, ca.UnitCost, ca.CalculatedAmount))
            .ToListAsync(ct);

    private async Task<List<InstallmentRow>> InstallmentsAsync(CancellationToken ct) =>
        await context.Installments.AsNoTracking().OrderBy(i => i.DueDate)
            .Select(i => new InstallmentRow(i.Contract.ReferenceCode, i.Contract.Customer.Name, i.InstallmentNumber,
                i.DueDate, i.Amount, i.TaxAmount, i.TotalAmount, i.AllocatedAmount, i.Status.ToString()))
            .ToListAsync(ct);

    private async Task<List<PaymentRow>> PaymentsAsync(CancellationToken ct) =>
        await context.Payments.AsNoTracking().OrderBy(p => p.PaymentDate)
            .Select(p => new PaymentRow(p.PaymentDate, p.Amount, p.PaymentMethod.ToString(), p.TransactionType.ToString(),
                p.TenantReferenceCode, p.Description, p.Notes, p.UnallocatedAmount))
            .ToListAsync(ct);

    private async Task<List<AttachmentRow>> AttachmentsAsync(CancellationToken ct)
    {
        var rows = await context.FileAttachments.AsNoTracking().OrderBy(a => a.CreatedAt)
            .Select(a => new { a.EntityType, a.FileName, a.FilePath, a.SizeBytes, a.CreatedAt })
            .ToListAsync(ct);

        return rows.Select(a => new AttachmentRow(a.EntityType, a.FileName, a.SizeBytes, a.CreatedAt, storage.ResolveUrl(a.FilePath))).ToList();
    }

    // Photo έχει TenantId αλλά δεν υλοποιεί IMustHaveTenant — δεν φιλτράρεται
    // αυτόματα, χρειάζεται ρητό έλεγχο (ίδιο μοτίβο με TenantController).
    private async Task<List<PhotoRow>> PhotosAsync(Guid tenantId, CancellationToken ct)
    {
        var rows = await context.Photos.AsNoTracking()
            .Where(p => p.TenantId == tenantId)
            .OrderBy(p => p.Asset.Name)
            .Select(p => new { AssetName = p.Asset.Name, p.IsMain, p.Url })
            .ToListAsync(ct);

        return rows.Select(p => new PhotoRow(p.AssetName, p.IsMain, storage.ResolveUrl(p.Url))).ToList();
    }

    // Member δεν έχει TenantId — φτάνει μέσω του AppUser (που ΕΧΕΙ το filter).
    private async Task<List<MemberRow>> MembersAsync(Guid tenantId, CancellationToken ct)
    {
        var users = await context.Users.AsNoTracking()
            .Where(u => u.TenantId == tenantId)
            .Include(u => u.Member)
            .OrderBy(u => u.DisplayName)
            .ToListAsync(ct);

        var result = new List<MemberRow>();
        foreach (var u in users)
        {
            var roles = await userManager.GetRolesAsync(u);
            result.Add(new MemberRow(u.DisplayName, u.Email, [.. roles], u.IsActive,
                u.Member?.Created ?? default, u.Member?.LastActive ?? default));
        }
        return result;
    }

    // ── Sheet helpers (ίδιο στυλ με ExcelWorkbookBuilder) ────────────────────

    private static void AddTenantSheet(XLWorkbook wb, Tenant tenant)
    {
        var ws = wb.AddWorksheet("Εταιρεία");
        var pairs = new (string Label, string Value)[]
        {
            ("Επωνυμία", tenant.Name),
            ("ΑΦΜ", tenant.VatNumber ?? ""),
            ("Στοιχεία Επικοινωνίας", tenant.ContactInfo ?? ""),
            ("Πλάνο", tenant.PlanType.ToString()),
            ("Κατάσταση Συνδρομής", tenant.SubscriptionStatus.ToString()),
            ("Λήξη Πλάνου", tenant.PlanExpiresAt.HasValue ? Date(tenant.PlanExpiresAt.Value) : ""),
            ("Ημερομηνία Εξαγωγής", Date(DateTime.UtcNow)),
        };
        for (var i = 0; i < pairs.Length; i++)
        {
            ws.Cell(i + 1, 1).Value = pairs[i].Label;
            ws.Cell(i + 1, 1).Style.Font.Bold = true;
            ws.Cell(i + 1, 2).Value = pairs[i].Value;
        }
        ws.Column(1).Width = 24;
        ws.Column(2).Width = 40;
    }

    private static void AddSheet<T>(XLWorkbook wb, string name, string[] headers, List<T> rows, Func<T, object[]> project)
    {
        var ws = wb.AddWorksheet(name);

        for (var i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#DCE6F1");
        }
        ws.SheetView.FreezeRows(1);

        var r = 2;
        foreach (var row in rows)
        {
            var values = project(row);
            for (var c = 0; c < values.Length; c++)
                ws.Cell(r, c + 1).Value = values[c]?.ToString() ?? "";
            r++;
        }

        if (rows.Count == 0)
            ws.Cell(2, 1).Value = "Δεν υπάρχουν δεδομένα.";

        ws.Columns().AdjustToContents();
    }

    private static string Money(decimal v) => v.ToString("#,##0.00") + " €";
    private static string Date(DateTime d) => d.ToString("dd/MM/yyyy");

    // ── Row shapes (επίπεδες προβολές, καμία ένθετη λίστα) ──────────────────

    private record CustomerRow(string Name, CustomerType Type, string Afm, string? Dou, string? Address, string? Representative, DateTime CreatedAt);
    private record AssetRow(string Name, string AssetTypeName, decimal Cost, string RateUnit, string Status, string? Notes, DateTime CreatedAt);
    private record AssetTypeRow(string Name, string? Description);
    private record AttributeRow(string AssetName, string AssetTypeName, string Field, string Value);
    private record ContractRow(string? ReferenceCode, string CustomerName, DateTime StartDate, DateTime EndDate,
        decimal TotalAmount, decimal TaxAmount, decimal DiscountAmount, string Status, DateTime? SignedDate, string? Terms, string? Notes, DateTime CreatedAt);
    private record ContractAssetRow(string? ContractReferenceCode, string AssetName, DateTime StartDate, DateTime EndDate, decimal UnitCost, decimal CalculatedAmount);
    private record InstallmentRow(string? ContractReferenceCode, string CustomerName, int InstallmentNumber, DateTime DueDate,
        decimal Amount, decimal TaxAmount, decimal TotalAmount, decimal AllocatedAmount, string Status);
    private record PaymentRow(DateTime PaymentDate, decimal Amount, string PaymentMethod, string TransactionType,
        string? TenantReferenceCode, string? Description, string? Notes, decimal UnallocatedAmount);
    private record AttachmentRow(string EntityType, string FileName, long SizeBytes, DateTime CreatedAt, string? Url);
    private record PhotoRow(string AssetName, bool IsMain, string? Url);
    private record MemberRow(string DisplayName, string? Email, string[] Roles, bool IsActive, DateTime Created, DateTime LastActive);
}
