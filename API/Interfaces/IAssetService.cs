using System;
using API.DTOs.Asset;
using API.Helper;
using static API.Entities.Enums;

namespace API.Interfaces;

public interface IAssetService
{
   
    // ---------------- AssetType (category) management ----------------
    Task<List<AssetTypeDto>> GetAssetTypesAsync();
    Task<List<AssetTypeLookupDto>> GetAssetTypeLookupAsync();
    Task<AssetTypeDto?> GetAssetTypeByIdAsync(Guid id);
    Task<AssetTypeDto> CreateAssetTypeAsync(AssetTypeCreateDto dto, string currentUserId);
    Task<AssetTypeDto> UpdateAssetTypeAsync(Guid id, AssetTypeUpdateDto dto, string currentUserId);
    Task DeleteAssetTypeAsync(Guid id);
 
    // ---------------- AssetTypeField (dynamic schema) management ----------------
    Task<AssetTypeFieldDto> AddFieldAsync(Guid assetTypeId, AssetTypeFieldCreateDto dto, string currentUserId);
    Task<AssetTypeFieldDto> UpdateFieldAsync(Guid assetTypeId, Guid fieldId, AssetTypeFieldUpdateDto dto, string currentUserId);
    Task DeleteFieldAsync(Guid assetTypeId, Guid fieldId);
 
    // ---------------- AssetTypeFieldOption (dropdown choices) management ----------------
    Task<AssetTypeFieldOptionDto> AddOptionAsync(Guid assetTypeId, Guid fieldId, AssetTypeFieldOptionCreateDto dto, string currentUserId);
    Task<AssetTypeFieldOptionDto> UpdateOptionAsync(Guid assetTypeId, Guid fieldId, Guid optionId, AssetTypeFieldOptionUpdateDto dto, string currentUserId);
    Task DeleteOptionAsync(Guid assetTypeId, Guid fieldId, Guid optionId);
 
    // ---------------- Asset (data) CRUD ----------------
     Task<PaginatedResult<AssetDto>> GetAllAsync(PagingParams pagingParams, Guid? assetTypeId, AssetStatus? status, DateTime? availableFrom = null, DateTime? availableTo = null);
    Task<AssetDetailDto> GetByIdAsync(Guid id);
    Task<List<AssetLookupDto>> GetLookupAsync(string? search, Guid? assetTypeId);
    Task<AssetDto> CreateAsync(AssetCreateDto dto, string currentUserId);
    Task<AssetDto> UpdateAsync(Guid id, AssetUpdateDto dto, string currentUserId);
    Task<AssetDto> UpdateAttributeAsync(Guid id, AssetAttributeUpdateDto dto, string currentUserId);
    Task<AssetDto> UpdateStatusAsync(Guid id, AssetStatusUpdateDto dto, string currentUserId);
    Task DeleteAsync(Guid id, string currentUserId);
 
    // ---------------- Dynamic facet search (eBay-style filter panel) ----------------
    Task<PaginatedResult<AssetDto>> SearchAsync(AssetSearchRequest request);
 
    // ---------------- Maintenance history ----------------
    Task<PaginatedResult<CostAssetHistDto>> GetMaintenanceHistoryAsync(Guid assetId, PagingParams pagingParams);
    Task<CostAssetHistDto> AddMaintenanceRecordAsync(Guid assetId, CostAssetHistCreateDto dto, string currentUserId);
    Task<CostAssetHistDto> UpdateMaintenanceRecordAsync(Guid assetId, Guid recordId, CostAssetHistUpdateDto dto, string currentUserId);
   
    Task DeleteMaintenanceRecordAsync(Guid assetId, Guid recordId, string currentUserId);
    

    // ---------------- Contract history (per asset) ----------------
    Task<PaginatedResult<AssetContractHistDto>> GetContractHistoryAsync(Guid assetId, PagingParams pagingParams);
    Task<AssetAvailabilityDto> CheckAvailabilityAsync(Guid assetId, DateTime from, DateTime to);
    Task<List<AssetCalendarEntryDto>> GetCalendarAsync(DateTime from, DateTime to);
    Task<List<AssetContractPeriodDto>> GetContractPeriodsAsync(Guid assetId);

    
    // ---------------- Photos ----------------
    Task<PhotoDto> AddPhotoAsync(Guid assetId, IFormFile file, string currentUserId);
    Task DeletePhotoAsync(Guid assetId, Guid photoId, string currentUserId);
    Task<AssetDetailDto> SetMainPhotoAsync(Guid assetId, Guid photoId, string currentUserId);
}
 
