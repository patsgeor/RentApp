using API.Data.Contexts;
using API.DTOs.Contract;
using API.Entities;
using API.Helper;
using API.Interfaces;
using Microsoft.EntityFrameworkCore;
using static API.Entities.Enums;

namespace API.Data.Repositories;

public class ContractRepository(AppDbContext context) : IContractRepository
{
    public async Task<PaginatedResult<ContractListItemDto>> GetAllAsync(ContractParams p)
    {
        var query = context.Contracts.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(p.Search))
        {
            var term = p.Search.Trim().ToLower();
            query = query.Where(c =>
                c.Customer.Name.ToLower().Contains(term) ||
                (c.ReferenceCode != null && c.ReferenceCode.ToLower().Contains(term)) );
        }

        if (p.Status.HasValue)
            query = query.Where(c => c.Status == p.Status.Value);

        if (p.CustomerId.HasValue)
            query = query.Where(c => c.CustomerId == p.CustomerId.Value);

        var projected = query
            .OrderByDescending(c => c.StartDate)
            .Select(c => new ContractListItemDto
            {
                Id                 = c.Id,
                CustomerName       = c.Customer.Name,
                ReferenceCode      = c.ReferenceCode,
                StartDate          = c.StartDate,
                EndDate            = c.EndDate,
                TotalAmount        = c.TotalAmount,
                Status             = c.Status,
                AssetNames         = c.ContractAssets.Select(ca => ca.Asset.Name).ToList(),
                PaidAmount         = c.PaymentContracts
                    .Where(pc => pc.Payment.TransactionType == TransactionType.Income && !pc.Payment.IsDeleted)
                    .Sum(pc => (decimal?)pc.Payment.Amount) ?? 0m,
                OutstandingBalance = c.TotalAmount - (c.PaymentContracts
                    .Where(pc => pc.Payment.TransactionType == TransactionType.Income && !pc.Payment.IsDeleted)
                    .Sum(pc => (decimal?)pc.Payment.Amount) ?? 0m)
            });

        return await PaginationHelper.CreateAsync(projected, p.PageNumber, p.PageSize);
    }

    public Task<ContractDetailDto?> GetByIdAsync(Guid id)
    {
        return context.Contracts
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new ContractDetailDto
            {
                Id                  = c.Id,
                RowVersion          = c.xmin,
                CustomerId          = c.CustomerId,
                CustomerName        = c.Customer.Name,
                ReferenceCode       = c.ReferenceCode,
                StartDate           = c.StartDate,
                EndDate             = c.EndDate,
                SignedDate          = c.SignedDate,
                TotalAmount         = c.TotalAmount,
                TaxAmount           = c.TaxAmount,
                DiscountAmount      = c.DiscountAmount,
                Status              = c.Status,
                InstallmentFrequency = c.InstallmentFrequency,
                Notes               = c.Notes,
                Terms               = c.Terms,
                Assets = c.ContractAssets.Select(ca => new ContractAssetDto
                {
                    Id               = ca.Id,
                    AssetId          = ca.AssetId,
                    AssetName        = ca.Asset.Name,
                    StartDate        = ca.StartDate,
                    EndDate          = ca.EndDate,
                    UnitCost         = ca.UnitCost,
                    RateUnit         = ca.RateUnit,
                    CalculatedAmount = ca.CalculatedAmount,
                    Notes            = ca.Notes
                }).ToList()
            })
            .FirstOrDefaultAsync();
    }

    public async Task<List<AvailableAssetDto>> GetAvailableAssetsAsync(
        DateTime start, DateTime end, Guid? excludeContractId = null)
    {
         // Npgsql requires DateTimeKind.Utc for query parameters
        start = DateTime.SpecifyKind(start, DateTimeKind.Utc);
        end   = DateTime.SpecifyKind(end,   DateTimeKind.Utc);

        // Collect IDs of assets already booked for overlapping Pending/Active contracts
        var busyIds = await context.ContractAssets
            .AsNoTracking()
            .Where(ca =>
                ca.StartDate < end && ca.EndDate > start &&
                (ca.Contract.Status == RentalStatus.Active || ca.Contract.Status == RentalStatus.Pending) &&
                (excludeContractId == null || ca.ContractId != excludeContractId))
            .Select(ca => ca.AssetId)
            .Distinct()
            .ToListAsync();

        return await context.Assets
            .AsNoTracking()
            .Where(a => !busyIds.Contains(a.Id))
            .OrderBy(a => a.Name)
            .Select(a => new AvailableAssetDto
            {
                Id            = a.Id,
                Name          = a.Name,
                AssetTypeName = a.AssetType.Name,
                Cost          = a.Cost,
                RateUnit      = a.RateUnit
            })
            .ToListAsync();
    }

    public void Add(Contract contract) => context.Contracts.Add(contract);

    public void Update(Contract contract) => context.Contracts.Update(contract);

    public void Remove(Contract contract)
    {
        contract.IsDeleted = true;
        contract.DeletedAt = DateTime.UtcNow;
    }

    public Task<Contract?> FindAsync(Guid id) =>
    context.Contracts
        .FirstOrDefaultAsync(c => c.Id == id);

// Raw DELETE - no xmin in the WHERE clause
public Task DeleteAllAssetsAsync(Guid contractId) =>
    context.Database.ExecuteSqlRawAsync(
        "DELETE FROM \"ContractAssets\" WHERE \"ContractId\" = {0}", contractId);

public void AddAssets(IEnumerable<ContractAsset> assets) =>
    context.ContractAssets.AddRange(assets);
}
