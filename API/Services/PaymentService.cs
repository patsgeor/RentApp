using API.DTOs.Payment;
using API.Entities;
using API.Errors;
using API.Helper;
using API.Interfaces;
using Microsoft.AspNetCore.Http;
using static API.Entities.Enums;

namespace API.Services;

public class PaymentService(
    IUnitOfWork uow,
    ITenantProvider tenantProvider,
    IPhotoService photoService) : IPaymentService
{
    public Task<PaginatedResult<ContractPaymentDto>> GetContractsAsync(
        string? search, RentalStatus? status, PagingParams pagingParams)
        => uow.PaymentRepository.GetContractsAsync(search, status, pagingParams);

    public Task<PaginatedResult<PaymentListItemDto>> GetIncomeAsync(
        PagingParams pagingParams, Guid? contractId)
        => uow.PaymentRepository.GetIncomeAsync(pagingParams, contractId);

    public Task<PaginatedResult<PaymentListItemDto>> GetExpensesAsync(PagingParams pagingParams)
        => uow.PaymentRepository.GetExpensesAsync(pagingParams);

    public async Task<PaymentListItemDto> RecordIncomeAsync(IncomeCreateDto dto, string userId)
    {
        var payment = new Payment
        {
            TenantId        = tenantProvider.TenantId,
            Amount          = dto.Amount,
            PaymentDate     = dto.PaymentDate,
            PaymentMethod   = dto.PaymentMethod,
            Notes           = dto.Notes,
            TransactionType = TransactionType.Income,
            CreatedBy       = userId
        };

        payment.PaymentContracts.Add(new PaymentContract
        {
            ContractId = dto.ContractId
        });

        await uow.PaymentRepository.AddAsync(payment);
        await uow.Complete();

        return new PaymentListItemDto
        {
            Id              = payment.Id,
            Amount          = payment.Amount,
            PaymentDate     = DateTime.SpecifyKind(payment.PaymentDate, DateTimeKind.Utc),
            PaymentMethod   = payment.PaymentMethod,
            TransactionType = payment.TransactionType,
            Notes           = payment.Notes,
            ContractIds     = [dto.ContractId],
            CreatedAt       = payment.CreatedAt
        };
    }

    public async Task<PaymentListItemDto> RecordExpenseAsync(ExpenseCreateDto dto, IFormFile? file, string userId)
    {
        var payment = new Payment
        {
            TenantId        = tenantProvider.TenantId,
            Amount          = dto.Amount,
            PaymentDate     = DateTime.SpecifyKind(dto.PaymentDate, DateTimeKind.Utc),
            PaymentMethod   = dto.PaymentMethod,
            Notes           = dto.Notes,
            Description     = dto.Description,
            TransactionType = TransactionType.Expense,
            CreatedBy       = userId
        };

        if (dto.AssetIds is { Count: > 0 })
        {
            payment.PaymentAssets = dto.AssetIds.Select(assetId => new PaymentAsset
            {
                AssetId  = assetId,
                TenantId = tenantProvider.TenantId
            }).ToList();
        }

        await uow.PaymentRepository.AddAsync(payment);

        string? attachmentUrl      = null;
        string? attachmentFileName = null;

        if (file != null)
        {
            var uploaded = await photoService.AddPhotoAsync(file);
            await uow.PaymentRepository.AddAttachmentAsync(new FileAttachment
            {
                TenantId    = tenantProvider.TenantId,
                EntityType  = "Payment",
                EntityId    = payment.Id,
                FileName    = file.FileName,
                ContentType = file.ContentType,
                FilePath    = uploaded.Url,
                PublicId    = uploaded.PublicId,
                CreatedBy   = userId
            });
            attachmentUrl      = uploaded.Url;
            attachmentFileName = file.FileName;
        }

        await uow.Complete();

        return new PaymentListItemDto
        {
            Id                 = payment.Id,
            Amount             = payment.Amount,
            PaymentDate        = payment.PaymentDate,
            PaymentMethod      = payment.PaymentMethod,
            TransactionType    = payment.TransactionType,
            Notes              = payment.Notes,
            Description        = payment.Description,
            AttachmentUrl      = attachmentUrl,
            AttachmentFileName = attachmentFileName,
            CreatedAt          = payment.CreatedAt
        };
    }

    public async Task DeleteAsync(Guid id, string userId)
    {
        var payment = await uow.PaymentRepository.GetEntityByIdAsync(id)
            ?? throw new NotFoundException($"Payment '{id}' was not found.");

        var attachment = await uow.PaymentRepository.GetAttachmentAsync(id);
        if (attachment?.PublicId is not null)
            await photoService.DeletePhotoAsync(attachment.PublicId);

        if (attachment != null)
            uow.PaymentRepository.RemoveAttachment(attachment);

        uow.PaymentRepository.SoftDelete(payment, userId);
        await uow.Complete();
    }
}