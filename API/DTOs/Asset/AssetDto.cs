using System;
using System.ComponentModel.DataAnnotations;
using static API.Entities.Enums;

namespace API.DTOs.Asset;

// ============================================================
//  Asset — list/detail view. "Attributes" is a flattened key->value map
//  built from AssetAttributeValue rows (or PropertiesJson), so the Angular
//  EAV field renderer can bind directly without knowing about the EAV tables.
// ============================================================
public class AssetDto
{
    public Guid Id { get; set; }
    public Guid AssetTypeId { get; set; }
    public string AssetTypeName { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Notes { get; set; }
     public RateUnit RateUnit { get; set; }
    public decimal Cost { get; set; }
    public AssetStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }

    public string? PhotoUrl { get; set; }
 
    public uint RowVersion { get; set; }   
    public Dictionary<string, object?> Attributes { get; set; } = new();
}
 
public class AssetDetailDto : AssetDto
{
    public List<PhotoDto> Photos { get; set; } = new();
}

public class AssetLookupDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public AssetStatus Status { get; set; }
}
 
public class AssetCreateDto
{
    public required Guid AssetTypeId { get; set; }
    public required string Name { get; set; }
    public string? Notes { get; set; }
    public RateUnit RateUnit { get; set; }
    public decimal Cost { get; set; }
 
    // key = AssetTypeField.Name, value = raw value from the Angular form
    // (always sent as JSON; server parses/validates against the field's DataType)
    // e.g. { "Color": "Red", "Mileage": 12345, "FirstRegistration": "2020-01-01" }
    public Dictionary<string, object?> Attributes { get; set; } = new();
}
 
public class AssetUpdateDto
{
    public uint RowVersion { get; set; }   
    public required string Name { get; set; }
    public string? Notes { get; set; }
    public RateUnit RateUnit { get; set; }
    public decimal Cost { get; set; }
    public Dictionary<string, object?> Attributes { get; set; } = new();
}
 
public class AssetStatusUpdateDto
{
    public uint RowVersion { get; set; }
    public AssetStatus Status { get; set; }
}

public class AssetContractHistDto
{
    public Guid ContractId { get; set; }
    public string CustomerName { get; set; } = null!;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public RentalStatus Status { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }
}
 

public class CostAssetHistDto
{
    public uint RowVersion { get; set; }
    public Guid Id { get; set; }
    public DateTime Date { get; set; }
    public string Description { get; set; }=null!;
    public decimal Cost { get; set; }
    public string? MaintainedBy { get; set; }
}
public class CostAssetHistUpdateDto
{
    public uint RowVersion { get; set; }
    public DateTime Date { get; set; }
    [Required, MaxLength(250)]
    public string Description { get; set; } = null!;
    [Range(0, double.MaxValue)]
    public decimal Cost { get; set; }
    [MaxLength(100)]
    public string? MaintainedBy { get; set; }
}

public class CostAssetHistCreateDto
{
    public DateTime Date { get; set; }
    public string Description { get; set; }=null!;
    public decimal Cost { get; set; }
    public string? MaintainedBy { get; set; }
}

public class AssetAttributeUpdateDto
{
    // Must match an AssetTypeField.Name on this asset's AssetType.
    public required string FieldName { get; set; }
    public object? Value { get; set; }
}

public class PhotoDto
{
    public Guid Id { get; set; }
    public string Url { get; set; } = null!;
    public bool IsMain { get; set; }
}
