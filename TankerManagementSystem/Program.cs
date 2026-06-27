/*using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Configuration;
using System.Text;
using TankerManagementSystem.Models;
using TankerManagementSystem.Models.Email;
using TankerManagementSystem.Services;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

var configuration = builder.Configuration;
builder.Services.AddDbContext<ApplicationDbContext>
(options => options.UseSqlServer(configuration.GetConnectionString("conn")));


// Adding Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.SaveToken = true;
    options.RequireHttpsMetadata = false;
    options.TokenValidationParameters = new TokenValidationParameters()
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidAudience = configuration["JWT:ValidAudience"],
        ValidIssuer = configuration["JWT:ValidIssuer"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["JWT:Secret"]))
    };
});


//Add Email Configs
var emailConfig = configuration.GetSection("EmailConfiguration").Get<EmailConfiguration>();
builder.Services.AddSingleton(emailConfig);

builder.Services.AddScoped<IEmailService, EmailService>();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(option =>
{
    option.SwaggerDoc("v1", new OpenApiInfo { Title = "Auth API", Version = "v1" });
    option.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Please enter a valid token",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        BearerFormat = "JWT",
        Scheme = "Bearer"
    });
    option.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type=ReferenceType.SecurityScheme,
                    Id="Bearer"
                }
            },
            new string[]{}
        }
    });
});



var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
*/

/*using Microsoft.AspNetCore.Authentication.JwtBearer;
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

// DB Context
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(configuration.GetConnectionString("conn")));

// Identity (IMPORTANT for login + roles)
builder.Services.AddIdentity<IdentityUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// JWT Authentication (optional if using API login)
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    // Aapki purani token validation parameters...

    // --- NEW CODE: Cookie se token read karne ke liye ---
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            // Browser ki cookies me se token nikalna
            var token = context.Request.Cookies["BearerToken"];
            if (!string.IsNullOrEmpty(token))
            {
                context.Token = token;
            }
            return Task.CompletedTask;
        }
    };

    options.SaveToken = true;
    options.RequireHttpsMetadata = false;

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidAudience = configuration["JWT:ValidAudience"],
        ValidIssuer = configuration["JWT:ValidIssuer"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(configuration["JWT:Secret"]))
    };
});

// Email service
var emailConfig = configuration.GetSection("EmailConfiguration").Get<EmailConfiguration>();
builder.Services.AddSingleton(emailConfig);
builder.Services.AddScoped<IEmailService, EmailService>();

// Login aur Logout ke paths set karna
builder.Services.ConfigureApplicationCookie(options =>
{
    // Agar user authenticated nahi hai, toh is URL par redirect karo
    options.LoginPath = "/Admin/Login"; // Apne login controller/action ka sahi path yahan likhein

    // Agar user logged in hai par uske paas is page ka Role nahi hai
    options.AccessDeniedPath = "/Authentication/AccessDenied";
});


// 🔥 IMPORTANT: MVC (Views + Controllers + Razor)
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Error handling
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Static files (CSS, JS, Images)
app.UseStaticFiles();

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// MVC routing (Views ke liye REQUIRED)
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Admin}/{action=Index}/{id?}");

app.Run();*/

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

// 1. DB Context
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(configuration.GetConnectionString("conn")));

// 2. Identity
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
    //options.RequireHttpsMetadata = true;

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
            // SECURITY FIX: Explicitly redirect to the secure HTTPS version of your login page
            context.Response.Redirect("http://salesgoo.runasp.net/Admin/Login");
            //context.Response.Redirect("/Admin/Login"); 
            return Task.CompletedTask;
        }
    };
});

// 4. Email service
var emailConfig = configuration.GetSection("EmailConfiguration").Get<EmailConfiguration>();
builder.Services.AddSingleton(emailConfig);
builder.Services.AddScoped<IEmailService, EmailService>();

// 5. MVC (Controllers + Views)
builder.Services.AddControllersWithViews();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

//app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Admin}/{action=Index}/{id?}");

app.Run();