using API.Data.Contexts;
using API.DTOs.Asset;
using API.Entities;
using API.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;

public partial class AssetRepository(
    AppDbContext context,
    ITenantProvider tenantProvider,
    IFileStorage fileStorage) : IAssetRepository
{
  

    // ==================================================================
    //  ASSET TYPE (category) — e.g. "Αυτοκίνητα", "Βιβλία", "Σπίτια"
    // ==================================================================
 
    public async Task<List<AssetTypeDto>> GetAssetTypesAsync()
    {
        return await context.AssetTypes
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(ProjectTypeToDto())
            .ToListAsync();
    }
 
    public async Task<List<AssetTypeLookupDto>> GetAssetTypeLookupAsync()
    {
        return await context.AssetTypes
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new AssetTypeLookupDto { Id = t.Id, Name = t.Name })
            .ToListAsync();
    }
 
    public async Task<AssetTypeDto?> GetAssetTypeByIdAsync(Guid id)
    {
        return await context.AssetTypes
            .AsNoTracking()
            .Where(t => t.Id == id)
            .Select(ProjectTypeToDto())
            .FirstOrDefaultAsync();
    }
 
    public async Task<AssetType?> GetAssetTypeEntityByIdAsync(Guid id)
    {
        return await context.AssetTypes
            .Include(t => t.Fields.OrderBy(f => f.DisplayOrder))
                .ThenInclude(f => f.Options.OrderBy(o => o.DisplayOrder))
            .FirstOrDefaultAsync(t => t.Id == id);
    }
 
    public async Task<bool> AssetTypeNameExistsAsync(string name, Guid? excludingId = null)
    {
        return await context.AssetTypes
            .AnyAsync(t => t.Name == name && (excludingId == null || t.Id != excludingId));
    }
 
    public async Task AddAssetTypeAsync(AssetType assetType)
    {
        await context.AssetTypes.AddAsync(assetType);
    }
 
    public void UpdateAssetType(AssetType assetType)
    {
        context.Entry(assetType).State = EntityState.Modified;
    }
 
    public async Task<int> CountAssetsOfTypeAsync(Guid assetTypeId)
    {
        return await context.Assets.CountAsync(a => a.AssetTypeId == assetTypeId);
    }
 
    public void RemoveAssetType(AssetType assetType)
    {
        // AssetType is not a BaseEntity (no soft-delete) — hard delete is fine
        // here because the service layer guards against deleting a type that
        // still has assets (see CountAssetsOfTypeAsync), so this never cascades
        // into orphaned Asset rows.
        context.AssetTypes.Remove(assetType);
    }
 
    private static System.Linq.Expressions.Expression<Func<AssetType, AssetTypeDto>> ProjectTypeToDto()
    {
        return t => new AssetTypeDto
        {
            Id = t.Id,
            Name = t.Name,
            Description = t.Description,
            AssetCount = t.Assets.Count,
            Fields = t.Fields.OrderBy(f => f.DisplayOrder).Select(f => new AssetTypeFieldDto
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
            }).ToList()
        };
    }
 
    // ==================================================================
    //  ASSET TYPE FIELD — the dynamic schema of a category
    // ==================================================================
 
    public async Task<AssetTypeField?> GetFieldEntityByIdAsync(Guid fieldId)
    {
        return await context.AssetTypeFields
            .Include(f => f.Options)
            .FirstOrDefaultAsync(f => f.Id == fieldId);
    }
 
    public async Task<List<AssetTypeField>> GetFieldsForTypeAsync(Guid assetTypeId)
    {
        return await context.AssetTypeFields
            .Include(f => f.Options)
            .Where(f => f.AssetTypeId == assetTypeId)
            .OrderBy(f => f.DisplayOrder)
            .ToListAsync();
    }
 
    public async Task<bool> FieldNameExistsAsync(Guid assetTypeId, string name, Guid? excludingId = null)
    {
        return await context.AssetTypeFields.AnyAsync(f =>
            f.AssetTypeId == assetTypeId &&
            f.Name == name &&
            (excludingId == null || f.Id != excludingId));
    }
 
    public async Task AddFieldAsync(AssetTypeField field)
    {
        await context.AssetTypeFields.AddAsync(field);
    }
 
    public void UpdateField(AssetTypeField field)
    {
        context.Entry(field).State = EntityState.Modified;
    }
 
    public void RemoveField(AssetTypeField field)
    {
        context.AssetTypeFields.Remove(field);
    }
 
    public async Task<bool> FieldHasValuesAsync(Guid fieldId)
    {
        return await context.AssetAttributeValues.AnyAsync(v => v.AssetTypeFieldId == fieldId);
    }

    
    // ==================================================================
    //  ASSET TYPE FIELD OPTION — dropdown/select choices for a field
    // ==================================================================
 
    public async Task<AssetTypeFieldOption?> GetOptionEntityByIdAsync(Guid optionId)
    {
        return await context.AssetTypeFieldOptions.FirstOrDefaultAsync(o => o.Id == optionId);
    }
 
    public async Task<bool> OptionValueExistsAsync(Guid fieldId, string value, Guid? excludingId = null)
    {
        return await context.AssetTypeFieldOptions.AnyAsync(o =>
            o.AssetTypeFieldId == fieldId &&
            o.Value == value &&
            (excludingId == null || o.Id != excludingId));
    }
 
    public async Task AddOptionAsync(AssetTypeFieldOption option)
    {
        await context.AssetTypeFieldOptions.AddAsync(option);
    }
 
    public void UpdateOption(AssetTypeFieldOption option)
    {
        context.Entry(option).State = EntityState.Modified;
    }
 
    public void RemoveOption(AssetTypeFieldOption option)
    {
        context.AssetTypeFieldOptions.Remove(option);
    }
 
    public async Task<bool> OptionValueInUseAsync(Guid fieldId, string value)
    {
        // An option's "Value" (e.g. "red") is what actually gets stored in
        // AssetAttributeValue.StringValue / PropertiesJson — not the option's
        // Id — so checking usage means matching on that stored string value.
        return await context.AssetAttributeValues.AnyAsync(v =>
            v.AssetTypeFieldId == fieldId && v.StringValue == value);
    }
}
