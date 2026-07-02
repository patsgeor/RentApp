using System;
using API.DTOs;
using API.Entities;
using API.Interfaces;

namespace API.Extensions;

public static class AppUserExtensions
{
    public static async Task<UserDto> ToDto(this AppUser user,ITokenService tokenService)
    {
         return new UserDto
        {
            Id = user.Id,
            Email = user.Email!,
            DisplayName = user.DisplayName,
            TenantId = user.TenantId.ToString(),
            TenantName = user.Tenant.Name,
            Token = await tokenService.CreateToken(user)
        };
    }

}

// Maps to 409 — used when a concurrent update is detected (xmin mismatch)
public class ConflictException(string message) : Exception(message);

