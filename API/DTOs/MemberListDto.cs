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
    public string MonadaName { get; set; } = "";
    public int MonadaId { get; set; }
    
    public DateTime LastActive { get; set; }
    
    public bool IsLockout { get; set; }
    

}
