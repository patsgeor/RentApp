using System;
using API.Data.Contexts;
using API.DTOs;
using API.Entities;
using API.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;

public class MemberRepository (
                AppDbContext context,
                UserManager<AppUser> userManager,
                IEmailService emailService,
                ITenantProvider tenantProvider) : IMemberRepository
{
    
    //==============================================================================
    //     ADD NEW TENANT + admin for this tenant
    //==============================================================================
    public async Task<AppUser> AddTenantAsync(TenantRegisterDto dto)
    {
        using var transaction = await context.Database.BeginTransactionAsync();

        try
        {
            Tenant tenant= new Tenant
            {
                Name= dto.CompanyName ,
                VatNumber=dto.VatNumber,
                ContactInfo= dto.ContactInfo
            };

            await context.Tenants.AddAsync(tenant);
            await context.SaveChangesAsync();

            tenantProvider.SetCurrentTenant(tenant.Id);

            AppUser user = new AppUser
            {
                UserName = dto.Email,
                Email = dto.Email,
                DisplayName = dto.DisplayName,
                IsActive = true,
                TenantId=tenant.Id,
                Member = new Member
                    {
                        FirstName = dto.FirstName,
                        LastName = dto.LastName,
                    }
            };

            var result = await userManager.CreateAsync(user, dto.Password);

            if(!result.Succeeded)
            {
                var errors =result.Errors.Select(e => e.Description);
                throw new Exception(string.Join(" , ",errors));
            }

            await userManager.AddToRoleAsync(user,"Admin");
            
            // 3. Commit
            await transaction.CommitAsync();
            return user;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            throw ex;
        }

    }

    //==============================================================================
    //      INVITE MEMBER FROM TENANT
    //==============================================================================

    // για να εγγραφή ένα νεο μέλος πρέπει πρώτα να το προσκαλέσουμε με ένα invite link που θα στείλουμε στο email του
    //αυτο το κάνει ο admin του tenant. Ο admin στέλνει ένα invite link στο email του νέου μέλους και το νέο μέλος εγγράφεται με αυτό το link.
    // -- InviteMemberAsync 
        // στέλνει το invite link στο email του νέου μέλους και αποθηκεύει το invite στο database.
        //  Το invite περιέχει ένα token που θα χρησιμοποιηθεί για να εγγραφεί το νέο μέλος.
    // -- GetInviteInfoAsync
        // παίρνει το invite token και επιστρέφει τις πληροφορίες του invite (email, first name, last name, tenant name)
    // -- RegisterFromInviteAsync
        // παίρνει το invite token και τα στοιχεία του νέου μέλους (display name, password) και δημιουργεί το νέο μέλος στο database. 
        // Το invite token γίνεται used και δεν μπορεί να χρησιμοποιηθεί ξανά.
    public async Task InviteMemberAsync(MemberInviteDto dto, Guid tenantId, string InvitedBy, IEnumerable<string>? cc = null)
    {
        var invite = new MemberInvite
        {
            Email = dto.Email,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Role = dto.Role,
            TenantId = tenantId,
            CreatedBy = InvitedBy,
            Token = Guid.NewGuid().ToString("N"),
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        await context.MemberInvites.AddAsync(invite);
        await context.SaveChangesAsync();

        var registerLink = $"https://localhost:4200/register-invite?token={invite.Token}";


        await emailService.SendEmailAsync(
            dto.Email,
            "Πρόσκληση εγγραφής",
            $"Γεια σας {dto.FirstName},\n\nΈχετε προσκληθεί να εγγραφείτε στην πλατφόρμα.\n\n" +
            $"Κάντε κλικ στον παρακάτω σύνδεσμο για να ολοκληρώσετε την εγγραφή σας:\n\n" +
            $"{registerLink}\n\n" +
            $"Το link λήγει σε 7 ημέρες.", isHtml: true, cc: cc);

    }

    public async Task<MemberInviteInfoDto?> GetInviteInfoAsync(string token)
    {
        var invite = await context.MemberInvites
            .Include(x => x.Tenant)
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Token == token);

        if (invite == null || invite.IsUsed || invite.ExpiresAt < DateTime.UtcNow)
            throw new Exception("Invalid invite");

        return new MemberInviteInfoDto
        {
            Email = invite.Email,
            FirstName = invite.FirstName,
            LastName = invite.LastName,
            TenantName = invite.Tenant.Name
        };
    }

    public async Task<AppUser> RegisterFromInviteAsync(MemberRegisterFromInviteDto dto)
    {
        var invite = await context.MemberInvites
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Token == dto.Token);

        if (invite == null)
            throw new Exception("Invalid invite");

        if (invite.IsUsed)
            throw new Exception("Invite already used");

        if (invite.ExpiresAt < DateTime.UtcNow)
            throw new Exception("Invite expired");
        
        tenantProvider.SetCurrentTenant(invite.TenantId);

        var user = new AppUser
        {
            UserName = invite.Email,
            Email = invite.Email,
            DisplayName = dto.DisplayName,
            TenantId = invite.TenantId,
            IsActive = true,
            Member = new Member
            {
                FirstName = invite.FirstName,
                LastName = invite.LastName
            }
        };

        var result = await userManager.CreateAsync(user, dto.Password);

        if (!result.Succeeded)
            throw new Exception(string.Join(", ", result.Errors.Select(x => x.Description)));

        await userManager.AddToRoleAsync(user, invite.Role);

        invite.IsUsed = true;

        await context.SaveChangesAsync();

        return await userManager.Users
            .Include(u => u.Tenant)
            .Include(u => u.Member)
            .FirstAsync(u => u.Id == user.Id);
    }



    //-------------------------------------------------------------------------------------------------

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
   
   

    public async Task<IReadOnlyList<Member>> GetAllAsync()
    {
        return await context.Members.Include(m => m.User).IgnoreQueryFilters().ToListAsync();
    }

    public async Task<Member?> GetMemberByEmailAsync(string email)
    {
        return await context.Members.Include(m => m.User)
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(m => m.User.Email == email);
    }
}
