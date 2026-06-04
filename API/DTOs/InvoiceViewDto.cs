using System;

namespace API.DTOs;

public class InvoiceViewDto
{

    public long Id { get; set; }
        
    // Βασικά στοιχεία Τιμολογίου
    public string Afm { get; set; } = string.Empty;
    public DateOnly InvoiceDate { get; set; }
    public decimal Amount { get; set; }

    // Στοιχεία Χρηματοδότησης (Flattened)
    public long? FundingAllocationId { get; set; }
    public string? FundingAllocationProtocol { get; set; } = string.Empty; // Π.χ. "ΑΠ 12345/2024"
    public string? FundingSourceName { get; set; } = string.Empty;       // Π.χ. "ΕΣΠΑ"

    // Ποιος το καταχώρησε (Flattened)
    public string CreatedByMemberName { get; set; } = string.Empty;     // Π.χ. "Γιώργος Παπαδόπουλος"
    
    // Ημερομηνία Καταχώρησης (για έλεγχο)
    public DateTime CreatedAt { get; set; }

    // Λίστα με τα είδη του τιμολογίου (Οι γραμμές του)
    public List<InvoiceItemViewDto> Items { get; set; } = new();

}
