using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using API.Entities;
using API.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace API.Services;

public class TokenService (IConfiguration config,UserManager<AppUser> userManager): ITokenService
{
    public async Task<string> CreateToken(AppUser user)
    {
        // Ανάκτηση του TokenKey από το configuration
        var tokenKey = config["TokenKey"] ?? throw new Exception("Cannot find TokenKey in configuration.");

        // Ελεγχος μήκους του TokenKey για ασφάλεια
        if (tokenKey.Length < 64)
        {
            throw new Exception("TokenKey must be at least 64 characters long for security reasons.");
        }

        // Create symmetric security key
        var _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(tokenKey));

        var roles = await userManager.GetRolesAsync(user);

        // δημιουργία claims για το χρήστη 
        var claims = new List<Claim>
        {
            new Claim (ClaimTypes.Email, user.Email!),
            new Claim (ClaimTypes.NameIdentifier, user.Id),
            new Claim ("TenantName", user.Tenant.Name),
            new Claim("TenantId",user.TenantId.ToString()),
            new Claim("PlanType", ((int)user.Tenant.PlanType).ToString()),
            new Claim("PlanExpiresAt", user.Tenant.PlanExpiresAt?.ToString("O") ?? "")
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        // δημιουργία των credentials χρησιμοποιώντας το κλειδί και τον αλγόριθμο HMAC SHA512
        var creds = new SigningCredentials(_key, SecurityAlgorithms.HmacSha512Signature);

        // περιγραφή του token
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            NotBefore = DateTime.UtcNow,
            Expires = DateTime.Now.AddDays(7),
            SigningCredentials = creds
        };

        // δημιουργία του token
        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(randomBytes);
    }
}
