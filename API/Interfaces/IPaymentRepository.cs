using API.DTOs.Payment;
using API.Entities;
using API.Helper;
using static API.Entities.Enums;

namespace API.Interfaces;

public interface IPaymentRepository
{
      Task<PaginatedResult<ContractPaymentDto>> GetContractsAsync(string? search, RentalStatus? status, PagingParams pagingParams);
    Task<PaginatedResult<PaymentListItemDto>> GetIncomeAsync(PagingParams pagingParams, Guid? contractId);
    Task<PaginatedResult<PaymentListItemDto>> GetExpensesAsync(PagingParams pagingParams);
    Task AddAsync(Payment payment);
    Task<Payment?> GetEntityByIdAsync(Guid id);
    void SoftDelete(Payment payment, string userId);

    Task AddAttachmentAsync(FileAttachment attachment);
    Task<FileAttachment?> GetAttachmentAsync(Guid paymentId);
    void RemoveAttachment(FileAttachment attachment);

}
