using API.DTOs.Contract;
using API.Helper;

namespace API.Interfaces;

public interface IContractService
{
    Task<PaginatedResult<ContractListItemDto>> GetAllAsync(ContractParams p);
    Task<ContractDetailDto?> GetByIdAsync(Guid id);
    Task<List<AvailableAssetDto>> GetAvailableAssetsAsync(DateTime start, DateTime end, Guid? excludeContractId = null);
    Task<ContractDetailDto> CreateAsync(ContractCreateDto dto, string memberId);
    Task<ContractDetailDto> UpdateAsync(Guid id, ContractUpdateDto dto, string memberId);
    // Σκοπίμως δεν υπάρχει DeleteAsync: η ματαίωση συμβολαίου εκφράζεται ως
    // μεταβολή κατάστασης σε RentalStatus.Cancelled μέσω του UpdateAsync.

    /// <summary>Αποστολή συμβολαίου με email (HTML τύπου τιμολογίου) + προαιρετικά συνημμένα.</summary>
    Task<ContractEmailResultDto> SendByEmailAsync(
        Guid id, ContractEmailDto dto, IEnumerable<EmailAttachment> attachments, string memberId, string? senderEmail = null);

    /// <summary>Μεταφέρει σε Ολοκληρωμένο όσα ενεργά συμβόλαια έχουν λήξει.</summary>
    Task RefreshCompletedStatusesAsync();
}
