using System;

namespace API.DTOs;

public class MemberListDto
{
    public string Id { get; set; } = "";
    public string FullName { get; set; } = ""; // Συνδυασμός First/Last Name
    public string Afm { get; set; } = "";
    
    // Στοιχεία Χρήστη (Flattened)
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
    
    // Στοιχεία Μονάδας (Flattened)
    public string TentalName { get; set; } = "";
    public Guid TentalId { get; set; } = Guid.Empty;
    
    public DateTime LastActive { get; set; }
    
    public bool IsLockout { get; set; }
    

}
