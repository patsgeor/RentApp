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
                ITenantProvider tenantProvider,
                IConfiguration config) : IMemberRepository
{
    
    //==============================================================================
    //     ADD NEW TENANT + admin for this tenant
    //==============================================================================
    public async Task<AppUser> AddTenantAsync(TenantRegisterDto dto)
    {
        // Το EnableRetryOnFailure (βλ. Program.cs — απαραίτητο για τη Neon, που
        // αναστέλλεται όταν είναι αδρανής) απαγορεύει χειροκίνητα transactions
        // εκτός CreateExecutionStrategy: χωρίς αυτό, ολόκληρη η εγγραφή πετούσε
        // πάντα "NpgsqlRetryingExecutionStrategy does not support user-initiated
        // transactions" — δεν ήταν θέμα ρύθμισης της βάσης, ήταν αυτό.
        var strategy = context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
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
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
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

        // Το domain έρχεται από ρυθμίσεις, όπως και στο forgot-password. Ήταν
        // καρφωμένο σε localhost, οπότε κάθε πρόσκληση από παραγωγή θα κατέληγε
        // σε σύνδεσμο που δεν ανοίγει.
        var frontUrl = config["Frontend:BaseUrl"]
            ?? throw new InvalidOperationException("Frontend:BaseUrl δεν έχει οριστεί.");

        var registerLink = $"{frontUrl.TrimEnd('/')}/register-invite?token={invite.Token}";


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

        // Επιστρέφει null αντί να πετάξει: ο controller το μεταφράζει σε 404 και το
        // frontend δείχνει «Η πρόσκληση δεν είναι έγκυρη ή έχει λήξει». Με exception
        // η απόκριση γινόταν 500 και ο χρήστης έβλεπε οθόνη «Server Error» — ένα
        // ληγμένο ή ήδη χρησιμοποιημένο link είναι αναμενόμενη κατάσταση, όχι σφάλμα.
        if (invite == null || invite.IsUsed || invite.ExpiresAt < DateTime.UtcNow)
            return null;

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
   
   

    // ΠΡΟΣΟΧΗ: επιστρέφει μέλη ΟΛΩΝ των ενοίκων (IgnoreQueryFilters). Προορίζεται
    // αποκλειστικά για cross-tenant χρήση από τον SuperAdmin — δεν πρέπει ποτέ να
    // εκτεθεί σε endpoint προσβάσιμο από διαχειριστή εταιρείας.
    public async Task<IReadOnlyList<Member>> GetAllAsync()
    {
        return await context.Members.Include(m => m.User).IgnoreQueryFilters().ToListAsync();
    }

    /// <summary>
    /// Τα μέλη του τρέχοντος ενοίκου, για τη λίστα χρήστων του διαχειριστή.
    /// Σκοπίμως ΧΩΡΙΣ IgnoreQueryFilters: ο AppUser υλοποιεί IMustHaveTenant, οπότε
    /// το global query filter περιορίζει αυτόματα στον ένοικο του τρέχοντος JWT.
    /// Στηριζόμαστε στον καθιερωμένο μηχανισμό αντί σε χειροκίνητο φίλτρο, ώστε η
    /// απομόνωση να μην εξαρτάται από το να θυμηθεί κανείς να τη γράψει.
    /// </summary>
    public async Task<IReadOnlyList<TenantMemberDto>> GetTenantMembersAsync()
    {
        var users = await context.Users
            .Include(u => u.Member)
            .OrderBy(u => u.DisplayName)
            .ToListAsync();

        var ids = users.Select(u => u.Id).ToList();

        var roles = await (from ur in context.UserRoles
                           join r in context.Roles on ur.RoleId equals r.Id
                           where ids.Contains(ur.UserId)
                           select new { ur.UserId, RoleName = r.Name! })
                          .ToListAsync();

        return users.Select(u => new TenantMemberDto
        {
            Id          = u.Id,
            FirstName   = u.Member?.FirstName ?? "",
            LastName    = u.Member?.LastName ?? "",
            DisplayName = u.DisplayName,
            Email       = u.Email ?? "",
            IsActive    = u.IsActive,
            Created     = u.Member?.Created,
            LastActive  = u.Member?.LastActive,
            Roles       = roles.Where(r => r.UserId == u.Id).Select(r => r.RoleName).ToList()
        }).ToList();
    }

    /// <summary>
    /// Ενεργοποιεί ή απενεργοποιεί μέλος του τρέχοντος ενοίκου.
    /// Χωρίς IgnoreQueryFilters: το global query filter εγγυάται ότι ένας
    /// διαχειριστής δεν μπορεί να αγγίξει χρήστη άλλης εταιρείας, ακόμη κι αν
    /// στείλει χειροκίνητα ξένο Id.
    /// </summary>
    public async Task<bool> SetMemberActiveAsync(string userId, bool isActive)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return false;

        user.IsActive = isActive;

        // Κατά την απενεργοποίηση ακυρώνεται και το refresh token, ώστε η
        // τρέχουσα συνεδρία να μην μπορεί να ανανεωθεί όταν λήξει.
        if (!isActive)
        {
            user.RefreshToken = null;
            user.RefreshTokenExpiry = null;
        }

        await context.SaveChangesAsync();
        return true;
    }

    public async Task<Member?> GetMemberByEmailAsync(string email)
    {
        return await context.Members.Include(m => m.User)
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(m => m.User.Email == email);
    }
}
