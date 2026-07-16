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
    AppDbContext context) : IContractService
{
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

        var totalAmount = assetLines.Sum(a => a.CalculatedAmount) - dto.DiscountAmount + dto.TaxAmount;

        var contract = new Contract
        {
            TenantId             = tenantProvider.TenantId,
            CustomerId           = dto.CustomerId,
            StartDate            = DateTime.SpecifyKind(dto.StartDate, DateTimeKind.Utc),
            EndDate              = DateTime.SpecifyKind(dto.EndDate, DateTimeKind.Utc),
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
        });
        uow.ContractRepository.AddAssets(newAssets);

        var totalAmount = dto.Assets.Sum(a => a.CalculatedAmount) - dto.DiscountAmount + dto.TaxAmount;

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

    return await uow.ContractRepository.GetByIdAsync(id)
        ?? throw new Exception("Αδυναμία ανάκτησης συμβολαίου μετά την ενημέρωση.");
}

    public async Task DeleteAsync(Guid id, string memberId)
    {
        var contract = await uow.ContractRepository.FindAsync(id)
            ?? throw new NotFoundException($"Συμβόλαιο {id} δεν βρέθηκε.");

        if (contract.Status == RentalStatus.Active)
            throw new BadRequestException("Δεν επιτρέπεται διαγραφή ενεργού συμβολαίου.");
        
        // Soft-delete installments — αν κάποια έχει PaymentInstallments (Restrict FK)
        // το soft delete δεν αγγίζει τη βάση ως hard delete, οπότε δεν υπάρχει πρόβλημα
        var installments = await context.Installments
                                .Where(i => i.ContractId == id)
                                .ToListAsync();

        var now = DateTime.UtcNow;
        foreach (var inst in installments)
        {
            inst.IsDeleted  = true;
            inst.DeletedAt  = now;
            inst.DeletedBy  = memberId;
        }

        contract.DeletedBy = memberId;
        uow.ContractRepository.Remove(contract);
        await uow.Complete();
    }
}
