using System;
using API.DTOs;
using API.Entities;

namespace API.Interfaces;

public interface IMemberRepository
{
    Task<Member?> GetMemberByIdAsync (string id);
    Task<Member?> GetMemberByEmailAsync(string email);
    Task<IReadOnlyList<Member>> GetAllAsync();
    Task<IReadOnlyList<TenantMemberDto>> GetTenantMembersAsync();
    Task<bool> SetMemberActiveAsync(string userId, bool isActive);

    Task<AppUser> AddTenantAsync(TenantRegisterDto tenantRegisterDto);
    
    Task InviteMemberAsync(MemberInviteDto dto, Guid tenantId, string InvitedBy, IEnumerable<string>? cc = null);
    Task<MemberInviteInfoDto?> GetInviteInfoAsync(string token);
    Task<AppUser> RegisterFromInviteAsync(MemberRegisterFromInviteDto dto);


    Task SoftDelete(Member member);
    void Update(Member member);

}
