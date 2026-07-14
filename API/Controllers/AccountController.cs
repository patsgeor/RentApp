using System.Text;
using API.Data.Repositories;
using API.DTOs;
using API.DTOs.User;
using API.Entities;
using API.Extensions;
using API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace API.Controllers
{
    
    public class AccountController(IUnitOfWork uow,
                UserManager<AppUser> userManager,
                ITokenService tokenService,
                ITenantProvider tenantProvider,
                IEmailService emailService,
                IConfiguration config) : BaseApiController
    {
        [HttpPost]
        public async Task<ActionResult<UserDto>> Register(TenantRegisterDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var user = await uow.MemberRepository.AddTenantAsync(dto);
               
                await SetRefreshTokenCookie(user);  

                return await user.ToDto(tokenService);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("create", ex.Message);
                return BadRequest(ModelState);
            }
        }

        [HttpPost("login")]//api/account/login
        public async Task<ActionResult<UserDto>> Login(LoginDto loginDto)
        {
             var member = await uow.MemberRepository.GetMemberByEmailAsync(loginDto.Email);
    
            if (member == null) return Unauthorized("Invalid email");//error 401 δηλαδη χωρις εξουσιοδότηση
           
            tenantProvider.SetCurrentTenant(member.User.TenantId);

            var user = await userManager.Users
                        .Include(u => u.Member) // Φορτώνει τα στοιχεία του Member
                        .Include(u => u.Tenant)
                        .SingleAsync(u => u.Id == member.Id);

            var result= await userManager.CheckPasswordAsync(user,loginDto.Password);
            
            if(!result)   return Unauthorized("Invalid password");   

            await SetRefreshTokenCookie(user);

            return await user.ToDto(tokenService);
        }


        //========================================================================
        //          INVITE NEW MEMBER FROM TENANT
        //========================================================================

        [Authorize(Policy ="RequireAdminRole")]
        [HttpPost("invite")]
        public async Task<IActionResult> Invite(MemberInviteDto dto)
        {
            var tenantId = User.GetTenantId();
            await uow.MemberRepository.InviteMemberAsync(dto, tenantId, User.GetMemberId().ToString(),
                cc: new List<string> { User.GetEmail() });

            return Ok(new { message = "Invite sent" });
        }


        [HttpGet("invite")]
        public async Task<IActionResult> GetInvite([FromQuery] string token)
        {
            var data = await uow.MemberRepository.GetInviteInfoAsync(token);

            if (data == null)
                return NotFound();

            return Ok(data);
        }


        [HttpPost("MemberRegister")]
        public async Task<ActionResult<UserDto>> Register(MemberRegisterFromInviteDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            try
            {
                var user = await uow.MemberRepository.RegisterFromInviteAsync(dto);

                await SetRefreshTokenCookie(user);  

                return await user.ToDto(tokenService);
             }
            catch (Exception ex)
            {
                ModelState.AddModelError("create", ex.Message);
                return BadRequest(ModelState);
            }
        }


        //========================================================================

        [HttpPost("refresh-token")]//api/account/refresh-token
        public async Task<ActionResult<UserDto>> RefreshToken()
        {
            var refreshToken = Request.Cookies["refreshToken"]; // ανάκτηση του refresh token από το cookie
            if (string.IsNullOrEmpty(refreshToken)) return NoContent();

            var user = await userManager.Users.FirstOrDefaultAsync(u => u.RefreshToken == refreshToken 
                                                                && u.RefreshTokenExpiry > DateTime.UtcNow);

            if (user == null)
            {
                return Unauthorized("Invalid or expired refresh token");
            }

            await SetRefreshTokenCookie(user); // δημιουργία και αποθήκευση νέου refresh token στο cookie

            return await user.ToDto(tokenService);
        }
        
         private async Task SetRefreshTokenCookie(AppUser appUser)
        {
            //1 δημιουργία του refresh token
            var refreshToken= tokenService.GenerateRefreshToken(); 

            //2 το αποθηκεύουμε στη βάση δεδομένων
            appUser.RefreshToken=refreshToken; 
            appUser.RefreshTokenExpiry=DateTime.UtcNow.AddDays(7);
            await userManager.UpdateAsync(appUser);

            //3 ρυθμίζουμε το cookie για να αποθηκεύσουμε το refresh token στο browser του χρήστη
            var cookieOptions = new CookieOptions
            {
                HttpOnly=true,// για να μην μπορεί να προσπελαστεί από JavaScript (π.χ. σε περίπτωση XSS επίθεσης)
                Secure= true,// για να αποστέλλεται μόνο μέσω HTTPS
                SameSite = SameSiteMode.Strict,// για να μην αποστέλλεται σε αιτήσεις από άλλες ιστοσελίδες (π.χ. σε περίπτωση CSRF επίθεσης)
                Expires= DateTime.UtcNow.AddDays(7)  // για να λήγει μετά από 7 ημέρες (πρέπει να είναι το ίδιο με το expiry του refresh token στη βάση δεδομένων)          
            };

            //4 αποθήκευση του refresh token στο cookie
            Response.Cookies.Append("refreshToken",refreshToken,cookieOptions); 
        }  
    

//========================================================================
//          FORGOT PASSWORD & RESET PASSWORD
//========================================================================
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordDto dto)
        {
            // Πάντα Ok — δεν αποκαλύπτουμε αν υπάρχει το email
            var user = await userManager.Users
                        .IgnoreQueryFilters()
                        .SingleOrDefaultAsync(u => u.NormalizedEmail == dto.Email.ToUpper());
            if (user is null || !user.IsActive)
                return Ok(new { message = "Αν το email υπάρχει, θα λάβεις οδηγίες." });

            var token     = await userManager.GeneratePasswordResetTokenAsync(user);// Δημιουργία token επαναφοράς κωδικού
            // var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token)); // Κωδικοποίηση του token για χρήση σε URL
            var frontUrl  = config["Frontend:BaseUrl"] ?? "http://localhost:4200";
            var resetLink = $"{frontUrl}/reset-password?email={Uri.EscapeDataString(dto.Email)}&token={Uri.EscapeDataString(token)}";
            var body = $"""
                <p>Γεια σου <strong>{user.DisplayName}</strong>,</p>
                <p>Λάβαμε αίτημα επαναφοράς κωδικού για τον λογαριασμό σου.</p>
                <p><a href="{resetLink}" style="padding:10px 20px;background:#4f46e5;color:#fff;border-radius:6px;text-decoration:none;">
                Επαναφορά Κωδικού
                </a></p>
                <p>Ο σύνδεσμος ισχύει για <strong>24 ώρες</strong>.<br>
                Αν δεν ζήτησες επαναφορά, αγνόησε αυτό το email.</p>
                """;

            await emailService.SendEmailAsync(dto.Email, "Επαναφορά Κωδικού", body, isHtml: true);

            return Ok(new { message = "Αν το email υπάρχει, θα λάβεις οδηγίες." });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword(ResetPasswordDto dto)
        {
            var user = await userManager.Users
                .IgnoreQueryFilters()
                .SingleOrDefaultAsync(u => u.NormalizedEmail == dto.Email.ToUpper());            if (user is null)
                return BadRequest(new { message = "Μη έγκυρο αίτημα." });

            tenantProvider.SetCurrentTenant(user.TenantId);

            // var decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(dto.Token)); 
            var result = await userManager.ResetPasswordAsync(user, dto.Token, dto.NewPassword);
            if (!result.Succeeded)
            {
                return BadRequest(new { errors = result.Errors.Select(e => e.Description).ToList() });
            }

            return Ok(new { message = "Ο κωδικός άλλαξε επιτυχώς." });
        }

        [Authorize]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword(ChangePasswordDto dto)
        {
            var user = await userManager.FindByIdAsync(User.GetMemberId().ToString());
            if (user is null) return Unauthorized();

            var result = await userManager.ChangePasswordAsync(user, dto.CurrentPassword, dto.NewPassword);
            if (!result.Succeeded)
            {
                return BadRequest(new { errors = result.Errors.Select(e => e.Description).ToList() });
            }

            return Ok(new { message = "Ο κωδικός άλλαξε επιτυχώς." });
        }
    }
}
