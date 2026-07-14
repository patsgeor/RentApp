using API.Data;
using API.Data.Contexts;
using API.Data.Repositories;
using API.Entities;
using API.Interfaces;
using API.Middleware;
using API.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using API.Helper;
using System.Threading.Channels;



var builder = WebApplication.CreateBuilder(args);
//============================================================= 
// Add services to the container.
//=============================================================
builder.Services.AddControllers();
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));

    options.EnableSensitiveDataLogging();
    options.EnableDetailedErrors();
});

// ------------------------------------------------------------------------------
//    για να μπορει να χρησιμοποιήσει το AuditChannel και το AuditBackgroundService
//------------------------------------------------------------------------------
builder.Services.AddDbContext<AuditDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSingleton<AuditChannel>(); //  για να δημιουργηθεί ένα μόνο instance του AuditChannel για όλη την εφαρμογή
builder.Services.AddHostedService<AuditBackgroundService>(); // ξεκινά μαζί με το app και τρέχει στο background για να αποθηκεύει τα audit logs στη βάση δεδομένων
// ------------------------------------------------------------------------------


// για να μπορει να χρησιμοποιήσει το IHttpContextAccessor για να πάρει το tenantId από τα claims
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<ITenantProvider, TenantProvider>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IAssetService, AssetService>();
builder.Services.AddScoped<IPhotoService, PhotoService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IContractService, ContractService>();
builder.Services.AddScoped<IInstallmentService, InstallmentService>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<AadeService>();








// ------------------------------------------------------------------------------

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var tokenKey = builder.Configuration["TokenKey"] 
            ?? throw new Exception("TokenKey not found in configuration - program.cs");
       options.TokenValidationParameters = new TokenValidationParameters
       {
           ValidateIssuerSigningKey = true, // ελέγχει αν το token έχει υπογραφεί με το σωστό κλειδί
           IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(tokenKey)), // το κλειδί που χρησιμοποιείται για την υπογραφή του token
           ValidateIssuer = false, // δεν ελέγχει τον εκδότη του token 
           ValidateAudience = false // δεν ελέγχει το κοινό του token 
        };
        
        // Αυτό το κομμάτι είναι για να επιτρέψουμε στο SignalR να λαμβάνει το JWT token από το Query String, 
        // επειδή το SignalR δεν μπορεί να στείλει headers όπως το Authorization header.
        options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context => 
                {
                    // 1. Λήψη του token από το Query String
                    var accessToken = context.Request.Query["access_token"];// bydefault, το SignalR client στέλνει το token με το query parameter "access_token"
                    // 2. Έλεγχος της διαδρομής (Path)
                    var path = context.HttpContext.Request.Path;
                    
                    if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    {
                        // 3. Χειροκίνητη απόδοση του token στο Context
                        context.Token = accessToken;
                    }
                    return Task.CompletedTask;
                }
            };
    });
// ------------------------------------------------------------------------------

builder.Services.AddAuthorizationBuilder()
.AddPolicy("RequireAdminRole", p => p.RequireRole("Admin"))
.AddPolicy("SuperAdminRole", p => p.RequireRole("SuperAdmin"));


//για να επιτρεψει αιτησεις απο angular
builder.Services.AddCors();


// ------------------------------------------------------------------------------
//  Identity configuration
builder.Services.AddIdentityCore<AppUser>(options =>
    {
        options.Password.RequiredLength = 6;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireDigit = true;
        options.Password.RequireNonAlphanumeric = false;// για να μην απαιτείται ειδικός χαρακτήρας στο password
        options.User.RequireUniqueEmail = true;// για να απαιτείται μοναδικό email
    })
    .AddRoles<IdentityRole>() // για να προσθέσουμε υποστήριξη για ρόλους (π.χ. admin, moderator, κτλ)
    .AddEntityFrameworkStores<AppDbContext>()// για να χρησιμοποιήσουμε το AppDbcontext για να αποθηκεύσουμε τα δεδομένα του Identity (users, roles, κτλ)
    .AddDefaultTokenProviders(); // για να προσθέσουμε υποστήριξη για token providers (π.χ. για reset password, email confirmation, κτλ)
// ------------------------------------------------------------------------------

builder.Services.Configure<CloudinarySettings>(builder.Configuration.GetSection("CloudinarySettings"));

var app = builder.Build();
//=============================================================
// Configure the HTTP request pipeline.
//=============================================================

//custom middleware για global error handling
app.UseMiddleware<ExceptionMiddleware>();


// Configure the HTTP request pipeline.
app.UseCors(policy => policy.AllowAnyHeader()  // για να επιτρέψει όλα τα headers που στέλνει το angular (π.χ. Authorization header με το jwt token)
                            .AllowAnyMethod() // για να επιτρέψει όλα τα headers και όλες τις μεθόδους (GET, POST, κτλ) από το angular
                            .AllowCredentials() // για να επιτρέψει την αποστολή cookies από το angular
                            .WithOrigins("http://localhost:4200",
                                        "https://localhost:4200"));//angular
                                        


app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

//============================== Seed data=============================//
using var scope = app.Services.CreateScope();
var services = scope.ServiceProvider;
try
{
    //1. για να δημιουργήσει τη βάση αν δεν υπάρχει
    var context = services.GetRequiredService<AppDbContext>();
    // 2. ΠΡΟΣΘΗΚΗ: Παίρνουμε το UserManager! 
    var userManager = services.GetRequiredService<UserManager<AppUser>>();
    var tenantProvider = services.GetRequiredService<ITenantProvider>();


    //3.  Για να εφαρμόσει τυχόν pending migrations σαν να τρέχαμε το "dotnet ef database update"
    await context.Database.MigrateAsync(); 

    //4. Καλούμε την InitializeAsync περνώντας ΚΑΙ ΤΑ ΔΥΟ ορίσματα
    await DbInitializer.InitializeAsync(context, userManager, tenantProvider);
    }
catch (Exception ex)
{
    var logger = services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "An error occurred seeding the database.");
}

app.Run();
