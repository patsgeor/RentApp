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
    ITenantProvider tenantProvider) : IInstallmentService
{
    public async Task GenerateInstallmentsAsync(Guid contractId, string userId)
    {
        var contract = await context.Contracts
            .Include(c => c.Invoices)
            .FirstOrDefaultAsync(c => c.Id == contractId)
            ?? throw new NotFoundException($"Contract '{contractId}' was not found.");

        if (contract.Invoices.Any())
            throw new BadRequestException("Installments have already been generated for this contract.");

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
                Id                 = i.Id,
                ContractId         = i.ContractId,
                CustomerName       = i.Contract.Customer.Name,
                InstallmentNumber  = i.InstallmentNumber,
                PeriodStart        = i.PeriodStart,
                PeriodEnd          = i.PeriodEnd,
                DueDate            = i.DueDate,
                Amount             = i.Amount,
                TaxAmount          = i.TaxAmount,
                TotalAmount        = i.TotalAmount,
                AllocatedAmount    = i.AllocatedAmount,
                Status             = i.Status,
                Notes              = i.Notes,
                Allocations        = i.Allocations.Select(a => new AllocationSummaryDto
                {
                    AllocationId    = a.Id,
                    PaymentId       = a.PaymentId,
                    PaymentDate     = a.Payment.PaymentDate,
                    AllocatedAmount = a.AllocatedAmount
                }).ToList()
            })
            .ToListAsync();
    }

    public async Task<PaginatedResult<InstallmentDto>> GetOverdueAsync(PagingParams pagingParams)
    {
        var query = context.Invoices
            .AsNoTracking()
            .Where(i => i.Status == InstallmentStatus.Overdue)
            .OrderBy(i => i.DueDate)
            .Select(i => new InstallmentDto
            {
                Id                = i.Id,
                ContractId        = i.ContractId,
                CustomerName      = i.Contract.Customer.Name,
                InstallmentNumber = i.InstallmentNumber,
                PeriodStart       = i.PeriodStart,
                PeriodEnd         = i.PeriodEnd,
                DueDate           = i.DueDate,
                Amount            = i.Amount,
                TaxAmount         = i.TaxAmount,
                TotalAmount       = i.TotalAmount,
                AllocatedAmount   = i.AllocatedAmount,
                Status            = i.Status,
                Notes             = i.Notes,
            });

        return await PaginationHelper.CreateAsync(query, pagingParams.PageNumber, pagingParams.PageSize);
    }

    public async Task RefreshOverdueStatusesAsync()
    {
        var now = DateTime.UtcNow;
        var overdueInstallments = await context.Invoices
            .Where(i => i.Status == InstallmentStatus.Pending && i.DueDate < now)
            .ToListAsync();

        foreach (var inv in overdueInstallments)
            inv.Status = InstallmentStatus.Overdue;

        if (overdueInstallments.Count > 0)
            await context.SaveChangesAsync();
    }

    public async Task<MatchResultDto> AutoMatchAsync(Guid paymentId, string userId)
    {
        var payment = await context.Payments
            .Include(p => p.PaymentContracts)
            .Include(p => p.Allocations)
            .FirstOrDefaultAsync(p => p.Id == paymentId)
            ?? throw new NotFoundException($"Payment '{paymentId}' was not found.");

        if (string.IsNullOrWhiteSpace(payment.TenantReferenceCode))
            return new MatchResultDto
            {
                Matched    = false,
                Unallocated = payment.UnallocatedAmount,
                Message    = "Payment has no TenantReferenceCode — cannot auto-match."
            };

        var contract = await context.Contracts
            .FirstOrDefaultAsync(c =>
                c.TenantId == payment.TenantId &&
                c.ReferenceCode == payment.TenantReferenceCode)
            ?? throw new NotFoundException(
                $"No contract found with ReferenceCode '{payment.TenantReferenceCode}'.");

        // Link payment to contract if not already linked
        if (!payment.PaymentContracts.Any(pc => pc.ContractId == contract.Id))
        {
            payment.PaymentContracts.Add(new PaymentContract
            {
                PaymentId  = payment.Id,
                ContractId = contract.Id
            });
            payment.MatchStatus = PaymentMatchStatus.AutoMatched;
        }

        // FIFO: allocate to oldest unpaid/partially-paid installments
        var unpaidInstallments = await context.Invoices
            .Where(i => i.ContractId == contract.Id &&
                        i.Status != InstallmentStatus.Paid &&
                        i.Status != InstallmentStatus.Cancelled)
            .OrderBy(i => i.DueDate)
            .ToListAsync();

        var remaining = payment.UnallocatedAmount;
        var allocations = new List<AllocationSummaryDto>();

        foreach (var inv in unpaidInstallments)
        {
            if (remaining <= 0) break;

            var outstanding = inv.TotalAmount - inv.AllocatedAmount;
            if (outstanding <= 0) continue;

            var toAllocate = Math.Min(remaining, outstanding);
            var allocation = new PaymentAllocation
            {
                TenantId        = payment.TenantId,
                PaymentId       = payment.Id,
                InvoiceId       = inv.Id,
                AllocatedAmount = toAllocate,
                CreatedBy       = userId
            };

            await context.PaymentAllocations.AddAsync(allocation);

            inv.AllocatedAmount += toAllocate;
            inv.Status = inv.AllocatedAmount >= inv.TotalAmount
                ? InstallmentStatus.Paid
                : InstallmentStatus.PartiallyPaid;

            remaining -= toAllocate;
            allocations.Add(new AllocationSummaryDto
            {
                AllocationId    = allocation.Id,
                PaymentId       = payment.Id,
                PaymentDate     = payment.PaymentDate,
                AllocatedAmount = toAllocate
            });
        }

        payment.UnallocatedAmount = remaining;
        await context.SaveChangesAsync();

        return new MatchResultDto
        {
            Matched              = true,
            ContractId           = contract.Id,
            ContractReferenceCode = contract.ReferenceCode,
            TotalAllocated       = payment.Amount - remaining,
            Unallocated          = remaining,
            Allocations          = allocations,
            Message              = $"Auto-matched to contract '{contract.ReferenceCode}'. Allocated {allocations.Count} installments."
        };
    }

    public async Task AllocateManuallyAsync(
        Guid paymentId, List<AllocationItemDto> items, string userId)
    {
        var payment = await context.Payments
            .Include(p => p.Allocations)
            .FirstOrDefaultAsync(p => p.Id == paymentId)
            ?? throw new NotFoundException($"Payment '{paymentId}' was not found.");

        var totalToAllocate = items.Sum(i => i.Amount);
        if (totalToAllocate > payment.UnallocatedAmount)
            throw new BadRequestException(
                $"Total allocation ({totalToAllocate:N2}) exceeds unallocated amount ({payment.UnallocatedAmount:N2}).");

        foreach (var item in items)
        {
            var invoice = await context.Invoices.FindAsync(item.InvoiceId)
                ?? throw new NotFoundException($"Installment '{item.InvoiceId}' was not found.");

            var outstanding = invoice.TotalAmount - invoice.AllocatedAmount;
            if (item.Amount > outstanding)
                throw new BadRequestException(
                    $"Amount {item.Amount:N2} exceeds outstanding balance {outstanding:N2} for installment {invoice.InstallmentNumber}.");

            var allocation = new PaymentAllocation
            {
                TenantId        = payment.TenantId,
                PaymentId       = payment.Id,
                InvoiceId       = item.InvoiceId,
                AllocatedAmount = item.Amount,
                Notes           = item.Notes,
                CreatedBy       = userId
            };

            await context.PaymentAllocations.AddAsync(allocation);

            invoice.AllocatedAmount += item.Amount;
            invoice.Status = invoice.AllocatedAmount >= invoice.TotalAmount
                ? InstallmentStatus.Paid
                : InstallmentStatus.PartiallyPaid;
        }

        payment.UnallocatedAmount -= totalToAllocate;
        if (payment.MatchStatus == PaymentMatchStatus.Unmatched)
            payment.MatchStatus = PaymentMatchStatus.ManuallyMatched;

        await context.SaveChangesAsync();
    }

    public async Task DeallocateAsync(Guid allocationId, string userId)
    {
        var allocation = await context.PaymentAllocations
            .Include(a => a.Payment)
            .Include(a => a.Invoice)
            .FirstOrDefaultAsync(a => a.Id == allocationId)
            ?? throw new NotFoundException($"Allocation '{allocationId}' was not found.");

        allocation.Invoice.AllocatedAmount -= allocation.AllocatedAmount;
        allocation.Invoice.Status = allocation.Invoice.AllocatedAmount <= 0
            ? InstallmentStatus.Pending
            : InstallmentStatus.PartiallyPaid;

        allocation.Payment.UnallocatedAmount += allocation.AllocatedAmount;

        allocation.IsDeleted  = true;
        allocation.DeletedAt  = DateTime.UtcNow;
        allocation.DeletedBy  = userId;

        await context.SaveChangesAsync();
    }

    public async Task CancelInstallmentAsync(Guid invoiceId, string userId)
    {
        var invoice = await context.Invoices.FindAsync(invoiceId)
            ?? throw new NotFoundException($"Installment '{invoiceId}' was not found.");

        if (invoice.AllocatedAmount > 0)
            throw new BadRequestException("Cannot cancel an installment that has been partially or fully paid.");

        invoice.Status    = InstallmentStatus.Cancelled;
        invoice.UpdatedAt = DateTime.UtcNow;
        invoice.UpdatedBy = userId;

        await context.SaveChangesAsync();
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static List<Invoice> BuildInstallments(Contract contract, string userId)
    {
        var periods = GetPeriods(contract.StartDate, contract.EndDate, contract.InstallmentFrequency);
        var n = periods.Count;
        if (n == 0) return [];

        var netTotal = contract.TotalAmount - contract.TaxAmount - contract.DiscountAmount;
        var taxTotal = contract.TaxAmount;
        var perNet   = Math.Round(netTotal / n, 2);
        var perTax   = Math.Round(taxTotal / n, 2);

        var installments = new List<Invoice>();

        for (int i = 0; i < n; i++)
        {
            var (start, end) = periods[i];
            var isLast = i == n - 1;

            // Last installment absorbs rounding difference
            var amount    = isLast ? netTotal - perNet * (n - 1) : perNet;
            var taxAmount = isLast ? taxTotal - perTax * (n - 1) : perTax;

            installments.Add(new Invoice
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

        return installments;
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
                _ => end
            };

            var periodEnd = next > end ? end : next;
            periods.Add((current, periodEnd));
            current = next;

            if (freq == InstallmentFrequency.OneTime) break;
        }

        return periods;
    }
}