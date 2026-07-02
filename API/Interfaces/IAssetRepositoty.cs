using System;
using API.DTOs;
using API.DTOs.Asset;
using API.Entities;
using API.Helper;

namespace API.Interfaces;

public interface IAssetRepository

{
        // ---------------- AssetType (category) ----------------
    Task<List<AssetTypeDto>> GetAssetTypesAsync();
    Task<List<AssetTypeLookupDto>> GetAssetTypeLookupAsync();
    Task<AssetTypeDto?> GetAssetTypeByIdAsync(Guid id);
    Task<AssetType?> GetAssetTypeEntityByIdAsync(Guid id);
    Task<bool> AssetTypeNameExistsAsync(string name, Guid? excludingId = null);
    Task AddAssetTypeAsync(AssetType assetType);
    void UpdateAssetType(AssetType assetType);
    Task<int> CountAssetsOfTypeAsync(Guid assetTypeId);
    void RemoveAssetType(AssetType assetType);
 
    // ---------------- AssetTypeField (dynamic schema) ----------------
    Task<AssetTypeField?> GetFieldEntityByIdAsync(Guid fieldId);
    Task<List<AssetTypeField>> GetFieldsForTypeAsync(Guid assetTypeId);
    Task<bool> FieldNameExistsAsync(Guid assetTypeId, string name, Guid? excludingId = null);
    Task AddFieldAsync(AssetTypeField field);
    void UpdateField(AssetTypeField field);
    void RemoveField(AssetTypeField field);
    Task<bool> FieldHasValuesAsync(Guid fieldId);
 
    // ---------------- AssetTypeFieldOption (dropdown choices) ----------------
    Task<AssetTypeFieldOption?> GetOptionEntityByIdAsync(Guid optionId);
    Task<bool> OptionValueExistsAsync(Guid fieldId, string value, Guid? excludingId = null);
    Task AddOptionAsync(AssetTypeFieldOption option);
    void UpdateOption(AssetTypeFieldOption option);
    void RemoveOption(AssetTypeFieldOption option);
    Task<bool> OptionValueInUseAsync(Guid fieldId, string value);
 
    // ---------------- Asset (data) ----------------
    Task<PaginatedResult<AssetDto>> GetAllAsync(PagingParams pagingParams, Guid? assetTypeId, Enums.AssetStatus? status);
    Task<PaginatedResult<AssetDto>> SearchAsync(AssetSearchRequest request);
    Task<AssetDetailDto?> GetByIdAsync(Guid id);
    Task<List<AssetLookupDto>> GetLookupAsync(string? search, Guid? assetTypeId);
    Task<Asset?> GetEntityByIdAsync(Guid id);
 
    Task AddAsync(Asset asset);
    void Update(Asset asset);
    void SoftDelete(Asset asset);
 
    // ---------------- AssetAttributeValue (EAV values) ----------------
    Task ReplaceAttributeValuesAsync(Asset asset, Dictionary<string, object?> attributes, List<AssetTypeField> schema);
    Task SetSingleAttributeValueAsync(Asset asset, AssetTypeField field, object? rawValue);
 
    // ---------------- CostAssetHist (maintenance) ----------------
    Task<PaginatedResult<CostAssetHistDto>> GetMaintenanceHistoryAsync(Guid assetId, PagingParams pagingParams);
    

    // ---------------- Contract history (per asset) ----------------
    Task<PaginatedResult<AssetContractHistDto>> GetContractHistoryAsync(Guid assetId, PagingParams pagingParams);


    // ---------------- Photos ----------------
    Task<bool> HasPhotosAsync(Guid assetId);
    Task AddPhotoAsync(Photo photo);
    Task<Photo?> GetPhotoByIdAsync(Guid photoId);
    Task<Photo?> GetFirstPhotoAsync(Guid assetId);
    Task<List<Photo>> GetPhotosAsync(Guid assetId);
    void RemovePhoto(Photo photo);
}
