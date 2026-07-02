using System;
using System.ComponentModel.DataAnnotations;
using API.Interfaces;

namespace API.Entities;

public class PaymentContract 
{
    [Required] 
    public Guid PaymentId { get; set; }
    [Required] 
    public Guid ContractId { get; set; }

    // Navigation Properties
    public Payment Payment { get; set; } = null!;
    public Contract Contract { get; set; } = null!;
}