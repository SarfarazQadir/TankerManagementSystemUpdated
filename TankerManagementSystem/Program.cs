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

// 1. DB Context new
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(configuration.GetConnectionString("conn")));

// 2. Identity New
builder.Services.AddIdentity<IdentityUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// 3. JWT Authentication + Cookie Redirection Fix
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.SaveToken = true;
    options.RequireHttpsMetadata = false;

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidAudience = configuration["JWT:ValidAudience"],
        ValidIssuer = configuration["JWT:ValidIssuer"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["JWT:Secret"])),
        ClockSkew = TimeSpan.Zero // Token expiry me delay khatam karne ke liye
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
            // 🔥 YEH FIX HAI: Jab user pass nahi hoga (401 aayega), toh white page ke bajaye login par redirect hoga
            context.HandleResponse();
            context.Response.Redirect("/Admin/Login"); 
            return Task.CompletedTask;
        }
    };
});

// 4. Email service
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
