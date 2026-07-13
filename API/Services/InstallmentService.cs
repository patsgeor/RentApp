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
            .Include(c => c.Invoices)
            .FirstOrDefaultAsync(c => c.Id == contractId)
            ?? throw new NotFoundException($"Συμβόλαιο '{contractId}' δεν βρέθηκε.");

        if (contract.Invoices.Any())
            throw new BadRequestException("Οι δόσεις για αυτό το συμβόλαιο έχουν ήδη δημιουργηθεί.");

        var installments = BuildInstallments(contract, userId);
        await context.Invoices.AddRangeAsync(installments);
        await context.SaveChangesAsync();
    }

    public async Task<List<InstallmentDto>> GetByContractAsync(Guid contractId)
    {
        return await context.Invoices
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
                Status               = i.Status,
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
        var query = context.Invoices
            .AsNoTracking()
            .Where(i => i.Status == InstallmentStatus.Overdue)
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
                Status               = i.Status,
                Notes                = i.Notes,
            });

        return await PaginationHelper.CreateAsync(query, p.PageNumber, p.PageSize);
    }

    public async Task<PaginatedResult<InstallmentDto>> GetDebtsAsync(DebtParams p)
    {
        var query = context.Invoices
            .AsNoTracking()
            .Where(i => i.Status != InstallmentStatus.Paid &&
                        i.Status != InstallmentStatus.Cancelled);

        if (p.Status.HasValue)
            query = query.Where(i => i.Status == p.Status.Value);

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
                Status               = i.Status,
                Notes                = i.Notes,
            });

        return await PaginationHelper.CreateAsync(projected, p.PageNumber, p.PageSize);
    }

    public async Task RefreshOverdueStatusesAsync()
    {
        var now = DateTime.UtcNow;
        var overdue = await context.Invoices
            .Where(i => i.Status == InstallmentStatus.Pending && i.DueDate < now)
            .ToListAsync();

        foreach (var inv in overdue)
            inv.Status = InstallmentStatus.Overdue;

        if (overdue.Count > 0)
            await context.SaveChangesAsync();
    }

    public async Task<MatchResultDto> AutoMatchAsync(Guid paymentId, string userId)
    {
        var payment = await context.Payments
            .Include(p => p.PaymentContracts)
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

        if (!payment.PaymentContracts.Any(pc => pc.ContractId == contract.Id))
        {
            payment.PaymentContracts.Add(new PaymentContract
            {
                PaymentId  = payment.Id,
                ContractId = contract.Id
            });
            payment.MatchStatus = PaymentMatchStatus.AutoMatched;
        }

        var unpaid = await context.Invoices
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
            var alloc = new PaymentAllocation
            {
                TenantId        = payment.TenantId,
                PaymentId       = payment.Id,
                InvoiceId       = inv.Id,
                AllocatedAmount = toAllocate,
                CreatedBy       = userId
            };

            await context.PaymentAllocations.AddAsync(alloc);
            inv.AllocatedAmount += toAllocate;
            inv.Status = inv.AllocatedAmount >= inv.TotalAmount
                ? InstallmentStatus.Paid : InstallmentStatus.PartiallyPaid;

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
        if (total > payment.UnallocatedAmount)
            throw new BadRequestException(
                $"Το σύνολο κατανομής ({total:N2}) υπερβαίνει το διαθέσιμο ποσό ({payment.UnallocatedAmount:N2}).");

        foreach (var item in items)
        {
            var invoice = await context.Invoices.FindAsync(item.InvoiceId)
                ?? throw new NotFoundException($"Δόση '{item.InvoiceId}' δεν βρέθηκε.");

            var outstanding = invoice.TotalAmount - invoice.AllocatedAmount;
            if (item.Amount > outstanding)
                throw new BadRequestException(
                    $"Ποσό {item.Amount:N2} υπερβαίνει το εκκρεμές υπόλοιπο {outstanding:N2} για δόση #{invoice.InstallmentNumber}.");

            var alloc = new PaymentAllocation
            {
                TenantId        = payment.TenantId,
                PaymentId       = payment.Id,
                InvoiceId       = item.InvoiceId,
                AllocatedAmount = item.Amount,
                Notes           = item.Notes,
                CreatedBy       = userId
            };

            await context.PaymentAllocations.AddAsync(alloc);
            invoice.AllocatedAmount += item.Amount;
            invoice.Status = invoice.AllocatedAmount >= invoice.TotalAmount
                ? InstallmentStatus.Paid : InstallmentStatus.PartiallyPaid;
        }

        payment.UnallocatedAmount -= total;
        if (payment.MatchStatus == PaymentMatchStatus.Unmatched)
            payment.MatchStatus = PaymentMatchStatus.ManuallyMatched;

        await context.SaveChangesAsync();
    }

    public async Task DeallocateAsync(Guid allocationId, string userId)
    {
        var alloc = await context.PaymentAllocations
            .Include(a => a.Payment)
            .Include(a => a.Invoice)
            .FirstOrDefaultAsync(a => a.Id == allocationId)
            ?? throw new NotFoundException($"Κατανομή '{allocationId}' δεν βρέθηκε.");

        alloc.Invoice.AllocatedAmount -= alloc.AllocatedAmount;
        alloc.Invoice.Status = alloc.Invoice.AllocatedAmount <= 0
            ? InstallmentStatus.Pending : InstallmentStatus.PartiallyPaid;

        alloc.Payment.UnallocatedAmount += alloc.AllocatedAmount;
        alloc.IsDeleted = true;
        alloc.DeletedAt = DateTime.UtcNow;
        alloc.DeletedBy = userId;

        await context.SaveChangesAsync();
    }

    public async Task CancelInstallmentAsync(Guid invoiceId, string userId)
    {
        var invoice = await context.Invoices.FindAsync(invoiceId)
            ?? throw new NotFoundException($"Δόση '{invoiceId}' δεν βρέθηκε.");

        if (invoice.AllocatedAmount > 0)
            throw new BadRequestException("Δεν επιτρέπεται ακύρωση δόσης που έχει εξοφληθεί εν μέρει ή πλήρως.");

        invoice.Status    = InstallmentStatus.Cancelled;
        invoice.UpdatedAt = DateTime.UtcNow;
        invoice.UpdatedBy = userId;

        await context.SaveChangesAsync();
    }

    public async Task NotifyByEmailAsync(Guid invoiceId, string userId)
    {
        var invoice = await context.Invoices
            .Include(i => i.Contract)
                .ThenInclude(c => c.Customer)
                    .ThenInclude(cu => cu.Contacts)
            .FirstOrDefaultAsync(i => i.Id == invoiceId)
            ?? throw new NotFoundException($"Δόση '{invoiceId}' δεν βρέθηκε.");

        var email = invoice.Contract.Customer.Contacts
            .FirstOrDefault(c => !string.IsNullOrWhiteSpace(c.Email))?.Email;

        if (string.IsNullOrWhiteSpace(email))
            throw new BadRequestException("Δεν υπάρχει email για τον πελάτη.");

        var outstanding = invoice.TotalAmount - invoice.AllocatedAmount;
        var subject = $"Υπενθύμιση Οφειλής — Δόση #{invoice.InstallmentNumber}";
        var body = $"""
            Αγαπητέ/ή {invoice.Contract.Customer.Name},<br><br>
            Σας υπενθυμίζουμε ότι η <strong>Δόση #{invoice.InstallmentNumber}</strong>
            {(invoice.Contract.ReferenceCode != null ? $"(Σύμβαση: {invoice.Contract.ReferenceCode})" : "")}
            είναι εκκρεμής.<br><br>
            <table style="border-collapse:collapse;font-size:14px">
              <tr><td style="padding:4px 12px 4px 0"><b>Ημερομηνία λήξης:</b></td><td>{invoice.DueDate:dd/MM/yyyy}</td></tr>
              <tr><td style="padding:4px 12px 4px 0"><b>Συνολικό ποσό:</b></td><td>{invoice.TotalAmount:N2} €</td></tr>
              <tr><td style="padding:4px 12px 4px 0"><b>Εξοφλημένο:</b></td><td>{invoice.AllocatedAmount:N2} €</td></tr>
              <tr><td style="padding:4px 12px 4px 0"><b>Εκκρεμές:</b></td><td><strong>{outstanding:N2} €</strong></td></tr>
            </table>
            <br>Παρακαλούμε επικοινωνήστε μαζί μας για οποιαδήποτε διευκρίνιση.
            """;

        await emailService.SendEmailAsync(email, subject, body, isHtml: true);
    }

    // ── Private helpers ────────────────────────────────────────────────────

    private static List<Invoice> BuildInstallments(Contract contract, string userId)
    {
        var periods = GetPeriods(contract.StartDate, contract.EndDate, contract.InstallmentFrequency);
        var n = periods.Count;
        if (n == 0) return [];

        var netTotal = contract.TotalAmount - contract.TaxAmount;
        var taxTotal = contract.TaxAmount;
        var perNet   = Math.Round(netTotal / n, 2);
        var perTax   = Math.Round(taxTotal / n, 2);

        var list = new List<Invoice>();
        for (int i = 0; i < n; i++)
        {
            var (start, end) = periods[i];
            var isLast = i == n - 1;

            var amount    = isLast ? netTotal - perNet * (n - 1) : perNet;
            var taxAmount = isLast ? taxTotal - perTax * (n - 1) : perTax;

            list.Add(new Invoice
            {
                TenantId          = contract.TenantId,
                ContractId        = contract.Id,
                InstallmentNumber = i + 1,
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
            var existing = await context.Invoices
                .Where(i => i.ContractId == contractId)
                .ToListAsync();

            var contract = await context.Contracts.FindAsync(contractId)
                ?? throw new NotFoundException($"Συμβόλαιο '{contractId}' δεν βρέθηκε.");

            var incomingIds = schedule
                .Where(s => s.Id.HasValue)
                .Select(s => s.Id!.Value)
                .ToHashSet();

            // Διαγραφή δόσεων που αφαιρέθηκαν από τον χρήστη
            foreach (var inv in existing.Where(e => !incomingIds.Contains(e.Id)))
            {
                if (inv.AllocatedAmount > 0)
                    throw new BadRequestException(
                        $"Δεν μπορεί να διαγραφεί η δόση #{inv.InstallmentNumber} — έχει καταγεγραμμένες πληρωμές.");
                context.Invoices.Remove(inv);
            }

            // Ενημέρωση υπαρχουσών ή δημιουργία νέων
            foreach (var dto in schedule)
            {
                if (dto.Id.HasValue)
                {
                    var inv = existing.FirstOrDefault(e => e.Id == dto.Id.Value)
                        ?? throw new NotFoundException($"Δόση '{dto.Id}' δεν βρέθηκε.");

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
                    await context.Invoices.AddAsync(new Invoice
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
                    });
                }
            }

            await context.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch { await tx.RollbackAsync(); throw; }
    }

    public async Task<DebtStatsDto> GetStatsAsync(int? month, int? year)
    {
        var now    = DateTime.UtcNow;
        var m      = month ?? now.Month;
        var y      = year  ?? now.Year;
        var mStart = new DateTime(y, m, 1, 0, 0, 0, DateTimeKind.Utc);
        var mEnd   = mStart.AddMonths(1);

        var rows = await context.Invoices
            .AsNoTracking()
            .Where(i => i.Status != InstallmentStatus.Paid &&
                        i.Status != InstallmentStatus.Cancelled)
            .Select(i => new { i.Status, i.TotalAmount, i.AllocatedAmount, i.DueDate })
            .ToListAsync();

        return new DebtStatsDto
        {
            ExpectedThisMonth  = rows.Where(r => r.DueDate >= mStart && r.DueDate < mEnd)
                                    .Sum(r => r.TotalAmount - r.AllocatedAmount),
            TotalOutstanding   = rows.Sum(r  => r.TotalAmount - r.AllocatedAmount),
            OverdueCount       = rows.Count(r => r.Status == InstallmentStatus.Overdue),
            OverdueAmount      = rows.Where(r => r.Status == InstallmentStatus.Overdue)
                                    .Sum(r   => r.TotalAmount - r.AllocatedAmount),
            PendingCount       = rows.Count(r => r.Status == InstallmentStatus.Pending),
            PartiallyPaidCount = rows.Count(r => r.Status == InstallmentStatus.PartiallyPaid),
        };
    }
}