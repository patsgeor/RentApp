
using API.DTOs;
using API.DTOs.User;
using API.Entities;
using API.Errors;
using API.Extensions;
using API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using static API.Entities.Enums;

namespace API.Controllers
{
    
    public class AccountController(IUnitOfWork uow,
                UserManager<AppUser> userManager,
                ITokenService tokenService,
                ITenantProvider tenantProvider,
                IEmailService emailService,
                IConfiguration config,
                IPlanGate planGate) : BaseApiController
    {
        [EnableRateLimiting("auth")]
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

        [EnableRateLimiting("auth")]
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

            // Ο έλεγχος γίνεται ΜΕΤΑ τον κωδικό ώστε το μήνυμα να μην αποκαλύπτει
            // σε τρίτον ότι ο λογαριασμός υπάρχει αλλά είναι απενεργοποιημένος.
            if (!user.IsActive )
                return Unauthorized("Ο λογαριασμός σας έχει απενεργοποιηθεί. Επικοινωνήστε με τον διαχειριστή της εταιρείας σας.");

            // Το IsActive αφορά τον χρήστη μέσα στον tenant (το ελέγχει ο Admin του).
            // Το SubscriptionStatus αφορά ολόκληρη την εταιρεία (το ελέγχει ο SuperAdmin) —
            // χωρίς αυτό, ένας tenant που ανεστάλη μπορούσε να συνεχίσει να κάνει login κανονικά.
            if (user.Tenant.SubscriptionStatus != SubscriptionStatus.Active)
                return Unauthorized("Ο λογαριασμός της εταιρείας σας δεν είναι ενεργός. Επικοινωνήστε μαζί μας.");

            await SetRefreshTokenCookie(user);

            return await user.ToDto(tokenService);
        }


        //========================================================================
        //          INVITE NEW MEMBER FROM TENANT
        //========================================================================

        //========================================================================
        //          MEMBER MANAGEMENT (tenant admin)
        //========================================================================

        // Το μενού «Διαχείριση → Χρήστες» εμφανίζεται ήδη μόνο σε admin, αλλά ο
        // έλεγχος επιβάλλεται και εδώ: το UI δεν αποτελεί μηχανισμό ασφαλείας.
        [Authorize(Policy = "RequireAdminRole")]
        [HttpGet("members")]
        public async Task<ActionResult<IReadOnlyList<TenantMemberDto>>> GetMembers()
        {
            return Ok(await uow.MemberRepository.GetTenantMembersAsync());
        }

        [Authorize(Policy = "RequireAdminRole")]
        [HttpPatch("members/{id}/active")]
        public async Task<IActionResult> SetMemberActive(string id, SetMemberActiveDto dto)
        {
            // Αυτοαπενεργοποίηση θα κλείδωνε τον διαχειριστή έξω από την εταιρεία
            // του, χωρίς κανέναν τρόπο επαναφοράς από την εφαρμογή.
            if (id == User.GetMemberId().ToString() && !dto.IsActive)
                return BadRequest("Δεν μπορείτε να απενεργοποιήσετε τον δικό σας λογαριασμό.");

            var ok = await uow.MemberRepository.SetMemberActiveAsync(id, dto.IsActive);

            if (!ok) return NotFound("Ο χρήστης δεν βρέθηκε.");

            return Ok(new { message = dto.IsActive ? "Ο χρήστης ενεργοποιήθηκε." : "Ο χρήστης απενεργοποιήθηκε." });
        }


        [Authorize(Policy ="RequireAdminRole")]
        [HttpPost("invite")]
        public async Task<IActionResult> Invite(MemberInviteDto dto)
        {
            try
            {
                await planGate.EnsureAtLeastAsync(PlanType.Basic);

                var tenantId = User.GetTenantId();
                await uow.MemberRepository.InviteMemberAsync(dto, tenantId, User.GetMemberId().ToString(),
                    cc: new List<string> { User.GetEmail() });

                return Ok(new { message = "Invite sent" });
            }
            catch (PlanRestrictionException ex)
            {
                return StatusCode(403, new { message = ex.Message, upgradeRequired = true, requiredPlan = ex.RequiredPlan.ToString() });
            }
        }


        [HttpGet("invite")]
        public async Task<IActionResult> GetInvite([FromQuery] string token)
        {
            var data = await uow.MemberRepository.GetInviteInfoAsync(token);

            if (data == null)
                return NotFound();

            return Ok(data);
        }


        [EnableRateLimiting("auth")]
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

            // IgnoreQueryFilters: όπως ForgotPassword/ResetPassword παραπάνω, αυτό το
            // request είναι ανώνυμο (μόνο refresh cookie, χωρίς JWT) — δεν υπάρχει
            // ambient tenant ώστε να εφαρμοστεί το global query filter του AppUser.
            // Include(Tenant): χωρίς αυτό, το TokenService.CreateToken (user.Tenant.Name/
            // PlanType) πετούσε NullReferenceException σε κάθε refresh — το Tenant
            // είναι μη-nullable navigation και δεν υπάρχει lazy loading σε αυτό το project.
            var user = await userManager.Users
                .IgnoreQueryFilters()
                .Include(u => u.Tenant)
                .FirstOrDefaultAsync(u => u.RefreshToken == refreshToken
                                       && u.RefreshTokenExpiry > DateTime.UtcNow);

            if (user == null)
            {
                return Unauthorized("Invalid or expired refresh token");
            }

            tenantProvider.SetCurrentTenant(user.TenantId);

            // Χωρίς αυτόν τον έλεγχο, ένας απενεργοποιημένος χρήστης θα ανανέωνε
            // επ' αόριστον τη συνεδρία του και η απενεργοποίηση δεν θα είχε ισχύ.
            if (!user.IsActive)
                return Unauthorized("Ο λογαριασμός σας έχει απενεργοποιηθεί.");

            // Ίδιος λόγος με το Login: αν ο SuperAdmin αναστείλει τον tenant ΜΕΤΑ
            // το login, χωρίς αυτόν τον έλεγχο η υπάρχουσα συνεδρία θα συνέχιζε να
            // ανανεώνεται επ' αόριστον μέσω refresh-token, αγνοώντας την αναστολή.
            if (user.Tenant.SubscriptionStatus != SubscriptionStatus.Active)
                return Unauthorized("Ο λογαριασμός της εταιρείας σας δεν είναι ενεργός.");

            await SetRefreshTokenCookie(user); // δημιουργία και αποθήκευση νέου refresh token στο cookie

            return await user.ToDto(tokenService);
        }

        /// <summary>
        /// Τερματισμός συνεδρίας: ακυρώνει το refresh token στη βάση και διαγράφει το
        /// cookie. Χωρίς αυτό, η «αποσύνδεση» καθάριζε μόνο τον browser και το token
        /// παρέμενε έγκυρο στη βάση — σε κοινόχρηστο υπολογιστή, πραγματικό κενό.
        /// Είναι ανώνυμο επίτηδες: καλείται και όταν το access token έχει ήδη λήξει.
        /// </summary>
        [AllowAnonymous]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            var refreshToken = Request.Cookies["refreshToken"];

            if (!string.IsNullOrEmpty(refreshToken))
            {
                // IgnoreQueryFilters: το αίτημα είναι ανώνυμο, άρα δεν υπάρχει ενεργός
                // tenant για να εφαρμοστεί το global query filter του AppUser.
                var user = await userManager.Users
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(u => u.RefreshToken == refreshToken);

                if (user is not null)
                {
                    // Όπως και στο RefreshToken: η αποθήκευση περνά από το AppDbContext,
                    // που απαιτεί ενεργό tenant για τα global query filters και το audit.
                    tenantProvider.SetCurrentTenant(user.TenantId);

                    user.RefreshToken = null;
                    user.RefreshTokenExpiry = null;
                    await userManager.UpdateAsync(user);
                }
            }

            Response.Cookies.Delete("refreshToken", RefreshCookieOptions());

            return Ok(new { message = "Αποσυνδεθήκατε." });
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
            Response.Cookies.Append("refreshToken", refreshToken, RefreshCookieOptions());
        }

        /// <summary>
        /// Οι ρυθμίσεις του cookie ανανέωσης. Χρησιμοποιούνται και στη δημιουργία και
        /// στη διαγραφή του: ο browser σβήνει ένα cookie μόνο αν τα χαρακτηριστικά
        /// ταιριάζουν με εκείνα που είχε όταν δημιουργήθηκε.
        /// </summary>
        private static CookieOptions RefreshCookieOptions() => new()
        {
            // Δεν διαβάζεται από JavaScript — περιορίζει τη ζημιά μιας επίθεσης XSS.
            HttpOnly = true,

            // Αποστέλλεται μόνο μέσω HTTPS. Υποχρεωτικό όταν SameSite=None.
            Secure = true,

            // SameSite=None και όχι Strict: σε παραγωγή το frontend (Cloudflare Pages)
            // και το API (Azure App Service) βρίσκονται σε διαφορετικά sites, οπότε
            // ένα Strict cookie δεν θα στελνόταν ΠΟΤΕ από τον browser και η σιωπηλή
            // ανανέωση θα ήταν αδύνατη. Η προστασία έναντι CSRF δεν χάνεται: το cookie
            // από μόνο του δεν αυθεντικοποιεί κανένα αίτημα δεδομένων — τα endpoints
            // απαιτούν JWT στην κεφαλίδα Authorization — και η πολιτική CORS δέχεται
            // διαπιστευτήρια μόνο από τις ρητά δηλωμένες προελεύσεις.
            SameSite = SameSiteMode.None,

            // Χωρίς Expires: cookie συνεδρίας. Όταν κλείσει ο browser, χάνεται — άρα
            // το κλείσιμο του παραθύρου τερματίζει στην πράξη τη συνεδρία, ακόμη και
            // αν ο χρήστης δεν πατήσει «Αποσύνδεση». Η εγγραφή στη βάση διατηρεί τη
            // δική της λήξη (7 ημέρες) ως ανώτατο όριο.
            Path = "/"
        };  
    

//========================================================================
//          FORGOT PASSWORD & RESET PASSWORD
//========================================================================
        [EnableRateLimiting("auth")]
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
            // Χωρίς fallback σε localhost: ένα σιωπηλά λάθος domain στέλνει σε κάθε
            // χρήστη σύνδεσμο που δεν ανοίγει, χωρίς κανένα σφάλμα να το προδίδει.
            var frontUrl  = config["Frontend:BaseUrl"]
                ?? throw new InvalidOperationException("Frontend:BaseUrl δεν έχει οριστεί.");
            var resetLink = $"{frontUrl.TrimEnd('/')}/reset-password?email={Uri.EscapeDataString(dto.Email)}&token={Uri.EscapeDataString(token)}";
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

        [EnableRateLimiting("auth")]
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
