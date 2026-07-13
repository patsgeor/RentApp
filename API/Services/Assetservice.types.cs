using System.Text.RegularExpressions;
using API.DTOs.Asset;
using API.Entities;
using API.Errors;
using API.Interfaces;
 
namespace API.Services;
 // Machine keys must be safe to interpolate into a jsonb ->> operator in raw
// SQL (see AssetRepository.SearchAsync) — restricting them at creation time
// to lowercase/digits/underscore means there is no further sanitization
// needed downstream, and it keeps Angular's dynamic form binding predictable.
public partial class AssetService(IUnitOfWork unitOfWork, ITenantProvider tenantProvider, IPhotoService photoService) : IAssetService
{
    private static readonly Regex FieldNamePattern = new(@"^[a-z][a-z0-9_]{1,49}$", RegexOptions.Compiled);
 
    // ==================================================================
    //  ASSET TYPE (category)
    // ==================================================================
 
    public async Task<List<AssetTypeDto>> GetAssetTypesAsync()
        => await unitOfWork.AssetRepository.GetAssetTypesAsync();
 
    public async Task<List<AssetTypeLookupDto>> GetAssetTypeLookupAsync()
        => await unitOfWork.AssetRepository.GetAssetTypeLookupAsync();
 
    public async Task<AssetTypeDto?> GetAssetTypeByIdAsync(Guid id)
        => await unitOfWork.AssetRepository.GetAssetTypeByIdAsync(id);
 
    public async Task<AssetTypeDto> CreateAssetTypeAsync(AssetTypeCreateDto dto, string currentUserId)
    {
        if (await unitOfWork.AssetRepository.AssetTypeNameExistsAsync(dto.Name))
            throw new BadRequestException($"An asset category named '{dto.Name}' already exists.");
 
        var assetType = new AssetType
        {
            TenantId = tenantProvider.TenantId,
            Name = dto.Name,
            Description = dto.Description
        };
 
        await unitOfWork.AssetRepository.AddAssetTypeAsync(assetType);
        await unitOfWork.Complete();
 
        return (await unitOfWork.AssetRepository.GetAssetTypeByIdAsync(assetType.Id))!;
    }
 
    public async Task<AssetTypeDto> UpdateAssetTypeAsync(Guid id, AssetTypeUpdateDto dto, string currentUserId)
    {
        var assetType = await unitOfWork.AssetRepository.GetAssetTypeEntityByIdAsync(id)
            ?? throw new NotFoundException($"Asset category '{id}' was not found.");
 
        if (await unitOfWork.AssetRepository.AssetTypeNameExistsAsync(dto.Name, excludingId: id))
            throw new BadRequestException($"An asset category named '{dto.Name}' already exists.");
 
        assetType.Name = dto.Name;
        assetType.Description = dto.Description;
 
        unitOfWork.AssetRepository.UpdateAssetType(assetType);
        await unitOfWork.Complete();
 
        return (await unitOfWork.AssetRepository.GetAssetTypeByIdAsync(id))!;
    }
 
    public async Task DeleteAssetTypeAsync(Guid id)
    {
        var assetType = await unitOfWork.AssetRepository.GetAssetTypeEntityByIdAsync(id)
            ?? throw new NotFoundException($"Asset category '{id}' was not found.");
 
        var assetCount = await unitOfWork.AssetRepository.CountAssetsOfTypeAsync(id);
        if (assetCount > 0)
            throw new BadRequestException(
                $"Cannot delete category '{assetType.Name}' — {assetCount} asset(s) still use it.");
 
        unitOfWork.AssetRepository.RemoveAssetType(assetType);
        await unitOfWork.Complete();
    }
 
    // ==================================================================
    //  ASSET TYPE FIELD (dynamic schema)
    // ==================================================================
 
    public async Task<AssetTypeFieldDto> AddFieldAsync(Guid assetTypeId, AssetTypeFieldCreateDto dto, string currentUserId)
    {
        var assetType = await unitOfWork.AssetRepository.GetAssetTypeEntityByIdAsync(assetTypeId)
            ?? throw new NotFoundException($"Asset category '{assetTypeId}' was not found.");
 
        ValidateFieldName(dto.Name);
 
        if (await unitOfWork.AssetRepository.FieldNameExistsAsync(assetTypeId, dto.Name))
            throw new BadRequestException($"Field '{dto.Name}' already exists on this category.");
 
        if (dto.MinValue.HasValue && dto.MaxValue.HasValue && dto.MinValue > dto.MaxValue)
            throw new BadRequestException("MinValue cannot be greater than MaxValue.");
 
        var field = new AssetTypeField
        {
            TenantId = tenantProvider.TenantId,
            AssetTypeId = assetTypeId,
            Name = dto.Name,
            Label = dto.Label,
            DataType = dto.DataType,
            Placeholder = dto.Placeholder,
            DefaultValue = dto.DefaultValue,
            DisplayOrder = dto.DisplayOrder,
            ValidationRegex = dto.ValidationRegex,
            MinValue = dto.MinValue,
            MaxValue = dto.MaxValue,
            IsRequired = dto.IsRequired,
            CreatedBy = currentUserId
        };
 
        if (dto.Options is { Count: > 0 })
        {
            field.Options = dto.Options.Select(o => new AssetTypeFieldOption
            {
                TenantId = tenantProvider.TenantId,
                Label = o.Label,
                Value = o.Value,
                DisplayOrder = o.DisplayOrder,
                CreatedBy = currentUserId
            }).ToList();
        }
 
        await unitOfWork.AssetRepository.AddFieldAsync(field);
        await unitOfWork.Complete();
 
        var saved = await unitOfWork.AssetRepository.GetFieldEntityByIdAsync(field.Id);
        return MapFieldToDto(saved!);
    }
 
    public async Task<AssetTypeFieldDto> UpdateFieldAsync(
        Guid assetTypeId, Guid fieldId, AssetTypeFieldUpdateDto dto, string currentUserId)
    {
        var field = await unitOfWork.AssetRepository.GetFieldEntityByIdAsync(fieldId);
 
        if (field == null || field.AssetTypeId != assetTypeId)
            throw new NotFoundException($"Field '{fieldId}' was not found on this category.");
 
        if (dto.MinValue.HasValue && dto.MaxValue.HasValue && dto.MinValue > dto.MaxValue)
            throw new BadRequestException("MinValue cannot be greater than MaxValue.");
 
        // Name and DataType are intentionally immutable — see AssetTypeFieldUpdateDto.
        field.Label = dto.Label;
        field.Placeholder = dto.Placeholder;
        field.DefaultValue = dto.DefaultValue;
        field.DisplayOrder = dto.DisplayOrder;
        field.ValidationRegex = dto.ValidationRegex;
        field.MinValue = dto.MinValue;
        field.MaxValue = dto.MaxValue;
        field.IsRequired = dto.IsRequired;
        field.UpdatedAt = DateTime.UtcNow;
        field.UpdatedBy = currentUserId;
 
        unitOfWork.AssetRepository.UpdateField(field);
        await unitOfWork.Complete();
 
        return MapFieldToDto(field);
    }
 
    public async Task DeleteFieldAsync(Guid assetTypeId, Guid fieldId)
    {
        var field = await unitOfWork.AssetRepository.GetFieldEntityByIdAsync(fieldId);
 
        if (field == null || field.AssetTypeId != assetTypeId)
            throw new NotFoundException($"Field '{fieldId}' was not found on this category.");
 
        if (await unitOfWork.AssetRepository.FieldHasValuesAsync(fieldId))
            throw new BadRequestException(
                $"Cannot delete field '{field.Label}' — one or more assets already store a value for it.");
 
        unitOfWork.AssetRepository.RemoveField(field);
        await unitOfWork.Complete();
    }
 
    // ==================================================================
    //  ASSET TYPE FIELD OPTION (dropdown choices, e.g. Color -> red/black/white)
    // ==================================================================
 
    public async Task<AssetTypeFieldOptionDto> AddOptionAsync(
        Guid assetTypeId, Guid fieldId, AssetTypeFieldOptionCreateDto dto, string currentUserId)
    {
        var field = await unitOfWork.AssetRepository.GetFieldEntityByIdAsync(fieldId);
        if (field == null || field.AssetTypeId != assetTypeId)
            throw new NotFoundException($"Field '{fieldId}' was not found on this category.");
 
        if (await unitOfWork.AssetRepository.OptionValueExistsAsync(fieldId, dto.Value))
            throw new BadRequestException($"Option value '{dto.Value}' already exists on this field.");
 
        var option = new AssetTypeFieldOption
        {
            TenantId = tenantProvider.TenantId,
            AssetTypeFieldId = fieldId,
            Label = dto.Label,
            Value = dto.Value,
            DisplayOrder = dto.DisplayOrder,
            CreatedBy = currentUserId
        };
 
        await unitOfWork.AssetRepository.AddOptionAsync(option);
        await unitOfWork.Complete();
 
        return new AssetTypeFieldOptionDto
        {
            Id = option.Id,
            Label = option.Label,
            Value = option.Value,
            DisplayOrder = option.DisplayOrder
        };
    }
 
    public async Task<AssetTypeFieldOptionDto> UpdateOptionAsync(
        Guid assetTypeId, Guid fieldId, Guid optionId, AssetTypeFieldOptionUpdateDto dto, string currentUserId)
    {
        var field = await unitOfWork.AssetRepository.GetFieldEntityByIdAsync(fieldId);
        if (field == null || field.AssetTypeId != assetTypeId)
            throw new NotFoundException($"Field '{fieldId}' was not found on this category.");
 
        var option = await unitOfWork.AssetRepository.GetOptionEntityByIdAsync(optionId);
        if (option == null || option.AssetTypeFieldId != fieldId)
            throw new NotFoundException($"Option '{optionId}' was not found on this field.");
 
        if (await unitOfWork.AssetRepository.OptionValueExistsAsync(fieldId, dto.Value, excludingId: optionId))
            throw new BadRequestException($"Option value '{dto.Value}' already exists on this field.");
 
        // Changing Value (not just Label) would silently orphan any asset that
        // already stores the OLD value in AssetAttributeValue.StringValue /
        // PropertiesJson — those assets would keep a value no longer offered
        // as a choice. Block it the same way DeleteOptionAsync does, unless
        // the Value is staying the same (a pure label/display-order edit).
        if (option.Value != dto.Value && await unitOfWork.AssetRepository.OptionValueInUseAsync(fieldId, option.Value))
            throw new BadRequestException(
                $"Cannot change value '{option.Value}' — one or more assets already use it. " +
                "Add a new option instead, or update those assets first.");
 
        option.Label = dto.Label;
        option.Value = dto.Value;
        option.DisplayOrder = dto.DisplayOrder;
        option.UpdatedAt = DateTime.UtcNow;
        option.UpdatedBy = currentUserId;
 
        unitOfWork.AssetRepository.UpdateOption(option);
        await unitOfWork.Complete();
 
        return new AssetTypeFieldOptionDto
        {
            Id = option.Id,
            Label = option.Label,
            Value = option.Value,
            DisplayOrder = option.DisplayOrder
        };
    }
 
    public async Task DeleteOptionAsync(Guid assetTypeId, Guid fieldId, Guid optionId)
    {
        var field = await unitOfWork.AssetRepository.GetFieldEntityByIdAsync(fieldId);
        if (field == null || field.AssetTypeId != assetTypeId)
            throw new NotFoundException($"Field '{fieldId}' was not found on this category.");
 
        var option = await unitOfWork.AssetRepository.GetOptionEntityByIdAsync(optionId);
        if (option == null || option.AssetTypeFieldId != fieldId)
            throw new NotFoundException($"Option '{optionId}' was not found on this field.");
 
        if (await unitOfWork.AssetRepository.OptionValueInUseAsync(fieldId, option.Value))
            throw new BadRequestException(
                $"Cannot delete option '{option.Label}' — one or more assets already use it.");
 
        unitOfWork.AssetRepository.RemoveOption(option);
        await unitOfWork.Complete();
    }
 
    private static void ValidateFieldName(string name)
    {
        if (!FieldNamePattern.IsMatch(name))
            throw new BadRequestException(
                "Field name must start with a lowercase letter and contain only lowercase letters, digits, and underscores (e.g. 'mileage', 'first_registration').");
    }
 
    private static AssetTypeFieldDto MapFieldToDto(AssetTypeField f) => new()
    {
        Id = f.Id,
        Name = f.Name,
        Label = f.Label,
        DataType = f.DataType,
        Placeholder = f.Placeholder,
        DefaultValue = f.DefaultValue,
        DisplayOrder = f.DisplayOrder,
        ValidationRegex = f.ValidationRegex,
        MinValue = f.MinValue,
        MaxValue = f.MaxValue,
        IsRequired = f.IsRequired,
        Options = f.Options.OrderBy(o => o.DisplayOrder).Select(o => new AssetTypeFieldOptionDto
        {
            Id = o.Id,
            Label = o.Label,
            Value = o.Value,
            DisplayOrder = o.DisplayOrder
        }).ToList()
    };
}