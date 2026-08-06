using System;

// Ίδιο namespace με το γειτονικό MemberListDto (ο φάκελος DTOs/User φιλοξενεί
// ιστορικά και τα δύο namespaces).
namespace API.DTOs;

/// <summary>
/// Στοιχεία μέλους όπως τα βλέπει ο διαχειριστής της εταιρείας στη λίστα χρηστών.
/// Δεν εκθέτει ποτέ credentials ή refresh tokens.
/// </summary>
public class TenantMemberDto
{
    public string Id { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Email { get; set; } = "";
    public List<string> Roles { get; set; } = [];
    public bool IsActive { get; set; }
    public DateTime? Created { get; set; }
    public DateTime? LastActive { get; set; }
}

public class SetMemberActiveDto
{
    public bool IsActive { get; set; }
}
