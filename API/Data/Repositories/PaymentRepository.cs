using API.Data.Contexts;
using API.DTOs.Payment;
using API.Entities;
using API.Helper;
using API.Interfaces;
using Microsoft.EntityFrameworkCore;
using static API.Entities.Enums;

namespace API.Data.Repositories;

public class PaymentRepository(AppDbContext context) : IPaymentRepository
{
    public async Task<PaginatedResult<ContractPaymentDto>> GetContractsAsync(
        string? search, RentalStatus? status, PagingParams pagingParams)
    {
        var threshold = DateTime.UtcNow.AddDays(30);

        var query = context.Contracts.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(c =>
                c.Customer.Name.ToLower().Contains(term) ||
                c.Customer.Afm.Contains(term) );
        }

        if (status.HasValue)
            query = query.Where(c => c.Status == status.Value);

        var projected = query
            .OrderByDescending(c => c.StartDate)
            .Select(c => new ContractPaymentDto
            {
                Id             = c.Id,
                CustomerName   = c.Customer.Name,
                StartDate      = c.StartDate,
                EndDate        = c.EndDate,
                TotalAmount    = c.TotalAmount,
                PaidAmount     = c.PaymentContracts
                    .Where(pc => pc.Payment.TransactionType == TransactionType.Income && !pc.Payment.IsDeleted)
                    .Sum(pc => (decimal?)pc.Payment.Amount) ?? 0m,
                OutstandingBalance = c.TotalAmount - (c.PaymentContracts
                    .Where(pc => pc.Payment.TransactionType == TransactionType.Income && !pc.Payment.IsDeleted)
                    .Sum(pc => (decimal?)pc.Payment.Amount) ?? 0m),
                Status         = c.Status,
                CanExtend      = (c.Status == RentalStatus.Active && c.EndDate <= threshold)
                              || c.Status == RentalStatus.Completed,
                AssetNames     = c.ContractAssets.Select(ca => ca.Asset.Name).ToList()
            });

        return await PaginationHelper.CreateAsync(projected, pagingParams.PageNumber, pagingParams.PageSize);
    }

    public async Task<PaginatedResult<PaymentListItemDto>> GetIncomeAsync(
        PagingParams pagingParams, Guid? contractId)
    {
        var query = context.Payments
            .AsNoTracking()
            .Where(p => p.TransactionType == TransactionType.Income);

        if (contractId.HasValue)
            query = query.Where(p => p.PaymentContracts.Any(pc => pc.ContractId == contractId.Value));

        var projected = query
            .OrderByDescending(p => p.PaymentDate)
            .Select(p => new PaymentListItemDto
            {
                Id              = p.Id,
                Amount          = p.Amount,
                PaymentDate     = p.PaymentDate,
                PaymentMethod   = p.PaymentMethod,
                TransactionType = p.TransactionType,
                Notes           = p.Notes,
                ContractIds     = p.PaymentContracts.Select(pc => pc.ContractId).ToList(),
                CustomerNames   = p.PaymentContracts.Select(pc => pc.Contract.Customer.Name).ToList(),
                CreatedAt       = p.CreatedAt
            });

        return await PaginationHelper.CreateAsync(projected, pagingParams.PageNumber, pagingParams.PageSize);
    }

    public async Task<PaginatedResult<PaymentListItemDto>> GetExpensesAsync(PagingParams pagingParams)
    {
        var projected = context.Payments
            .AsNoTracking()
            .Where(p => p.TransactionType == TransactionType.Expense)
            .OrderByDescending(p => p.PaymentDate)
            .Select(p => new PaymentListItemDto
            {
                Id                 = p.Id,
                Amount             = p.Amount,
                PaymentDate        = p.PaymentDate,
                PaymentMethod      = p.PaymentMethod,
                TransactionType    = p.TransactionType,
                Notes              = p.Notes,
                Description        = p.Description,
                AssetNames         = p.PaymentAssets.Select(pa => pa.Asset.Name).ToList(),
                AttachmentUrl      = context.FileAttachments
                    .Where(fa => fa.EntityType == "Payment" && fa.EntityId == p.Id)
                    .Select(fa => fa.FilePath)
                    .FirstOrDefault(),
                AttachmentFileName = context.FileAttachments
                    .Where(fa => fa.EntityType == "Payment" && fa.EntityId == p.Id)
                    .Select(fa => fa.FileName)
                    .FirstOrDefault(),
                CreatedAt          = p.CreatedAt
            });

        return await PaginationHelper.CreateAsync(projected, pagingParams.PageNumber, pagingParams.PageSize);
    }

    public async Task AddAsync(Payment payment)
        => await context.Payments.AddAsync(payment);

    public async Task<Payment?> GetEntityByIdAsync(Guid id)
        => await context.Payments
            .Include(p => p.PaymentAssets)
            .FirstOrDefaultAsync(p => p.Id == id);

    public void SoftDelete(Payment payment, string userId)
    {
        payment.IsDeleted  = true;
        payment.DeletedAt  = DateTime.UtcNow;
        payment.DeletedBy  = userId;
        context.Entry(payment).State = EntityState.Modified;
    }

    public async Task AddAttachmentAsync(FileAttachment attachment)
        => await context.FileAttachments.AddAsync(attachment);

    public Task<FileAttachment?> GetAttachmentAsync(Guid paymentId)
        => context.FileAttachments
            .FirstOrDefaultAsync(fa => fa.EntityType == "Payment" && fa.EntityId == paymentId);

    public void RemoveAttachment(FileAttachment attachment)
        => context.FileAttachments.Remove(attachment);
}