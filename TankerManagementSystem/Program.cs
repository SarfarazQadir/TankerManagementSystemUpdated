// Modified by AI
// Date: 2026-07-21
// Reason: Multiple fixes applied:
//   H-05 — Registered ILedgerRecalculationService as Scoped DI service.
//   H-01 — JWT Secret now read from environment variable "JWT_SECRET" first,
//           falling back to configuration (appsettings). This prevents the secret
//           from being required in source-controlled config files.
//   L-03 — RequireHttpsMetadata now conditionally true in Production (false only in Development).
//   M-05 — UserManager injection fix moved to AdminController constructor instead.
// New
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using TankerManagementSystem.Models;
using TankerManagementSystem.Models.Email;
using TankerManagementSystem.Services;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

// 1. DB Context New old old new
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(configuration.GetConnectionString("conn")));

// 2. Identity New
builder.Services.AddIdentity<IdentityUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// Modified by AI
// Date: 2026-07-21
// Reason: H-05 — Register the shared ledger recalculation service so it can be
// injected into all controllers instead of each controller duplicating the logic.
builder.Services.AddScoped<ILedgerRecalculationService, LedgerRecalculationService>();

// Modified by AI
// Date: 2026-07-21
// Reason: H-01 — Resolve JWT secret from environment variable first, then fall back
// to appsettings.json. This supports secure production deployments where the secret
// is set via environment variable (e.g., "JWT_SECRET") without modifying source files.
// In development, the secret in appsettings.Development.json is used as fallback.
string jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET")
                   ?? configuration["JWT:Secret"]
                   ?? throw new InvalidOperationException("JWT:Secret is not configured.");

// 3. JWT Authentication + Cookie Redirection Fix
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.SaveToken = true;

    // Modified by AI
    // Date: 2026-07-21
    // Reason: L-03 — RequireHttpsMetadata must be true in Production to prevent
    // token interception over plain HTTP. Only disabled in Development for convenience.
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidAudience = configuration["JWT:ValidAudience"],
        ValidIssuer = configuration["JWT:ValidIssuer"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ClockSkew = TimeSpan.Zero
    };

    // --- IMPORTANT EVENTS FOR COOKIE & REDIRECT ---
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            // Cookie se token uthana
            var token = context.Request.Cookies["BearerToken"];
            if (!string.IsNullOrEmpty(token))
            {
                context.Token = token;
            }
            return Task.CompletedTask;
        },
        OnChallenge = context =>
        {
            context.HandleResponse();
            context.Response.Redirect("/Admin/Login");
            return Task.CompletedTask;
        }
    };
});

// 4. Email service (disabled — no email in current build)
//var emailConfig = configuration.GetSection("EmailConfiguration").Get<EmailConfiguration>();
//builder.Services.AddSingleton(emailConfig);
//builder.Services.AddScoped<IEmailService, EmailService>();

// 5. MVC (Controllers + Views)
builder.Services.AddControllersWithViews();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Admin}/{action=Index}/{id?}");

app.Run();
