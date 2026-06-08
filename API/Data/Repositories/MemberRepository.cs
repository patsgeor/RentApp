using System;
using API.Data.Contexts;
using API.DTOs;
using API.Entities;
using API.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;

public class MemberRepositor (AppDbContext context,UserManager<AppUser> userManager) : IMemberRepository
{
    public async Task AddAsync(MemberRegisterDto dto)
    {
        AppUser user = new AppUser
        {
            UserName = dto.Email,
            Email = dto.Email,
            DisplayName = dto.DisplayName,
            IsActive = true
        };

        var result = await userManager.CreateAsync(user, dto.Password);

        if(!result.Succeeded)
        {
            var errors =result.Errors.Select(e => e.Description);
            throw new Exception(string.Join(" , ",errors));
        }

        Member member= new Member
        {
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Afm = dto.Afm,
            Amka = dto.Amka,
            Id = user.Id
        };

        await context.Members.AddAsync(member);
    }

    public async Task<IReadOnlyList<MemberListDto>> GetAllAsync()
    {
        var members = await context.Members
                            .Select(m => new MemberListDto
                                {
                                    Id=m.Id,
                                    FullName= m.LastName+" "+m.FirstName,
                                    Afm=m.Afm,
                                    Email=m.User.Email!,
                                    DisplayName=m.User.DisplayName,
                                    TentalName = m.User.Tenant.Name,
                                    TentalId = m.User.TenantId,
                                    LastActive=m.LastActive,
                                    IsLockout = m.User.LockoutEnd != null && m.User.LockoutEnd > DateTime.UtcNow
                                }
                            ).ToListAsync();
        return members;
    }

    public async Task<Member?> GetMemberByIdAsync(string id)
    {
        return await context.Members.FindAsync(id);
    }

    public async Task SoftDelete(Member member)
    {
        await context.Users.Where(u => u.Id==member.Id && u.IsActive==true).ExecuteUpdateAsync(set => set.SetProperty(m =>m.IsActive, false));
    }

    public void Update(Member member)
    {
        context.Entry(member).State = EntityState.Modified;
    }
   
    public async Task<bool> SaveAllAsyng()
    {
        return await context.SaveChangesAsync()>0;
    }
}
