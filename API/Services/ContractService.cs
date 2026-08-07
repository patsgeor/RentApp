using API.Data.Contexts;
using API.DTOs.Contract;
using API.Entities;
using API.Errors;
using API.Helper;
using API.Interfaces;
using Microsoft.EntityFrameworkCore;
using static API.Entities.Enums;

namespace API.Services;

public class ContractService(
    IUnitOfWork uow,
    ITenantProvider tenantProvider,
    AppDbContext context,
    IEmailService emailService,
    IInstallmentService installmentService) : IContractService
{
    // Η ενημέρωση ληγμένων συμβολαίων έφευγε από εδώ: έγραφε στη βάση σε κάθε
    // GET της λίστας. Εκτελείται πλέον περιοδικά από το StatusRefreshService.
    public Task<PaginatedResult<ContractListItemDto>> GetAllAsync(ContractParams p)
        => uow.ContractRepository.GetAllAsync(p);

    public Task<ContractDetailDto?> GetByIdAsync(Guid id)
        => uow.ContractRepository.GetByIdAsync(id);

    public Task<List<AvailableAssetDto>> GetAvailableAssetsAsync(DateTime start, DateTime end, Guid? excludeContractId = null)
        => uow.ContractRepository.GetAvailableAssetsAsync(start, end, excludeContractId);

    public async Task<ContractDetailDto> CreateAsync(ContractCreateDto dto, string memberId)
    {
        if (dto.EndDate <= dto.StartDate)
            throw new BadRequestException("Η ημερομηνία λήξης πρέπει να είναι μετά την έναρξη.");

        if (!dto.Assets.Any())
            throw new BadRequestException("Το συμβόλαιο πρέπει να έχει τουλάχιστον ένα πάγιο.");

        var assetLines = dto.Assets.Select(a => new ContractAsset
        {
            AssetId          = a.AssetId,
            StartDate        =  DateTime.SpecifyKind(a.StartDate, DateTimeKind.Utc),
            EndDate          = DateTime.SpecifyKind(a.EndDate, DateTimeKind.Utc),
            UnitCost         = a.UnitCost,
            RateUnit         = a.RateUnit,
            CalculatedAmount = a.CalculatedAmount,
            Notes            = a.Notes
        }).ToList();

        await EnsureNoAssetOverlapAsync(assetLines, excludeContractId: null);

        var totalAmount = assetLines.Sum(a => a.CalculatedAmount) - dto.DiscountAmount + dto.TaxAmount;

        var start = DateTime.SpecifyKind(dto.StartDate, DateTimeKind.Utc);
        var end   = DateTime.SpecifyKind(dto.EndDate,   DateTimeKind.Utc);

        var contract = new Contract
        {
            TenantId             = tenantProvider.TenantId,
            CustomerId           = dto.CustomerId,
            StartDate            = start,
            EndDate              = end,
            SignedDate           = dto.SignedDate.HasValue
                ? DateTime.SpecifyKind(dto.SignedDate.Value, DateTimeKind.Utc)
                : null,
            ReferenceCode        = string.IsNullOrWhiteSpace(dto.ReferenceCode) ? null : dto.ReferenceCode.Trim(),
            TaxAmount            = dto.TaxAmount,
            DiscountAmount       = dto.DiscountAmount,
            TotalAmount          = totalAmount,
            InstallmentFrequency = dto.InstallmentFrequency,
            Notes                = dto.Notes,
            Terms                = dto.Terms,
            Status               = RentalStatus.Pending,
            ContractAssets       = assetLines,
            CreatedBy            = memberId
        };

        // Auto-create single "full amount" installment — user can split later via Generate
        contract.Installments.Add(new Installment
        {
            TenantId          = tenantProvider.TenantId,
            InstallmentNumber = 1,
            PeriodStart       = start,
            PeriodEnd         = end,
            DueDate           = end,
            Amount            = totalAmount - dto.TaxAmount,
            TaxAmount         = dto.TaxAmount,
            TotalAmount       = totalAmount,
            Status            = InstallmentStatus.Pending,
            CreatedBy         = memberId
        });


        uow.ContractRepository.Add(contract);
        await uow.Complete();

        return await uow.ContractRepository.GetByIdAsync(contract.Id)
            ?? throw new Exception("Αδυναμία ανάκτησης συμβολαίου μετά τη δημιουργία.");
    }

   public async Task<ContractDetailDto> UpdateAsync(Guid id, ContractUpdateDto dto, string memberId)
{
    if (dto.EndDate <= dto.StartDate)
        throw new BadRequestException("Η ημερομηνία λήξης πρέπει να είναι μετά την έναρξη.");

    var contract = await uow.ContractRepository.FindAsync(id)
        ?? throw new NotFoundException($"Συμβόλαιο {id} δεν βρέθηκε.");

    if (contract.xmin != dto.RowVersion)
        throw new ConflictException("Το συμβόλαιο τροποποιήθηκε από άλλο χρήστη. Ανανεώστε και δοκιμάστε ξανά.");

    var oldTotal = contract.TotalAmount;
    var wasCancelled = contract.Status == RentalStatus.Cancelled;
    decimal totalAmount;

    await using var tx = await uow.BeginTransactionAsync();
    try
    {
        await uow.ContractRepository.DeleteAllAssetsAsync(id);

        var newAssets = dto.Assets.Select(a => new ContractAsset
        {
            ContractId       = id,
            AssetId          = a.AssetId,
            StartDate        = DateTime.SpecifyKind(a.StartDate, DateTimeKind.Utc),
            EndDate          = DateTime.SpecifyKind(a.EndDate,   DateTimeKind.Utc),
            UnitCost         = a.UnitCost,
            RateUnit         = a.RateUnit,
            CalculatedAmount = a.CalculatedAmount,
            Notes            = a.Notes
        }).ToList();

        await EnsureNoAssetOverlapAsync(newAssets, excludeContractId: id);

        uow.ContractRepository.AddAssets(newAssets);

        totalAmount = dto.Assets.Sum(a => a.CalculatedAmount) - dto.DiscountAmount + dto.TaxAmount;

        contract.CustomerId           = dto.CustomerId;
        contract.StartDate            = DateTime.SpecifyKind(dto.StartDate, DateTimeKind.Utc);
        contract.EndDate              = DateTime.SpecifyKind(dto.EndDate,   DateTimeKind.Utc);
        contract.SignedDate           = dto.SignedDate.HasValue
                                            ? DateTime.SpecifyKind(dto.SignedDate.Value, DateTimeKind.Utc)
                                            : null;
        contract.ReferenceCode        = string.IsNullOrWhiteSpace(dto.ReferenceCode) ? null : dto.ReferenceCode.Trim();
        contract.TaxAmount            = dto.TaxAmount;
        contract.DiscountAmount       = dto.DiscountAmount;
        contract.TotalAmount          = totalAmount;
        contract.InstallmentFrequency = dto.InstallmentFrequency;
        contract.Notes                = dto.Notes;
        contract.Terms                = dto.Terms;
        contract.Status               = dto.Status;
        contract.UpdatedBy            = memberId;
        contract.UpdatedAt            = DateTime.UtcNow;

        await uow.Complete();
        await tx.CommitAsync();
    }
    catch
    {
        await tx.RollbackAsync();
        throw;
    }

    // Invariant: το σύνολο του συμβολαίου πρέπει πάντα να ισούται με το άθροισμα
    // των δόσεών του. Όταν η αλλαγή (π.χ. νέο πάγιο) μεταβάλλει το σύνολο, ξανα-
    // κατανέμουμε αυτόματα — οι ήδη πληρωμένες δόσεις παραμένουν πάντα άθικτες
    // (βλ. InstallmentService.GenerateInstallmentsAsync).
    if (Math.Abs(totalAmount - oldTotal) > 0.01m)
        await installmentService.GenerateInstallmentsAsync(id, memberId);

    // Μετάβαση σε Ακυρωμένο: οι εκκρεμείς δόσεις ακυρώνονται ώστε το συμβόλαιο να
    // πάψει να συμμετέχει σε οφειλές, ληξιπρόθεσμα και προβλέψεις εισπράξεων.
    if (dto.Status == RentalStatus.Cancelled && !wasCancelled)
        await CancelPendingInstallmentsAsync(id, memberId);

    // Αντίστροφη μετάβαση: η ακύρωση είναι πλήρως αναστρέψιμη.
    else if (wasCancelled && dto.Status != RentalStatus.Cancelled)
        await RestoreCancelledInstallmentsAsync(id, memberId);

    return await uow.ContractRepository.GetByIdAsync(id)
        ?? throw new Exception("Αδυναμία ανάκτησης συμβολαίου μετά την ενημέρωση.");
}

    /// <summary>
    /// Ακυρώνει τις εκκρεμείς δόσεις ενός συμβολαίου που μόλις τέθηκε σε κατάσταση
    /// Cancelled. Δόσεις με έστω και μερική είσπραξη ΔΕΝ αγγίζονται: αντιπροσωπεύουν
    /// χρήματα που πράγματι εισπράχθηκαν και πρέπει να παραμείνουν στα οικονομικά
    /// στοιχεία. Έτσι το ακυρωμένο συμβόλαιο παύει να παράγει οφειλές και προβλέψεις,
    /// χωρίς να εξαφανίζεται το ιστορικό του.
    /// </summary>
    private async Task CancelPendingInstallmentsAsync(Guid contractId, string memberId)
    {
        var pending = await context.Installments
            .Where(i => i.ContractId == contractId
                     && i.AllocatedAmount == 0
                     && i.Status != InstallmentStatus.Cancelled)
            .ToListAsync();

        if (pending.Count == 0) return;

        var now = DateTime.UtcNow;
        foreach (var inst in pending)
        {
            inst.Status    = InstallmentStatus.Cancelled;
            inst.UpdatedAt = now;
            inst.UpdatedBy = memberId;
        }

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Επαναφέρει σε εκκρεμότητα τις δόσεις που είχαν ακυρωθεί μαζί με το συμβόλαιο,
    /// όταν αυτό βγαίνει από την κατάσταση Cancelled. Χωρίς αυτό, ένα συμβόλαιο που
    /// ακυρώθηκε κατά λάθος και επαναφέρθηκε θα παρέμενε χωρίς καμία οφειλή.
    /// Αγγίζονται μόνο δόσεις χωρίς καμία είσπραξη· ο χαρακτηρισμός ληξιπρόθεσμων
    /// επανυπολογίζεται αυτόματα από το RefreshOverdueStatusesAsync.
    /// </summary>
    private async Task RestoreCancelledInstallmentsAsync(Guid contractId, string memberId)
    {
        var cancelled = await context.Installments
            .Where(i => i.ContractId == contractId
                     && i.Status == InstallmentStatus.Cancelled
                     && i.AllocatedAmount == 0)
            .ToListAsync();

        if (cancelled.Count == 0) return;

        var now = DateTime.UtcNow;
        foreach (var inst in cancelled)
        {
            inst.Status    = InstallmentStatus.Pending;
            inst.UpdatedAt = now;
            inst.UpdatedBy = memberId;
        }

        await context.SaveChangesAsync();
    }

    public async Task<ContractEmailResultDto> SendByEmailAsync(
        Guid id, ContractEmailDto dto, IEnumerable<EmailAttachment> attachments, string memberId, string? senderEmail = null)
    {
        var contract = await context.Contracts
            .Include(c => c.Customer).ThenInclude(cu => cu.Contacts)
            .Include(c => c.ContractAssets).ThenInclude(ca => ca.Asset)
            .FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new NotFoundException($"Συμβόλαιο {id} δεν βρέθηκε.");

        var to = !string.IsNullOrWhiteSpace(dto.To)
            ? dto.To.Trim()
            : contract.Customer.Contacts.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c.Email))?.Email;

        if (string.IsNullOrWhiteSpace(to))
            throw new BadRequestException("Δεν βρέθηκε email παραλήπτη. Προσθέστε επαφή με email στον πελάτη ή δώστε διεύθυνση.");

        var subject = string.IsNullOrWhiteSpace(dto.Subject)
            ? $"Συμβόλαιο Μίσθωσης{(contract.ReferenceCode != null ? $" — {contract.ReferenceCode}" : "")}"
            : dto.Subject.Trim();

        await emailService.SendEmailAsync(to, subject, BuildContractHtml(contract, dto.Message),
            isHtml: true,
            cc: string.IsNullOrWhiteSpace(senderEmail) ? null : [senderEmail],
            attachments: attachments);

        // Η αποστολή είναι η στιγμή που το συμβόλαιο «ενεργοποιείται» προς τον πελάτη
        var statusChanged = false;
        if (dto.ActivateContract && contract.Status == RentalStatus.Pending)
        {
            contract.Status    = RentalStatus.Active;
            contract.UpdatedBy = memberId;
            contract.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
            statusChanged = true;
        }

        return new ContractEmailResultDto
        {
            Sent          = true,
            SentTo        = to,
            StatusChanged = statusChanged,
            Message       = statusChanged
                ? $"Το συμβόλαιο στάλθηκε στο {to} και ενεργοποιήθηκε."
                : $"Το συμβόλαιο στάλθηκε στο {to}."
        };
    }

    public async Task RefreshCompletedStatusesAsync()
    {
        var now = DateTime.UtcNow;
        var expired = await context.Contracts
            .Where(c => c.Status == RentalStatus.Active && c.EndDate < now)
            .ToListAsync();

        foreach (var c in expired)
        {
            c.Status    = RentalStatus.Completed;
            c.UpdatedAt = now;
        }

        if (expired.Count > 0) await context.SaveChangesAsync();
    }

    private static string BuildContractHtml(Contract c, string? personalMessage)
    {
        var rateUnitLabel = (RateUnit r) => r switch
        {
            RateUnit.PerHour  => "/ώρα",
            RateUnit.PerDay   => "/ημέρα",
            RateUnit.PerMonth => "/μήνα",
            _                 => "εφάπαξ"
        };

        var rows = string.Join("", c.ContractAssets.Select(ca => $"""
            <tr>
              <td style="padding:8px;border-bottom:1px solid #eee">{ca.Asset.Name}</td>
              <td style="padding:8px;border-bottom:1px solid #eee">{ca.StartDate:dd/MM/yyyy HH:mm}</td>
              <td style="padding:8px;border-bottom:1px solid #eee">{ca.EndDate:dd/MM/yyyy HH:mm}</td>
              <td style="padding:8px;border-bottom:1px solid #eee;text-align:right">{ca.UnitCost:N2} € {rateUnitLabel(ca.RateUnit)}</td>
              <td style="padding:8px;border-bottom:1px solid #eee;text-align:right"><strong>{ca.CalculatedAmount:N2} €</strong></td>
            </tr>
            """));

        var subtotal = c.ContractAssets.Sum(ca => ca.CalculatedAmount);
        var intro = string.IsNullOrWhiteSpace(personalMessage)
            ? ""
            : $"""<p style="white-space:pre-wrap">{System.Net.WebUtility.HtmlEncode(personalMessage)}</p>""";

        return $"""
            <div style="font-family:Arial,Helvetica,sans-serif;font-size:14px;color:#222;max-width:720px">
              <h2 style="margin:0 0 4px">Συμβόλαιο Μίσθωσης</h2>
              {(c.ReferenceCode != null ? $"<p style='margin:0 0 16px;color:#666'>Κωδικός: <strong>{c.ReferenceCode}</strong></p>" : "")}

              <p>Αγαπητέ/ή <strong>{c.Customer.Name}</strong>,</p>
              {intro}
              <p>Ακολουθούν τα στοιχεία του συμβολαίου σας:</p>

              <table style="border-collapse:collapse;margin:12px 0">
                <tr><td style="padding:4px 16px 4px 0"><b>Έναρξη:</b></td><td>{c.StartDate:dd/MM/yyyy HH:mm}</td></tr>
                <tr><td style="padding:4px 16px 4px 0"><b>Λήξη:</b></td><td>{c.EndDate:dd/MM/yyyy HH:mm}</td></tr>
                {(c.SignedDate.HasValue ? $"<tr><td style='padding:4px 16px 4px 0'><b>Ημ. Υπογραφής:</b></td><td>{c.SignedDate:dd/MM/yyyy}</td></tr>" : "")}
                <tr><td style="padding:4px 16px 4px 0"><b>ΑΦΜ:</b></td><td>{c.Customer.Afm}</td></tr>
              </table>

              <h3 style="margin:20px 0 8px">Πάγια</h3>
              <table style="border-collapse:collapse;width:100%;font-size:13px">
                <thead>
                  <tr style="background:#f5f5f5">
                    <th style="padding:8px;text-align:left">Πάγιο</th>
                    <th style="padding:8px;text-align:left">Από</th>
                    <th style="padding:8px;text-align:left">Έως</th>
                    <th style="padding:8px;text-align:right">Χρέωση</th>
                    <th style="padding:8px;text-align:right">Ποσό</th>
                  </tr>
                </thead>
                <tbody>{rows}</tbody>
              </table>

              <table style="border-collapse:collapse;margin:16px 0 0;margin-left:auto">
                <tr><td style="padding:4px 16px 4px 0">Υποσύνολο:</td><td style="text-align:right">{subtotal:N2} €</td></tr>
                <tr><td style="padding:4px 16px 4px 0">Έκπτωση:</td><td style="text-align:right">- {c.DiscountAmount:N2} €</td></tr>
                <tr><td style="padding:4px 16px 4px 0">Φόρος:</td><td style="text-align:right">{c.TaxAmount:N2} €</td></tr>
                <tr style="border-top:2px solid #333">
                  <td style="padding:8px 16px 4px 0"><b>Σύνολο:</b></td>
                  <td style="text-align:right;padding-top:8px"><b>{c.TotalAmount:N2} €</b></td>
                </tr>
              </table>

              {(string.IsNullOrWhiteSpace(c.Terms) ? "" : $"<h3 style='margin:24px 0 8px'>Όροι</h3><p style='white-space:pre-wrap;color:#444'>{System.Net.WebUtility.HtmlEncode(c.Terms)}</p>")}
            </div>
            """;
    }

    // Ξανατσεκάρει επικάλυψη ημερομηνιών ανά πάγιο πριν το save — το read-only
    // GetAvailableAssetsAsync (dropdown) δεν εμποδίζει δύο ταυτόχρονα requests
    // από το να διπλοκλείσουν το ίδιο πάγιο.
    private async Task EnsureNoAssetOverlapAsync(List<ContractAsset> assetLines, Guid? excludeContractId)
    {
        var assetIds = assetLines.Select(a => a.AssetId).ToList();

        var busy = await context.ContractAssets
            .AsNoTracking()
            .Where(ca =>
                assetIds.Contains(ca.AssetId) &&
                (ca.Contract.Status == RentalStatus.Active || ca.Contract.Status == RentalStatus.Pending) &&
                (excludeContractId == null || ca.ContractId != excludeContractId))
            .Select(ca => new { ca.AssetId, ca.StartDate, ca.EndDate, ca.Asset.Name })
            .ToListAsync();

        foreach (var line in assetLines)
        {
            var conflict = busy.FirstOrDefault(b =>
                b.AssetId == line.AssetId &&
                b.StartDate < line.EndDate && b.EndDate > line.StartDate);

            if (conflict != null)
                throw new BadRequestException(
                    $"Το πάγιο '{conflict.Name}' είναι ήδη δεσμευμένο για την περίοδο " +
                    $"{conflict.StartDate:dd/MM/yyyy}–{conflict.EndDate:dd/MM/yyyy}.");
        }
    }
}
