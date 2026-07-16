using System;
using static API.Entities.Enums;

namespace API.DTOs;

public class UserDto
{
    public required string Id { get; set; }
    public required string Email { get; set; }
    public required string DisplayName { get; set; }
    public required string TenantId { get; set; }
    public required string TenantName { get; set; }
    public PlanType PlanType { get; set; }

    public required string Token { get; set; }

}
