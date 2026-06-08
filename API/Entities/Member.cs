using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace API.Entities;

//αφορα στα μέλη (υπαλληλους) που είναι υπεύθυνοι για την διαχείριση των περιουσιακών στοιχείων (Rents) και των τιμολογίων (invoices) που συνδέονται με μια εταιρία (Tenancy ).
//συνδεεται με μια εταιρία (Tenancy ) και με το AppUser για authentication/authorization, καθώς κάθε μέλος αντιστοιχεί σε έναν χρήστη της εφαρμογής.
public class Member

{
    [Key, MaxLength(50)]
    public string Id { get; set; } = null!; // Ίδιο Id με το AppUser.Id
    
    [Required, MaxLength(50)]
    public required string FirstName { get; set; }
    
    [Required, MaxLength(50)]
    public required string LastName { get; set; }
    
    [Required, MaxLength(9)]
    public required string Afm { get; set; }
    
    [Required, MaxLength(11)]
    public required string Amka { get; set; }

    public DateTime Created { get; set; } = DateTime.UtcNow;
    public DateTime LastActive { get; set; } = DateTime.UtcNow;

    // Navigation Properties
    [ForeignKey(nameof(Id))]
    public  AppUser User { get; set; } = null!;



}
