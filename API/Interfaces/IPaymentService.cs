using API.DTOs.Payment;
using API.Helper;
using Microsoft.AspNetCore.Http;
using static API.Entities.Enums;

namespace API.Interfaces;

public interface IPaymentService
{
    Task<PaginatedResult<ContractPaymentDto>> GetContractsAsync(string? search, RentalStatus? status, PagingParams pagingParams);
    Task<PaymentListItemDto> RecordIncomeAsync(IncomeCreateDto dto, string userId);
    Task<PaymentListItemDto> RecordExpenseAsync(ExpenseCreateDto dto, IFormFile? file, string userId);
    Task<PaginatedResult<PaymentListItemDto>> GetIncomeAsync(PagingParams pagingParams, Guid? contractId);
    Task<PaginatedResult<PaymentListItemDto>> GetExpensesAsync(PagingParams pagingParams);
    Task DeleteAsync(Guid id, string userId);
}
