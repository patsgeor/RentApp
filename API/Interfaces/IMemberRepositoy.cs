using System;
using API.DTOs;
using API.Entities;

namespace API.Interfaces;

public interface IMemberRepository
{
    Task<Member?> GetMemberByIdAsync (string id);
    Task<IReadOnlyList<MemberListDto>> GetAllAsync();
    Task AddAsync(MemberRegisterDto memberRegisterDto);
    Task SoftDelete(Member member);
    void Update(Member member);
    Task<bool> SaveAllAsyng();
}
