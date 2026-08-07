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
using API.Services.Reports;
using API.Services.Storage;



var builder = WebApplication.CreateBuilder(args);
//============================================================= 
// Add services to the container.
//=============================================================
builder.Services.AddControllers();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection δεν έχει οριστεί.");

// Κοινές ρυθμίσεις Npgsql για τα δύο DbContext.
//  • EnableRetryOnFailure: η βάση σε serverless πάροχο (Neon) αναστέλλεται όταν
//    είναι αδρανής. Χωρίς retry, το πρώτο αίτημα μετά την αφύπνιση αποτυγχάνει.
//  • CommandTimeout: αποτρέπει ένα κολλημένο ερώτημα να κρατά σύνδεση επ' αόριστον.
static void ConfigureNpgsql(Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.NpgsqlDbContextOptionsBuilder npg)
{
    npg.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorCodesToAdd: null);
    npg.CommandTimeout(30);
}

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(connectionString, ConfigureNpgsql);

    // ΜΟΝΟ σε development: το SensitiveDataLogging γράφει τιμές παραμέτρων στα
    // logs — δηλαδή κωδικούς κατά το login και προσωπικά δεδομένα.
    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();
    }
});

// ------------------------------------------------------------------------------
//    για να μπορει να χρησιμοποιήσει το AuditChannel και το AuditBackgroundService
//------------------------------------------------------------------------------
builder.Services.AddDbContext<AuditDbContext>(options =>
    options.UseNpgsql(connectionString, ConfigureNpgsql));

builder.Services.AddSingleton<AuditChannel>(); //  για να δημιουργηθεί ένα μόνο instance του AuditChannel για όλη την εφαρμογή
builder.Services.AddHostedService<AuditBackgroundService>(); // ξεκινά μαζί με το app και τρέχει στο background για να αποθηκεύει τα audit logs στη βάση δεδομένων
builder.Services.AddHostedService<LogRetentionService>(); // καθαρίζει περιοδικά παλιά audit/error logs (βλ. appsettings Logging:*RetentionDays)

// Ενημέρωση ληξιπρόθεσμων δόσεων/ληγμένων συμβολαίων: όχι πλέον background
// timer (χτυπούσε τη βάση κάθε 6 ώρες ό,τι κι αν γίνεται, εμποδίζοντας server
// και serverless βάση να κοιμηθούν σε αδράνεια). Τρέχει τώρα μέσα σε αίτημα,
// ανά tenant, με throttle — βλ. StatusRefreshMiddleware.
builder.Services.Configure<StatusRefreshSettings>(builder.Configuration.GetSection("StatusRefresh"));
builder.Services.AddSingleton<StatusRefreshThrottle>();
builder.Services.AddScoped<IStatusRefreshService, TenantStatusRefreshService>();
// ------------------------------------------------------------------------------


// για να μπορει να χρησιμοποιήσει το IHttpContextAccessor για να πάρει το tenantId από τα claims
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<ITenantProvider, TenantProvider>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IAssetService, AssetService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IContractService, ContractService>();
builder.Services.AddScoped<IInstallmentService, InstallmentService>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<AadeService>();

// ------------------------------------------------------------------------------
//  Αποθήκευση αρχείων (Cloudflare R2) — αντικαθιστά το Cloudinary.
//  Οι διαπιστευτήρια έρχονται από τη ρύθμιση "R2", τα όρια/ποιότητα από "Storage".
//  Και οι δύο ενότητες δένονται στο ίδιο StorageSettings (η δέσμευση επιλογών
//  του .NET τις εφαρμόζει διαδοχικά στο ίδιο instance).
// ------------------------------------------------------------------------------
builder.Services.Configure<StorageSettings>(builder.Configuration.GetSection("R2"));
builder.Services.Configure<StorageSettings>(builder.Configuration.GetSection("Storage"));
builder.Services.AddSingleton<IFileStorage, R2FileStorage>();
builder.Services.AddScoped<FileValidationService>();
builder.Services.AddScoped<ImageCompressor>();
builder.Services.AddScoped<IAttachmentService, AttachmentService>();

// ------------------------------------------------------------------------------
//  Αναφορές / εξαγωγή Excel
//  Τα όρια είναι ρυθμιζόμενα (appsettings → "Reports") ώστε να προσαρμόζονται στο
//  μηχάνημα χωρίς νέο deployment. Το ExportThrottle είναι singleton: το φράγμα
//  ταυτόχρονων εξαγωγών πρέπει να είναι καθολικό για όλη τη διεργασία.
// ------------------------------------------------------------------------------
builder.Services.Configure<ReportSettings>(builder.Configuration.GetSection("Reports"));
builder.Services.AddSingleton<ExportThrottle>();
builder.Services.AddScoped<IReportService, ReportService>();








// ------------------------------------------------------------------------------

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var tokenKey = builder.Configuration["TokenKey"]
            ?? throw new Exception("TokenKey not found in configuration - program.cs");
        var tokenIssuer = builder.Configuration["TokenIssuer"]
            ?? throw new Exception("TokenIssuer not found in configuration - program.cs");
        var tokenAudience = builder.Configuration["TokenAudience"]
            ?? throw new Exception("TokenAudience not found in configuration - program.cs");
       options.TokenValidationParameters = new TokenValidationParameters
       {
           ValidateIssuerSigningKey = true, // ελέγχει αν το token έχει υπογραφεί με το σωστό κλειδί
           IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(tokenKey)), // το κλειδί που χρησιμοποιείται για την υπογραφή του token
           ValidateIssuer = true, // απορρίπτει tokens που δεν εκδόθηκαν από αυτή την εφαρμογή (π.χ. από άλλο περιβάλλον με το ίδιο κλειδί)
           ValidIssuer = tokenIssuer,
           ValidateAudience = true, // απορρίπτει tokens που δεν προορίζονταν για αυτό το API
           ValidAudience = tokenAudience
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
// .AddPolicy("RequireFreePlan",  p => p.RequireClaim("PlanType", "0", "1", "2"))
// .AddPolicy("RequireBasicPlan", p => p.RequireClaim("PlanType", "1", "2"))
// .AddPolicy("RequireProPlan",   p => p.RequireClaim("PlanType", "2"))
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

var app = builder.Build();
//=============================================================
// Configure the HTTP request pipeline.
//=============================================================

//custom middleware για global error handling
app.UseMiddleware<ExceptionMiddleware>();


// Εκτός development: ανακατεύθυνση σε HTTPS και HSTS.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

// Οι επιτρεπόμενες προελεύσεις έρχονται από ρυθμίσεις (Cors:AllowedOrigins),
// ώστε το domain παραγωγής να μπαίνει χωρίς αλλαγή κώδικα. Τα localhost μένουν
// ως fallback μόνο για development.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();

if (allowedOrigins is null or { Length: 0 })
{
    if (!app.Environment.IsDevelopment())
        throw new InvalidOperationException(
            "Cors:AllowedOrigins δεν έχει οριστεί. Χωρίς αυτό καμία εφαρμογή-πελάτης δεν μπορεί να συνδεθεί.");

    allowedOrigins = ["http://localhost:4200", "https://localhost:4200",
                      "http://localhost:4300", "https://localhost:4300"];
}

app.UseCors(policy => policy.AllowAnyHeader()
                            .AllowAnyMethod()
                            .AllowCredentials() // απαιτεί ρητές προελεύσεις — δεν επιτρέπεται "*"
                            // Χωρίς αυτό ο browser κρύβει το Content-Disposition από τη JavaScript,
                            // οπότε η λήψη αναφορών δεν μπορεί να διαβάσει το όνομα αρχείου του server.
                            .WithExposedHeaders("Content-Disposition")
                            .WithOrigins(allowedOrigins));

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<StatusRefreshMiddleware>();
app.MapControllers();

// Ελαφρύ σημείο ελέγχου ζωής — χρήσιμο για να ξυπνά η εφαρμογή μετά από αδράνεια
// και για να επιβεβαιώνεται ότι απαντά χωρίς να αγγίζεται η βάση.
app.MapGet("/health", () => Results.Ok(new { status = "ok", utc = DateTime.UtcNow }))
   .AllowAnonymous();

//====================== Migrations & seed data =======================//
// Τα migrations εφαρμόζονται πάντα· τα δοκιμαστικά δεδομένα ΠΟΤΕ εκτός development.
// Το DbInitializer παράγει ~2.000 πλαστές εγγραφές και λογαριασμούς με γνωστό
// κωδικό — σε παραγωγική βάση θα ήταν και ρύπανση δεδομένων και κενό ασφαλείας.
using var scope = app.Services.CreateScope();
var services = scope.ServiceProvider;
var startupLogger = services.GetRequiredService<ILogger<Program>>();

try
{
    var context = services.GetRequiredService<AppDbContext>();

    await context.Database.MigrateAsync();

    if (app.Environment.IsDevelopment())
    {
        var userManager = services.GetRequiredService<UserManager<AppUser>>();
        var tenantProvider = services.GetRequiredService<ITenantProvider>();
        await DbInitializer.InitializeAsync(context, userManager, tenantProvider, isDevelopment: true);
    }
    else
    {
        startupLogger.LogInformation("Production: migrations applied, seeding skipped.");
    }
}
catch (Exception ex)
{
    startupLogger.LogError(ex, "Database migration/seed failed.");

    // Σε development συνεχίζουμε για να μη μπλοκάρει η δουλειά. Σε παραγωγή όχι:
    // η εκκίνηση πάνω σε μισο-μεταναστευμένη βάση παράγει σφάλματα που μοιάζουν
    // με bug εφαρμογής και είναι πολύ δυσκολότερα στη διάγνωση.
    if (!app.Environment.IsDevelopment()) throw;
}

app.Run();
