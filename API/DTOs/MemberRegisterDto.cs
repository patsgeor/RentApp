using System;
using System.ComponentModel.DataAnnotations;

namespace API.DTOs;

public class MemberRegisterDto
{

    [Required(ErrorMessage = "Το DisplayName είναι υποχρεωτικό")]
    [MaxLength(50, ErrorMessage = "Μέγιστο 50 χαρακτήρες")]
    public required string DisplayName { get; set; }

    [Required(ErrorMessage = "Το Email είναι υποχρεωτικό")]
    [EmailAddress(ErrorMessage = "Το Email δεν είναι έγκυρο")]
    [MaxLength(100)]
    public required string Email { get; set; }

    [Required(ErrorMessage = "Το FirstName είναι υποχρεωτικό")]
    [MaxLength(50, ErrorMessage = "Μέγιστο 50 χαρακτήρες")]
    public required string FirstName { get; set; }

    [Required(ErrorMessage = "Το LastName είναι υποχρεωτικό")]
    [MaxLength(50, ErrorMessage = "Μέγιστο 50 χαρακτήρες")]
    public required string LastName { get; set; }

    [Required(ErrorMessage = "Το ΑΦΜ είναι υποχρεωτικό")]
    [StringLength(9, MinimumLength = 9,
        ErrorMessage = "Το ΑΦΜ πρέπει να είναι 9 ψηφία")]
    [RegularExpression(@"^\d{9}$",
        ErrorMessage = "Το ΑΦΜ πρέπει να περιέχει μόνο αριθμούς")]
    public required string Afm { get; set; }

    [Required(ErrorMessage = "Το ΑΜΚΑ είναι υποχρεωτικό")]
    [StringLength(11, MinimumLength = 11,
        ErrorMessage = "Το ΑΜΚΑ πρέπει να είναι 11 ψηφία")]
    [RegularExpression(@"^\d{11}$",
        ErrorMessage = "Το ΑΜΚΑ πρέπει να περιέχει μόνο αριθμούς")]
    public required string Amka { get; set; }

    [Required(ErrorMessage = "Το MonadaId είναι υποχρεωτικό")]
    [Range(1, int.MaxValue,
        ErrorMessage = "Μη έγκυρο MonadaId")]
    public int MonadaId { get; set; }

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
