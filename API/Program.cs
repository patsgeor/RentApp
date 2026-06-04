using API.Data;
using API.Data.Contexts;
using API.Data.Repositories;
using API.Entities;
using API.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
//============================================================= 
// Add services to the container.
//=============================================================
builder.Services.AddControllers();
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"));
});

builder.Services.AddScoped<IRentRepository,RentRepository>();

//για να επιτρεψει αιτησεις απο angular
builder.Services.AddCors();


// ------------------------------------------------------------------------------
//  Identity configuration
builder.Services.AddIdentityCore<AppUser>(options =>
{
    options.Password.RequireNonAlphanumeric = false;// για να μην απαιτείται ειδικός χαρακτήρας στο password
    options.User.RequireUniqueEmail = true;// για να απαιτείται μοναδικό email
})
.AddRoles<IdentityRole>() // για να προσθέσουμε υποστήριξη για ρόλους (π.χ. admin, moderator, κτλ)
.AddEntityFrameworkStores<AppDbContext>();// για να χρησιμοποιήσουμε το AppDbcontext για να αποθηκεύσουμε τα δεδομένα του Identity (users, roles, κτλ)
// ------------------------------------------------------------------------------


var app = builder.Build();

//=============================================================
// Configure the HTTP request pipeline.
//=============================================================

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Configure the HTTP request pipeline.
app.UseCors(policy => policy.AllowAnyHeader()  // για να επιτρέψει όλα τα headers που στέλνει το angular (π.χ. Authorization header με το jwt token)
                            .AllowAnyMethod() // για να επιτρέψει όλα τα headers και όλες τις μεθόδους (GET, POST, κτλ) από το angular
                            .AllowCredentials() // για να επιτρέψει την αποστολή cookies από το angular
                            .WithOrigins("http://localhost:4200",
                                        "https://localhost:4200"));//angular
                                        

// Seed the database
using var scope = app.Services.CreateScope();
var services = scope.ServiceProvider;
try
{
    //1. για να δημιουργήσει τη βάση αν δεν υπάρχει
    var context = services.GetRequiredService<AppDbContext>();
    // 2. ΠΡΟΣΘΗΚΗ: Παίρνουμε το UserManager! 
    var userManager = services.GetRequiredService<UserManager<AppUser>>();

    //3.  Για να εφαρμόσει τυχόν pending migrations σαν να τρέχαμε το "dotnet ef database update"
    await context.Database.MigrateAsync(); 

    //4. Καλούμε την InitializeAsync περνώντας ΚΑΙ ΤΑ ΔΥΟ ορίσματα
    await DbInitializer.InitializeAsync(context, userManager);}
catch (Exception ex)
{
    var logger = services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "An error occurred seeding the database.");
}


app.MapControllers();

app.Run();
