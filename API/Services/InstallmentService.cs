using API.Data.Contexts;
using API.DTOs.Payment;
using API.Entities;
using API.Errors;
using API.Helper;
using API.Interfaces;
using Microsoft.EntityFrameworkCore;
using static API.Entities.Enums;

namespace API.Services;

public class InstallmentService(
    AppDbContext context,
    ITenantProvider tenantProvider,
    IEmailService emailService) : IInstallmentService
{
    public async Task GenerateInstallmentsAsync(Guid contractId, string userId)
    {
        var contract = await context.Contracts
            .Include(c => c.Installments)
            .FirstOrDefaultAsync(c => c.Id == contractId)
            ?? throw new NotFoundException($"Συμβόλαιο '{contractId}' δεν βρέθηκε.");

        // Δόσεις με έστω και μερική πληρωμή δεν αγγίζονται ποτέ — αντιπροσωπεύουν
        // ήδη εισπραγμένα χρήματα. Ξαναχτίζουμε μόνο τις ανεξόφλητες, κατανέμοντας
        // το ΥΠΟΛΟΙΠΟ ποσό (νέο σύνολο συμβολαίου μείον ό,τι έχει ήδη προγραμματιστεί
        // σε πληρωμένες δόσεις) στον χρόνο που απομένει. Αυτό επιτρέπει να μεγαλώσει
        // το συμβόλαιο (νέο πάγιο, παράταση) χωρίς να χαθεί το ιστορικό πληρωμών.
        var preserved = contract.Installments
            .Where(i => i.AllocatedAmount > 0)
            .OrderBy(i => i.InstallmentNumber)
            .ToList();
        var toRemove = contract.Installments.Where(i => i.AllocatedAmount == 0).ToList();

        var preservedTotal = preserved.Sum(i => i.TotalAmount);
        var remaining = contract.TotalAmount - preservedTotal;

        if (remaining < -0.01m)
            throw new BadRequestException(
                $"Το νέο συνολικό ποσό του συμβολαίου ({contract.TotalAmount:N2}€) είναι μικρότερο από το ήδη " +
                $"προγραμματισμένο/εξοφλημένο ποσό στις υπάρχουσες δόσεις ({preservedTotal:N2}€). Αυξήστε το ποσό ή αφαιρέστε λιγότερα πάγια.");

        context.Installments.RemoveRange(toRemove);

        var periodStart = preserved.Count > 0 ? preserved.Max(i => i.PeriodEnd) : contract.StartDate;
        if (periodStart < contract.StartDate) periodStart = contract.StartDate;

        var nextNumber = preserved.Count > 0 ? preserved.Max(i => i.InstallmentNumber) + 1 : 1;

        var installments = BuildRemainingInstallments(contract, periodStart, remaining, nextNumber, userId);
        await context.Installments.AddRangeAsync(installments);
        await context.SaveChangesAsync();
    }

    public async Task<List<InstallmentDto>> GetByContractAsync(Guid contractId)
    {
        // Καμία εγγραφή σε διαδρομή ανάγνωσης: το «ληξιπρόθεσμη» υπολογίζεται
        // δηλωτικά από την ημερομηνία, αντί να ενημερώνεται η στήλη σε κάθε GET.
        var now = DateTime.UtcNow;

        return await context.Installments
            .AsNoTracking()
            .Where(i => i.ContractId == contractId)
            .OrderBy(i => i.InstallmentNumber)
            .Select(i => new InstallmentDto
            {
                Id                   = i.Id,
                ContractId           = i.ContractId,
                ContractReferenceCode = i.Contract.ReferenceCode,
                CustomerName         = i.Contract.Customer.Name,
                InstallmentNumber    = i.InstallmentNumber,
                PeriodStart          = i.PeriodStart,
                PeriodEnd            = i.PeriodEnd,
                DueDate              = i.DueDate,
                Amount               = i.Amount,
                TaxAmount            = i.TaxAmount,
                TotalAmount          = i.TotalAmount,
                AllocatedAmount      = i.AllocatedAmount,
                Status               = i.Status == InstallmentStatus.Pending && i.DueDate < now
                                           ? InstallmentStatus.Overdue
                                           : i.Status,
                Notes                = i.Notes,
                Allocations = i.Allocations.Select(a => new AllocationSummaryDto
                {
                    AllocationId    = a.Id,
                    PaymentId       = a.PaymentId,
                    PaymentDate     = a.Payment.PaymentDate,
                    AllocatedAmount = a.AllocatedAmount
                }).ToList()
            })
            .ToListAsync();
    }

    public async Task<PaginatedResult<InstallmentDto>> GetOverdueAsync(PagingParams p)
    {
        var now = DateTime.UtcNow;

        // «Ληξιπρόθεσμη» = απλήρωτη με ημερομηνία που πέρασε. Ο έλεγχος γίνεται
        // στην ημερομηνία και όχι στην αποθηκευμένη κατάσταση, ώστε το αποτέλεσμα
        // να είναι σωστό χωρίς να χρειάζεται προηγούμενη ενημέρωση της στήλης.
        var query = context.Installments
            .AsNoTracking()
            .Where(i => i.Status != InstallmentStatus.Paid
                     && i.Status != InstallmentStatus.Cancelled
                     && i.DueDate < now
                     && i.Contract.Status != RentalStatus.Cancelled)
            .OrderBy(i => i.DueDate)
            .Select(i => new InstallmentDto
            {
                Id                   = i.Id,
                ContractId           = i.ContractId,
                ContractReferenceCode = i.Contract.ReferenceCode,
                CustomerName         = i.Contract.Customer.Name,
                InstallmentNumber    = i.InstallmentNumber,
                PeriodStart          = i.PeriodStart,
                PeriodEnd            = i.PeriodEnd,
                DueDate              = i.DueDate,
                Amount               = i.Amount,
                TaxAmount            = i.TaxAmount,
                TotalAmount          = i.TotalAmount,
                AllocatedAmount      = i.AllocatedAmount,
                Status               = InstallmentStatus.Overdue,
                Notes                = i.Notes,
            });

        return await PaginationHelper.CreateAsync(query, p.PageNumber, p.PageSize);
    }

    public async Task<PaginatedResult<InstallmentDto>> GetDebtsAsync(DebtParams p)
    {
        var now = DateTime.UtcNow;

        var query = context.Installments
            .AsNoTracking()
            // Ακυρωμένα συμβόλαια δεν παράγουν οφειλές. Ο έλεγχος γίνεται και σε
            // επίπεδο συμβολαίου, ώστε η οθόνη να συμφωνεί με το KPI «Ανεξόφλητα»
            // του Πίνακα Ελέγχου, το οποίο οδηγεί εδώ.
            .Where(i => i.Status != InstallmentStatus.Paid &&
                        i.Status != InstallmentStatus.Cancelled &&
                        i.Contract.Status != RentalStatus.Cancelled);

        // Το «Ληξιπρόθεσμη» και το «Εκκρεμής» κρίνονται από την ημερομηνία, όχι από
        // την αποθηκευμένη στήλη — αλλιώς το φίλτρο θα εξαρτιόταν από το αν έχει
        // προηγηθεί ενημέρωση καταστάσεων.
        if (p.Status.HasValue)
        {
            query = p.Status.Value switch
            {
                InstallmentStatus.Overdue => query.Where(i => i.DueDate < now),
                InstallmentStatus.Pending => query.Where(i => i.Status == InstallmentStatus.Pending
                                                           && i.DueDate >= now),
                _                         => query.Where(i => i.Status == p.Status.Value)
            };
        }

        if (p.Month.HasValue)
        {
            var year = p.Year ?? DateTime.UtcNow.Year;
            var monthStart = new DateTime(year, p.Month.Value, 1, 0, 0, 0, DateTimeKind.Utc);
            var monthEnd   = monthStart.AddMonths(1);
            query = query.Where(i => i.DueDate >= monthStart && i.DueDate < monthEnd);
        }
        else if (p.Year.HasValue)
        {
            var yearStart = new DateTime(p.Year.Value, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var yearEnd   = yearStart.AddYears(1);
            query = query.Where(i => i.DueDate >= yearStart && i.DueDate < yearEnd);
        }

        if (p.CustomerId.HasValue)
            query = query.Where(i => i.Contract.CustomerId == p.CustomerId.Value);

        if (p.ContractId.HasValue)
            query = query.Where(i => i.ContractId == p.ContractId.Value);

        if (!string.IsNullOrWhiteSpace(p.Search))
        {
            var s = p.Search.Trim().ToLower();
            query = query.Where(i =>
                i.Contract.Customer.Name.ToLower().Contains(s) ||
                (i.Contract.ReferenceCode != null && i.Contract.ReferenceCode.ToLower().Contains(s)));
        }

        var projected = query
            .OrderBy(i => i.DueDate)
            .Select(i => new InstallmentDto
            {
                Id                   = i.Id,
                ContractId           = i.ContractId,
                ContractReferenceCode = i.Contract.ReferenceCode,
                CustomerName         = i.Contract.Customer.Name,
                InstallmentNumber    = i.InstallmentNumber,
                PeriodStart          = i.PeriodStart,
                PeriodEnd            = i.PeriodEnd,
                DueDate              = i.DueDate,
                Amount               = i.Amount,
                TaxAmount            = i.TaxAmount,
                TotalAmount          = i.TotalAmount,
                AllocatedAmount      = i.AllocatedAmount,
                Status               = i.Status == InstallmentStatus.Pending && i.DueDate < now
                                           ? InstallmentStatus.Overdue
                                           : i.Status,
                Notes                = i.Notes,
            });

        return await PaginationHelper.CreateAsync(projected, p.PageNumber, p.PageSize);
    }

    public async Task RefreshOverdueStatusesAsync()
    {
        var now = DateTime.UtcNow;
        var overdue = await context.Installments
            .Where(i => i.Status == InstallmentStatus.Pending
                     && i.DueDate < now
                     && i.Contract.Status != RentalStatus.Cancelled)
            .ToListAsync();

        foreach (var inv in overdue)
            inv.Status = InstallmentStatus.Overdue;

        if (overdue.Count > 0)
            await context.SaveChangesAsync();
    }

    public async Task<MatchResultDto> AutoMatchAsync(Guid paymentId, string userId)
    {
        var payment = await context.Payments
            .Include(p => p.Allocations)
            .FirstOrDefaultAsync(p => p.Id == paymentId)
            ?? throw new NotFoundException($"Πληρωμή '{paymentId}' δεν βρέθηκε.");

        if (string.IsNullOrWhiteSpace(payment.TenantReferenceCode))
            return new MatchResultDto
            {
                Matched     = false,
                Unallocated = payment.UnallocatedAmount,
                Message     = "Η πληρωμή δεν έχει TenantReferenceCode — δεν είναι δυνατή η αυτόματη αντιστοίχιση."
            };

        var contract = await context.Contracts
            .FirstOrDefaultAsync(c =>
                c.TenantId == payment.TenantId &&
                c.ReferenceCode == payment.TenantReferenceCode)
            ?? throw new NotFoundException($"Δεν βρέθηκε συμβόλαιο με ReferenceCode '{payment.TenantReferenceCode}'.");

        payment.MatchStatus = PaymentMatchStatus.AutoMatched;

        var unpaid = await context.Installments
            .Where(i => i.ContractId == contract.Id &&
                        i.Status != InstallmentStatus.Paid &&
                        i.Status != InstallmentStatus.Cancelled)
            .OrderBy(i => i.DueDate)
            .ToListAsync();

        var remaining  = payment.UnallocatedAmount;
        var allocations = new List<AllocationSummaryDto>();

        foreach (var inv in unpaid)
        {
            if (remaining <= 0) break;
            var outstanding = inv.TotalAmount - inv.AllocatedAmount;
            if (outstanding <= 0) continue;

            var toAllocate = Math.Min(remaining, outstanding);
            var alloc = new PaymentInstallment
            {
                TenantId        = payment.TenantId,
                PaymentId       = payment.Id,
                InstallmentId       = inv.Id,
                AllocatedAmount = toAllocate,
                CreatedBy       = userId
            };

            await context.PaymentInstallments.AddAsync(alloc);
            inv.AllocatedAmount += toAllocate;
            inv.Status = ComputeInstallmentStatus(inv);

            remaining -= toAllocate;
            allocations.Add(new AllocationSummaryDto
            {
                AllocationId    = alloc.Id,
                PaymentId       = payment.Id,
                PaymentDate     = payment.PaymentDate,
                AllocatedAmount = toAllocate
            });
        }

        payment.UnallocatedAmount = remaining;
        await context.SaveChangesAsync();

        return new MatchResultDto
        {
            Matched               = true,
            ContractId            = contract.Id,
            ContractReferenceCode = contract.ReferenceCode,
            TotalAllocated        = payment.Amount - remaining,
            Unallocated           = remaining,
            Allocations           = allocations,
            Message               = $"Αντιστοιχίστηκε σε '{contract.ReferenceCode}'. Κατανεμήθηκαν {allocations.Count} δόσεις."
        };
    }

    public async Task AllocateManuallyAsync(Guid paymentId, List<AllocationItemDto> items, string userId)
    {
        var payment = await context.Payments
            .Include(p => p.Allocations)
            .FirstOrDefaultAsync(p => p.Id == paymentId)
            ?? throw new NotFoundException($"Πληρωμή '{paymentId}' δεν βρέθηκε.");

        var total = items.Sum(i => i.Amount);
        if (total > payment.UnallocatedAmount + 0.01m)
            throw new BadRequestException(
                $"Το σύνολο κατανομής ({total:N2}) υπερβαίνει το διαθέσιμο ποσό ({payment.UnallocatedAmount:N2}).");

        foreach (var item in items)
        {
            var installment = await context.Installments.FindAsync(item.InstallmentId)
                ?? throw new NotFoundException($"Δόση '{item.InstallmentId}' δεν βρέθηκε.");

            var outstanding = installment.TotalAmount - installment.AllocatedAmount;
            if (item.Amount > outstanding + 0.01m)
                throw new BadRequestException(
                    $"Ποσό {item.Amount:N2} υπερβαίνει το εκκρεμές υπόλοιπο {outstanding:N2} για δόση #{installment.InstallmentNumber}.");

             var alloc = new PaymentInstallment
            {
                TenantId        = payment.TenantId,
                PaymentId       = payment.Id,
                InstallmentId   = item.InstallmentId,
                AllocatedAmount = item.Amount,
                Notes           = item.Notes,
                CreatedBy       = userId
            };

            await context.PaymentInstallments.AddAsync(alloc);
            installment.AllocatedAmount += item.Amount;
            installment.Status = ComputeInstallmentStatus(installment);
        }

        payment.UnallocatedAmount -= total;

        if (payment.MatchStatus == PaymentMatchStatus.Unmatched)
            payment.MatchStatus = PaymentMatchStatus.ManuallyMatched;

        await context.SaveChangesAsync();
    }

    public async Task DeallocateAsync(Guid allocationId, string userId)
    {
        var alloc = await context.PaymentInstallments
            .Include(a => a.Payment)
            .Include(a => a.Installment)
            .FirstOrDefaultAsync(a => a.Id == allocationId)
            ?? throw new NotFoundException($"Κατανομή '{allocationId}' δεν βρέθηκε.");

        alloc.Installment.AllocatedAmount -= alloc.AllocatedAmount;
        alloc.Installment.Status = ComputeInstallmentStatus(alloc.Installment);

        alloc.Payment.UnallocatedAmount += alloc.AllocatedAmount;
        alloc.IsDeleted = true;
        alloc.DeletedAt = DateTime.UtcNow;
        alloc.DeletedBy = userId;

        await context.SaveChangesAsync();
    }

    public async Task CancelInstallmentAsync(Guid installmentId, string userId)
    {
        var installment = await context.Installments.FindAsync(installmentId)
            ?? throw new NotFoundException($"Δόση '{installmentId}' δεν βρέθηκε.");

        if (installment.AllocatedAmount > 0)
            throw new BadRequestException("Δεν επιτρέπεται ακύρωση δόσης που έχει εξοφληθεί εν μέρει ή πλήρως.");

        installment.Status    = InstallmentStatus.Cancelled;
        installment.UpdatedAt = DateTime.UtcNow;
        installment.UpdatedBy = userId;

        await context.SaveChangesAsync();
    }

    public async Task NotifyByEmailAsync(Guid installmentId, string userId, string? senderEmail = null)
    {
        var installment = await context.Installments
            .Include(i => i.Contract)
                .ThenInclude(c => c.Customer)
                    .ThenInclude(cu => cu.Contacts)
            .FirstOrDefaultAsync(i => i.Id == installmentId)
            ?? throw new NotFoundException($"Δόση '{installmentId}' δεν βρέθηκε.");

        var email = installment.Contract.Customer.Contacts
            .FirstOrDefault(c => !string.IsNullOrWhiteSpace(c.Email))?.Email;

        if (string.IsNullOrWhiteSpace(email))
            throw new BadRequestException("Δεν υπάρχει email για τον πελάτη.");

        var outstanding = installment.TotalAmount - installment.AllocatedAmount;
        var subject = $"Υπενθύμιση Οφειλής — Δόση #{installment.InstallmentNumber}";
        var body = $"""
            Αγαπητέ/ή {installment.Contract.Customer.Name},<br><br>
            Σας υπενθυμίζουμε ότι η <strong>Δόση #{installment.InstallmentNumber}</strong>
            {(installment.Contract.ReferenceCode != null ? $"(Σύμβαση: {installment.Contract.ReferenceCode})" : "")}
            είναι εκκρεμής.<br><br>
            <table style="border-collapse:collapse;font-size:14px">
              <tr><td style="padding:4px 12px 4px 0"><b>Ημερομηνία λήξης:</b></td><td>{installment.DueDate:dd/MM/yyyy}</td></tr>
              <tr><td style="padding:4px 12px 4px 0"><b>Συνολικό ποσό:</b></td><td>{installment.TotalAmount:N2} €</td></tr>
              <tr><td style="padding:4px 12px 4px 0"><b>Εξοφλημένο:</b></td><td>{installment.AllocatedAmount:N2} €</td></tr>
              <tr><td style="padding:4px 12px 4px 0"><b>Εκκρεμές:</b></td><td><strong>{outstanding:N2} €</strong></td></tr>
            </table>
            <br>Παρακαλούμε επικοινωνήστε μαζί μας για οποιαδήποτε διευκρίνιση.
            """;

        await emailService.SendEmailAsync(email, subject, body, isHtml: true,
            cc: string.IsNullOrWhiteSpace(senderEmail) ? null : [senderEmail]);
    }

    // ── Private helpers ────────────────────────────────────────────────────

    private static InstallmentStatus ComputeInstallmentStatus(Installment inv)
    {
        if (inv.AllocatedAmount >= inv.TotalAmount) return InstallmentStatus.Paid;
        if (inv.AllocatedAmount > 0) return InstallmentStatus.PartiallyPaid;
        if (DateTime.UtcNow > inv.DueDate) return InstallmentStatus.Overdue;
        return InstallmentStatus.Pending;
    }

    /// <summary>
    /// Χτίζει τις δόσεις για το ΥΠΟΛΟΙΠΟ ποσό ενός συμβολαίου, ξεκινώντας από
    /// <paramref name="periodStart"/> μέχρι το τέλος του συμβολαίου. Όταν δεν υπάρχουν
    /// ήδη πληρωμένες δόσεις (periodStart == contract.StartDate, startNumber == 1,
    /// remainingTotal == contract.TotalAmount), παράγει το ίδιο αποτέλεσμα με πλήρη
    /// αρχική δημιουργία.
    /// </summary>
    private static List<Installment> BuildRemainingInstallments(
        Contract contract, DateTime periodStart, decimal remainingTotal, int startNumber, string userId)
    {
        if (remainingTotal <= 0.01m) return [];

        var periods = periodStart < contract.EndDate
            ? GetPeriods(periodStart, contract.EndDate, contract.InstallmentFrequency)
            : [];

        // Δεν απομένει χρόνος (μεγάλωσε μόνο το ποσό, όχι η διάρκεια) — μία δόση με όλο το υπόλοιπο.
        if (periods.Count == 0)
            periods = [(periodStart, contract.EndDate < periodStart ? periodStart : contract.EndDate)];

        var n = periods.Count;

        // Ίδια αναλογία φόρου/καθαρού με το συμβόλαιο συνολικά.
        var taxRatio = contract.TotalAmount > 0 ? contract.TaxAmount / contract.TotalAmount : 0m;
        var netTotal = Math.Round(remainingTotal * (1 - taxRatio), 2);
        var taxTotal = remainingTotal - netTotal;
        var perNet   = Math.Round(netTotal / n, 2);
        var perTax   = Math.Round(taxTotal / n, 2);

        var list = new List<Installment>();
        for (int i = 0; i < n; i++)
        {
            var (start, end) = periods[i];
            var isLast = i == n - 1;

            var amount    = isLast ? netTotal - perNet * (n - 1) : perNet;
            var taxAmount = isLast ? taxTotal - perTax * (n - 1) : perTax;

            list.Add(new Installment
            {
                TenantId          = contract.TenantId,
                ContractId        = contract.Id,
                InstallmentNumber = startNumber + i,
                PeriodStart       = start,
                PeriodEnd         = end,
                DueDate           = end,
                Amount            = amount,
                TaxAmount         = taxAmount,
                TotalAmount       = amount + taxAmount,
                Status            = InstallmentStatus.Pending,
                CreatedBy         = userId
            });
        }

        return list;
    }

    private static List<(DateTime Start, DateTime End)> GetPeriods(
        DateTime start, DateTime end, InstallmentFrequency freq)
    {
        var periods = new List<(DateTime, DateTime)>();
        var current = start;

        while (current < end)
        {
            var next = freq switch
            {
                InstallmentFrequency.Weekly    => current.AddDays(7),
                InstallmentFrequency.Monthly   => current.AddMonths(1),
                InstallmentFrequency.Quarterly => current.AddMonths(3),
                InstallmentFrequency.Yearly    => current.AddYears(1),
                InstallmentFrequency.OneTime   => end,
                _                              => end
            };

            var periodEnd = next > end ? end : next;
            periods.Add((current, periodEnd));
            current = next;

            if (freq == InstallmentFrequency.OneTime) break;
        }

        return periods;
    }

    public async Task UpdateScheduleAsync(Guid contractId, List<ScheduleInstallmentDto> schedule, string userId)
    {
        await using var tx = await context.Database.BeginTransactionAsync();
        try
        {
            var existing = await context.Installments
                .Where(i => i.ContractId == contractId)
                .ToListAsync();

            var contract = await context.Contracts.FindAsync(contractId)
                ?? throw new NotFoundException($"Συμβόλαιο '{contractId}' δεν βρέθηκε.");

            var incomingIds = schedule
                .Where(s => s.Id.HasValue)
                .Select(s => s.Id!.Value)
                .ToHashSet();

            // Διαγραφή δόσεων που αφαιρέθηκαν από τον χρήστη
            var removedIds = new HashSet<Guid>();
            foreach (var inv in existing.Where(e => !incomingIds.Contains(e.Id)))
            {
                if (inv.AllocatedAmount > 0)
                    throw new BadRequestException(
                        $"Δεν μπορεί να διαγραφεί η δόση #{inv.InstallmentNumber} — έχει καταγεγραμμένες πληρωμές.");
                context.Installments.Remove(inv);
                removedIds.Add(inv.Id);
            }

            var newlyAdded = new List<Installment>();

            // Ενημέρωση υπαρχουσών ή δημιουργία νέων
            foreach (var dto in schedule)
            {
                if (dto.Id.HasValue)
                {
                    var inv = existing.FirstOrDefault(e => e.Id == dto.Id.Value)
                        ?? throw new NotFoundException($"Δόση '{dto.Id}' δεν βρέθηκε.");

                    // Δόση με καταγεγραμμένη πληρωμή (έστω μερική) — τα ποσά/ημερομηνίες της
                    // δεν αλλάζουν ποτέ από εδώ, όποιες τιμές κι αν στείλει ο client.
                    // Επιτρέπουμε μόνο σημειώσεις.
                    if (inv.AllocatedAmount > 0)
                    {
                        inv.Notes     = dto.Notes;
                        inv.UpdatedBy = userId;
                        inv.UpdatedAt = DateTime.UtcNow;
                        continue;
                    }

                    inv.InstallmentNumber = dto.InstallmentNumber;
                    inv.PeriodStart       = DateTime.SpecifyKind(dto.PeriodStart, DateTimeKind.Utc);
                    inv.PeriodEnd         = DateTime.SpecifyKind(dto.PeriodEnd,   DateTimeKind.Utc);
                    inv.DueDate           = DateTime.SpecifyKind(dto.DueDate,     DateTimeKind.Utc);
                    inv.Amount            = dto.Amount;
                    inv.TaxAmount         = dto.TaxAmount;
                    inv.TotalAmount       = dto.Amount + dto.TaxAmount;
                    inv.Notes             = dto.Notes;
                    inv.UpdatedBy         = userId;
                    inv.UpdatedAt         = DateTime.UtcNow;
                }
                else
                {
                    var newInv = new Installment
                    {
                        TenantId          = contract.TenantId,
                        ContractId        = contractId,
                        InstallmentNumber = dto.InstallmentNumber,
                        PeriodStart       = DateTime.SpecifyKind(dto.PeriodStart, DateTimeKind.Utc),
                        PeriodEnd         = DateTime.SpecifyKind(dto.PeriodEnd,   DateTimeKind.Utc),
                        DueDate           = DateTime.SpecifyKind(dto.DueDate,     DateTimeKind.Utc),
                        Amount            = dto.Amount,
                        TaxAmount         = dto.TaxAmount,
                        TotalAmount       = dto.Amount + dto.TaxAmount,
                        Status            = InstallmentStatus.Pending,
                        Notes             = dto.Notes,
                        CreatedBy         = userId,
                    };
                    newlyAdded.Add(newInv);
                    await context.Installments.AddAsync(newInv);
                }
            }

            // Invariant: το σύνολο του συμβολαίου πρέπει πάντα να ισούται με το
            // άθροισμα των δόσεών του — ελέγχουμε πριν αποθηκεύσουμε οτιδήποτε.
            var finalTotal = existing.Where(e => !removedIds.Contains(e.Id)).Sum(e => e.TotalAmount)
                            + newlyAdded.Sum(e => e.TotalAmount);
            if (Math.Abs(finalTotal - contract.TotalAmount) > 0.01m)
                throw new BadRequestException(
                    $"Το άθροισμα των δόσεων ({finalTotal:N2}€) πρέπει να ισούται με το συνολικό ποσό " +
                    $"του συμβολαίου ({contract.TotalAmount:N2}€). Διαφορά: {(finalTotal - contract.TotalAmount):N2}€.");

            await context.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch { await tx.RollbackAsync(); throw; }
    }

    /// <summary>
    /// Συγκεντρωτικά οφειλών. Υπολογίζονται εξ ολοκλήρου στη βάση με ένα ερώτημα:
    /// η προηγούμενη υλοποίηση φόρτωνε ΟΛΕΣ τις ανεξόφλητες δόσεις στη μνήμη και
    /// άθροιζε τοπικά — κόστος που μεγάλωνε γραμμικά με τα δεδομένα του πελάτη.
    /// Το «ληξιπρόθεσμο» κρίνεται από την ημερομηνία, χωρίς εγγραφή στη βάση.
    /// </summary>
    public async Task<DebtStatsDto> GetStatsAsync(int? month, int? year)
    {
        var now    = DateTime.UtcNow;
        var m      = month ?? now.Month;
        var y      = year  ?? now.Year;
        var mStart = new DateTime(y, m, 1, 0, 0, 0, DateTimeKind.Utc);
        var mEnd   = mStart.AddMonths(1);

        var stats = await context.Installments
            .AsNoTracking()
            .Where(i => i.Status != InstallmentStatus.Paid &&
                        i.Status != InstallmentStatus.Cancelled &&
                        i.Contract.Status != RentalStatus.Cancelled)
            .GroupBy(_ => 1)
            .Select(g => new DebtStatsDto
            {
                ExpectedThisMonth  = g.Where(i => i.DueDate >= mStart && i.DueDate < mEnd)
                                      .Sum(i => (decimal?)(i.TotalAmount - i.AllocatedAmount)) ?? 0m,
                TotalOutstanding   = g.Sum(i => (decimal?)(i.TotalAmount - i.AllocatedAmount)) ?? 0m,
                OverdueCount       = g.Count(i => i.DueDate < now),
                OverdueAmount      = g.Where(i => i.DueDate < now)
                                      .Sum(i => (decimal?)(i.TotalAmount - i.AllocatedAmount)) ?? 0m,
                PendingCount       = g.Count(i => i.Status == InstallmentStatus.Pending && i.DueDate >= now),
                PartiallyPaidCount = g.Count(i => i.Status == InstallmentStatus.PartiallyPaid),
            })
            .FirstOrDefaultAsync();

        // Χωρίς καμία ανεξόφλητη δόση το GroupBy δεν επιστρέφει γραμμή.
        return stats ?? new DebtStatsDto();
    }
}