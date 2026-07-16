using API.DTOs.Asset;
using API.Entities;
using API.Errors;
using API.Extensions;
using API.Helper;
using static API.Entities.Enums;
 
namespace API.Services;
 public partial class AssetService
{
    // ==================================================================
    //  ASSET (data) CRUD
    // ==================================================================

     public Task<PaginatedResult<AssetDto>> GetAllAsync(PagingParams pagingParams, Guid? assetTypeId, AssetStatus? status, DateTime? availableFrom = null, DateTime? availableTo = null)
        => unitOfWork.AssetRepository.GetAllAsync(pagingParams, assetTypeId, status, availableFrom, availableTo);
    public Task<AssetDetailDto?> GetByIdAsync(Guid id)
        => unitOfWork.AssetRepository.GetByIdAsync(id);

    public Task<List<AssetLookupDto>> GetLookupAsync(string? search, Guid? assetTypeId)
        => unitOfWork.AssetRepository.GetLookupAsync(search, assetTypeId);

    public async Task<AssetDto> CreateAsync(AssetCreateDto dto, string currentUserId)
    {
        var assetType = await unitOfWork.AssetRepository.GetAssetTypeEntityByIdAsync(dto.AssetTypeId)
            ?? throw new NotFoundException($"Asset category '{dto.AssetTypeId}' was not found.");

        var schema = assetType.Fields.ToList();

        var asset = new Asset
        {
            TenantId = tenantProvider.TenantId,
            AssetTypeId = dto.AssetTypeId,
            Name        = dto.Name,
            Notes       = dto.Notes,
            RateUnit    = dto.RateUnit,
            Cost        = dto.Cost,
            Status      = AssetStatus.Active,
            CreatedBy   = currentUserId
        };

        await unitOfWork.AssetRepository.AddAsync(asset);

        // Attribute values reference AssetId as their FK — EF Core resolves
        // this automatically once both are added to the same tracked context,
        // because Complete() generates the Asset's INSERT before the
        // AssetAttributeValue INSERTs in the same transaction.
        await unitOfWork.AssetRepository.ReplaceAttributeValuesAsync(asset, dto.Attributes, schema);
        await unitOfWork.Complete();

        return (await unitOfWork.AssetRepository.GetByIdAsync(asset.Id))!;
    }

     public async Task<AssetDto> UpdateAttributeAsync(Guid id, AssetAttributeUpdateDto dto, string currentUserId)
    {
        var asset = await unitOfWork.AssetRepository.GetEntityByIdAsync(id)
            ?? throw new NotFoundException($"Asset '{id}' was not found.");
        
        var schema = await unitOfWork.AssetRepository.GetFieldsForTypeAsync(asset.AssetTypeId);
        var field = schema.FirstOrDefault(f => f.Name == dto.FieldName)
            ?? throw new BadRequestException(
                $"'{dto.FieldName}' is not a valid field for this asset's category.");
 
        await unitOfWork.AssetRepository.SetSingleAttributeValueAsync(asset, field, dto.Value);
 
        asset.UpdatedAt = DateTime.UtcNow;
        asset.UpdatedBy = currentUserId;
        unitOfWork.AssetRepository.Update(asset);
 
        await unitOfWork.Complete();
 
        return (await unitOfWork.AssetRepository.GetByIdAsync(id))!;
    }

    public async Task<AssetDto> UpdateAsync(Guid id, AssetUpdateDto dto, string currentUserId)
    {
        var asset = await unitOfWork.AssetRepository.GetEntityByIdAsync(id)
            ?? throw new NotFoundException($"Asset '{id}' was not found.");
        
        // Έλεγχος optimistic concurrency: αν ο client έχει παλιό xmin,
        // κάποιος άλλος έχει ήδη αλλάξει το record
        if (dto.RowVersion != 0 && asset.xmin != dto.RowVersion)
            throw new ConflictException("Το πάγιο τροποποιήθηκε από άλλο χρήστη. Ανανεώστε τη σελίδα και δοκιμάστε ξανά.");

 

        var schema = await unitOfWork.AssetRepository.GetFieldsForTypeAsync(asset.AssetTypeId);

        asset.Name = dto.Name;
        asset.Notes = dto.Notes;
        asset.RateUnit = dto.RateUnit;
        asset.Cost     = dto.Cost;
        asset.Status    = dto.Status;
        asset.UpdatedAt = DateTime.UtcNow;
        asset.UpdatedBy = currentUserId;

        unitOfWork.AssetRepository.Update(asset);
        await unitOfWork.AssetRepository.ReplaceAttributeValuesAsync(asset, dto.Attributes, schema);
        await unitOfWork.Complete();

        return (await unitOfWork.AssetRepository.GetByIdAsync(id))!;
    }

    public async Task<AssetDto> UpdateStatusAsync(Guid id, AssetStatusUpdateDto dto, string currentUserId)
    {
        var asset = await unitOfWork.AssetRepository.GetEntityByIdAsync(id)
            ?? throw new NotFoundException($"Asset '{id}' was not found.");

        asset.Status = dto.Status;
        asset.UpdatedAt = DateTime.UtcNow;
        asset.UpdatedBy = currentUserId;

        unitOfWork.AssetRepository.Update(asset);
        await unitOfWork.Complete();

        return (await unitOfWork.AssetRepository.GetByIdAsync(id))!;
    }

    public async Task DeleteAsync(Guid id, string currentUserId)
    {
        var asset = await unitOfWork.AssetRepository.GetEntityByIdAsync(id)
            ?? throw new NotFoundException($"Asset '{id}' was not found.");

        var isRented = await unitOfWork.AssetRepository.HasActiveContractAsync(id);
        if (isRented)
            throw new BadRequestException("Cannot delete an asset that is currently rented.");

        asset.DeletedBy = currentUserId;
        unitOfWork.AssetRepository.SoftDelete(asset);
        await unitOfWork.Complete();
    }

    // ==================================================================
    //  DYNAMIC FACET SEARCH
    //  This is the validation gate that makes AssetRepository.SearchAsync's
    //  raw-SQL field-name interpolation safe: every FieldName in the request
    //  must be an exact match for a real field belonging to THIS tenant's
    //  THIS AssetType — never arbitrary text from the client.
    // ==================================================================

    public async Task<PaginatedResult<AssetDto>> SearchAsync(AssetSearchRequest request)
    {
         if (request.Filters.Count > 0)
        {
             var schema = await unitOfWork.AssetRepository.GetFieldsForTypeAsync(request.AssetTypeId);
            var schemaByName = schema.ToDictionary(f => f.Name);
                     foreach (var filter in request.Filters)
            {
                if (!schemaByName.TryGetValue(filter.FieldName, out var field))
                    throw new BadRequestException( $"'{filter.FieldName}' is not a valid filter field for this asset category.");

                var isRangeFilter = filter.MinValue.HasValue || filter.MaxValue.HasValue
                                  || filter.MinDate.HasValue || filter.MaxDate.HasValue;

                if (isRangeFilter && field.DataType is not (FieldDataType.Number or FieldDataType.Date or FieldDataType.DateTime))
                    throw new BadRequestException($"Field '{field.Label}' does not support range filtering.");
            }
        }

        return await unitOfWork.AssetRepository.SearchAsync(request);

    }

    // ==================================================================
    //  CONTRACT HISTORY
    // ==================================================================
    public async Task<PaginatedResult<AssetContractHistDto>> GetContractHistoryAsync(Guid assetId, PagingParams pagingParams)
        => await unitOfWork.AssetRepository.GetContractHistoryAsync(assetId, pagingParams);

    public async Task<AssetAvailabilityDto> CheckAvailabilityAsync(Guid assetId, DateTime from, DateTime to)
    {
        var conflicts = await unitOfWork.AssetRepository.GetOccupiedPeriodsInRangeAsync(assetId, from, to);
        return new AssetAvailabilityDto { IsAvailable = conflicts.Count == 0, Conflicts = conflicts };
    }

    public Task<List<AssetCalendarEntryDto>> GetCalendarAsync(DateTime from, DateTime to)
        => unitOfWork.AssetRepository.GetCalendarAsync(from, to);

    public Task<List<AssetContractPeriodDto>> GetContractPeriodsAsync(Guid assetId)
    => unitOfWork.AssetRepository.GetContractPeriodsAsync(assetId);

    // ==================================================================
    //  PHOTOS
    // ==================================================================

    public async Task<PhotoDto> AddPhotoAsync(Guid assetId, IFormFile file, string currentUserId)
    {
        var asset = await unitOfWork.AssetRepository.GetEntityByIdAsync(assetId)
            ?? throw new NotFoundException($"Asset '{assetId}' was not found.");

        var uploadResult = await photoService.AddPhotoAsync(file);

        var isFirst = !await unitOfWork.AssetRepository.HasPhotosAsync(assetId);

        var photo = new Photo
        {
            TenantId = asset.TenantId,
            Url = uploadResult.Url,
            PublicId = uploadResult.PublicId,
            IsMain = isFirst,
            AssetId = assetId
        };

        await unitOfWork.AssetRepository.AddPhotoAsync(photo);

        if (isFirst)
        {
            asset.PhotoUrl = uploadResult.Url;
            unitOfWork.AssetRepository.Update(asset);
        }

        await unitOfWork.Complete();

        return new PhotoDto { Id = photo.Id, Url = photo.Url, IsMain = photo.IsMain };
    }

    public async Task DeletePhotoAsync(Guid assetId, Guid photoId, string currentUserId)
    {
        var asset = await unitOfWork.AssetRepository.GetEntityByIdAsync(assetId)
            ?? throw new NotFoundException($"Asset '{assetId}' was not found.");

        var photo = await unitOfWork.AssetRepository.GetPhotoByIdAsync(photoId)
            ?? throw new NotFoundException($"Photo '{photoId}' was not found.");

        if (photo.AssetId != assetId)
            throw new BadRequestException("Photo does not belong to this asset.");

        if (photo.PublicId is not null)
            await photoService.DeletePhotoAsync(photo.PublicId);

        var wasMain = photo.IsMain;
        unitOfWork.AssetRepository.RemovePhoto(photo);

        if (wasMain)
        {
            asset.PhotoUrl = null;
            unitOfWork.AssetRepository.Update(asset);
        }

        await unitOfWork.Complete();

        if (wasMain)
        {
            var next = await unitOfWork.AssetRepository.GetFirstPhotoAsync(assetId);
            if (next is not null)
            {
                next.IsMain = true;
                asset.PhotoUrl = next.Url;
                unitOfWork.AssetRepository.Update(asset);
                await unitOfWork.Complete();
            }
        }
    }

    public async Task<AssetDetailDto> SetMainPhotoAsync(Guid assetId, Guid photoId, string currentUserId)
    {
        var asset = await unitOfWork.AssetRepository.GetEntityByIdAsync(assetId)
            ?? throw new NotFoundException($"Asset '{assetId}' was not found.");

        var photos = await unitOfWork.AssetRepository.GetPhotosAsync(assetId);
        var newMain = photos.FirstOrDefault(p => p.Id == photoId)
            ?? throw new NotFoundException($"Photo '{photoId}' was not found on this asset.");

        foreach (var p in photos)
            p.IsMain = p.Id == photoId;

        asset.PhotoUrl = newMain.Url;
        unitOfWork.AssetRepository.Update(asset);

        await unitOfWork.Complete();

        return (await unitOfWork.AssetRepository.GetByIdAsync(assetId))!;
    }


     // ==================================================================
    //  MAINTENANCE HISTORY — backed by Payment (Expense + PaymentAsset)
    // ==================================================================

    public Task<PaginatedResult<CostAssetHistDto>> GetMaintenanceHistoryAsync(Guid assetId, PagingParams pagingParams)
        => unitOfWork.AssetRepository.GetMaintenanceHistoryAsync(assetId, pagingParams);
    public async Task<CostAssetHistDto> AddMaintenanceRecordAsync(
        Guid assetId, CostAssetHistCreateDto dto, string currentUserId)
    {
        var asset = await unitOfWork.AssetRepository.GetEntityByIdAsync(assetId)
            ?? throw new NotFoundException($"Asset '{assetId}' was not found.");

        var payment = new Payment
        {
            TenantId          = asset.TenantId,
            Amount            = dto.Cost,
            UnallocatedAmount = dto.Cost,
            PaymentDate       = DateTime.SpecifyKind(dto.Date, DateTimeKind.Utc),
            PaymentMethod     = PaymentMethod.Cash,
            Description       = dto.Description,
            Notes             = dto.MaintainedBy,
            TransactionType   = TransactionType.Expense,
            CreatedBy         = currentUserId,
            PaymentAssets     = new List<PaymentAsset>
            {
                new() { AssetId = assetId, TenantId = asset.TenantId }
            }
        };

        await unitOfWork.PaymentRepository.AddAsync(payment);
        await unitOfWork.Complete();

        return new CostAssetHistDto
        {
            Id           = payment.Id,
            Date         = DateTime.SpecifyKind(payment.PaymentDate, DateTimeKind.Utc),
            Description  = payment.Description ?? string.Empty,
            Cost         = payment.Amount,
            MaintainedBy = payment.Notes,
            RowVersion   = payment.xmin
        };
    }

    public async Task<CostAssetHistDto> UpdateMaintenanceRecordAsync(
        Guid assetId, Guid recordId, CostAssetHistUpdateDto dto, string currentUserId)
    {
        var payment = await unitOfWork.PaymentRepository.GetEntityByIdAsync(recordId)
            ?? throw new NotFoundException($"Maintenance record '{recordId}' was not found.");

        if (payment.TransactionType != TransactionType.Expense)
            throw new NotFoundException($"Maintenance record '{recordId}' was not found.");

        if (!payment.PaymentAssets.Any(pa => pa.AssetId == assetId))
            throw new NotFoundException($"Maintenance record '{recordId}' does not belong to this asset.");

        payment.PaymentDate       = DateTime.SpecifyKind(dto.Date, DateTimeKind.Utc);
        payment.Description       = dto.Description;
        payment.Amount            = dto.Cost;
        payment.UnallocatedAmount = dto.Cost;
        payment.Notes             = dto.MaintainedBy;
        payment.UpdatedAt         = DateTime.UtcNow;
        payment.UpdatedBy         = currentUserId;

        await unitOfWork.Complete();

        return new CostAssetHistDto
        {
            Id           = payment.Id,
            Date         = payment.PaymentDate,
            Description  = payment.Description ?? string.Empty,
            Cost         = payment.Amount,
            MaintainedBy = payment.Notes,
            RowVersion   = payment.xmin
        };
    }

    public async Task DeleteMaintenanceRecordAsync(
        Guid assetId, Guid recordId, string currentUserId)
    {
        var payment = await unitOfWork.PaymentRepository.GetEntityByIdAsync(recordId)
            ?? throw new NotFoundException($"Maintenance record '{recordId}' was not found.");

        if (payment.TransactionType != TransactionType.Expense)
            throw new NotFoundException($"Maintenance record '{recordId}' was not found.");

        if (!payment.PaymentAssets.Any(pa => pa.AssetId == assetId))
            throw new NotFoundException($"Maintenance record '{recordId}' does not belong to this asset.");

        payment.DeletedBy = currentUserId;
        unitOfWork.PaymentRepository.SoftDelete(payment, currentUserId);
        await unitOfWork.Complete();
    }
}