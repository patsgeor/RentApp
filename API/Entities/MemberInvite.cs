using System;

namespace API.Entities;

public class MemberInvite :BaseEntity
{

    public required string Email { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string Role { get; set; } 
    public required string Token { get; set; }
    public DateTime ExpiresAt { get; set; } 
    public bool IsUsed { get; set; } = false;


}
