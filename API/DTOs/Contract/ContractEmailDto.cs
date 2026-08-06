using System.ComponentModel.DataAnnotations;

namespace API.DTOs.Contract;

public class ContractEmailDto
{
    /// <summary>Αν μείνει κενό, χρησιμοποιείται το email της πρώτης επαφής του πελάτη.</summary>
    [EmailAddress]
    public string? To { get; set; }

    [MaxLength(200)]
    public string? Subject { get; set; }

    /// <summary>Προαιρετικό προσωπικό μήνυμα πριν τα στοιχεία του συμβολαίου.</summary>
    [MaxLength(2000)]
    public string? Message { get; set; }

    /// <summary>Αν true, το συμβόλαιο περνά από Εκκρεμές σε Ενεργό μετά την αποστολή.</summary>
    public bool ActivateContract { get; set; } = true;
}

public class ContractEmailResultDto
{
    public bool Sent { get; set; }
    public string SentTo { get; set; } = null!;
    public bool StatusChanged { get; set; }
    public string? Message { get; set; }
}
