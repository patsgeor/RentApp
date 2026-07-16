using System;
using System.ComponentModel.DataAnnotations;

namespace API.DTOs;

public class TenantRegisterDto
{
    [Required(ErrorMessage = "Το CompanyName είναι υποχρεωτικό")]
    [MaxLength(100, ErrorMessage = "Μέγιστο 100 χαρακτήρες")]
     public required string CompanyName { get; set; } 
    
    [RegularExpression(@"^(\d{9})?$",
    ErrorMessage = "Το ΑΦΜ πρέπει να περιέχει μόνο αριθμούς")]
    public string? VatNumber { get; set; } // ΑΦΜ
    
    [MaxLength(500)]
    public string? ContactInfo { get; set; }

    [Required(ErrorMessage = "Το DisplayName είναι υποχρεωτικό")]
    [MaxLength(50, ErrorMessage = "Μέγιστο 50 χαρακτήρες")]
    public required string DisplayName { get; set; }

    [Required(ErrorMessage = "Το Email είναι υποχρεωτικό")]
    [EmailAddress(ErrorMessage = "Το Email δεν είναι έγκυρο")]
    [MaxLength(100)]
    public required string Email { get; set; }

    [Required(ErrorMessage = "Το Email είναι υποχρεωτικό")]
    [MaxLength(20)]
    public required string PhoneNumber { get; set; } 

    [Required(ErrorMessage = "Το FirstName είναι υποχρεωτικό")]
    [MaxLength(50, ErrorMessage = "Μέγιστο 50 χαρακτήρες")]
    public required string FirstName { get; set; }

    [Required(ErrorMessage = "Το LastName είναι υποχρεωτικό")]
    [MaxLength(50, ErrorMessage = "Μέγιστο 50 χαρακτήρες")]
    public required string LastName { get; set; }

    [Required(ErrorMessage = "Το Password είναι υποχρεωτικό")]
    [MinLength(6,
        ErrorMessage = "Ο κωδικός πρέπει να έχει τουλάχιστον 6 χαρακτήρες")]
    [MaxLength(100)]
    public required string Password { get; set; }

    [Required(ErrorMessage = "Η επιβεβαίωση κωδικού είναι υποχρεωτική")]
    [Compare(nameof(Password),
        ErrorMessage = "Οι κωδικοί δεν ταιριάζουν")]
    public required string ConfirmPassword { get; set; }
    
}
