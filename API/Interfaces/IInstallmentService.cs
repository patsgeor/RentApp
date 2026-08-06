using API.DTOs.Payment;
using API.Helper;

namespace API.Interfaces;

public interface IInstallmentService
{
    // Δημιουργία οφειλών από συμβόλαιο
    Task GenerateInstallmentsAsync(Guid contractId, string userId);

    // Ανάκτηση οφειλών συμβολαίου
    Task<List<InstallmentDto>> GetByContractAsync(Guid contractId);

    // Εκπρόθεσμες (cross-contract)
    Task<PaginatedResult<InstallmentDto>> GetOverdueAsync(PagingParams pagingParams);
    
    // Οφειλές με φίλτρα (cross-contract)
    Task<PaginatedResult<InstallmentDto>> GetDebtsAsync(DebtParams p);

    // Batch: ενημέρωση status σε Overdue όσων πέρασε η DueDate
    Task RefreshOverdueStatusesAsync();

    // Αυτόματη αντιστοίχιση μέσω ReferenceCode + FIFO allocation
    Task<MatchResultDto> AutoMatchAsync(Guid paymentId, string userId);

    // Χειροκίνητη κατανομή πληρωμής σε οφειλές
    Task AllocateManuallyAsync(Guid paymentId, List<AllocationItemDto> items, string userId);

    // Αφαίρεση κατανομής
    Task DeallocateAsync(Guid allocationId, string userId);

    // Ακύρωση οφειλής
    Task CancelInstallmentAsync(Guid installmentId, string userId);
    Task NotifyByEmailAsync(Guid installmentId, string userId, string? senderEmail = null);

    Task UpdateScheduleAsync(Guid contractId, List<ScheduleInstallmentDto> schedule, string userId);
    Task<DebtStatsDto> GetStatsAsync(int? month, int? year);
}