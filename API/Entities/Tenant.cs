using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using static API.Entities.Enums;

namespace API.Entities;
//αφορά Multy-Tenancy, δηλαδή την υποστήριξη πολλαπλών εταιρειών στην ίδια βάση δεδομένων.
public class Tenant
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required, MaxLength(100)]
    public string Name { get; set; } = null!;
    
    [MaxLength(20)]
    public string? VatNumber { get; set; } // ΑΦΜ
    
    [MaxLength(500)]
    public string? ContactInfo { get; set; }

    public PlanType PlanType { get; set; } = PlanType.Free;
    public DateTime? PlanExpiresAt { get; set; }

    
    public SubscriptionStatus SubscriptionStatus { get; set; } = SubscriptionStatus.Active;

    // Navigation Properties
    [JsonIgnore]
    public  ICollection<AppUser> Users { get; set; } = new List<AppUser>();
    [JsonIgnore]
    public  ICollection<Customer> Customers { get; set; } = new List<Customer>();
}

