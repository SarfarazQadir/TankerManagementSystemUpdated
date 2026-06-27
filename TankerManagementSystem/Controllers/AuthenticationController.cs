/*using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NETCore.MailKit.Core;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using TankerManagementSystem.Attributes;
using TankerManagementSystem.Models;
using TankerManagementSystem.Models.Authentication.Login;
using TankerManagementSystem.Models.Authentication.SignUp;
using TankerManagementSystem.Models.Email;
using IEmailService = TankerManagementSystem.Services.IEmailService;

namespace TankerManagementSystem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    //[AuthorizeModule]
    public class AuthenticationController : ControllerBase
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;

        public AuthenticationController(UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager, IEmailService emailService,
            SignInManager<IdentityUser> signInManager, IConfiguration configuration)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _signInManager = signInManager;
            _emailService = emailService;
            _configuration = configuration;
        }
        [HttpPost]
        public async Task<IActionResult> Register([FromBody] RegisterUser registerUser, string role)
        {
            //Check User Exist 
            var userExist = await _userManager.FindByEmailAsync(registerUser.Email);
            if (userExist != null)
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    new Response { Status = "Error", Message = "User already exists!" });
            }

            //Add the User in the database
            IdentityUser user = new()
            {
                Email = registerUser.Email,
                SecurityStamp = Guid.NewGuid().ToString(),
                UserName = registerUser.Username,
                TwoFactorEnabled = true
            };
            if (await _roleManager.RoleExistsAsync(role))
            {
                var result = await _userManager.CreateAsync(user, registerUser.Password);
                if (!result.Succeeded)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError,
                        new Response { Status = "Error", Message = "User Failed to Create" });
                }
                //Add role to the user....

                await _userManager.AddToRoleAsync(user, role);

                //Add Token to Verify the email....
                var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                var confirmationLink = Url.Action(nameof(ConfirmEmail), "Authentication", new { token, email = user.Email }, Request.Scheme);
                var message = new Message(new string[] { user.Email! }, "Confirmation email link", confirmationLink!);
                _emailService.SendEmail(message);

                return StatusCode(StatusCodes.Status200OK,
                    new Response { Status = "Success", Message = $"User created & Email Sent to {user.Email} SuccessFully" });

            }
            else
            {
                return StatusCode(StatusCodes.Status500InternalServerError,
                        new Response { Status = "Error", Message = "This Role Doesnot Exist." });
            }
        }

        [HttpGet("ConfirmEmail")]
        public async Task<IActionResult> ConfirmEmail(string token, string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user != null)
            {
                var result = await _userManager.ConfirmEmailAsync(user, token);
                if (result.Succeeded)
                {
                    return StatusCode(StatusCodes.Status200OK,
                      new Response { Status = "Success", Message = "Email Verified Successfully" });
                }
            }
            return StatusCode(StatusCodes.Status500InternalServerError,
                       new Response { Status = "Error", Message = "This User Doesnot exist!" });
        }

        [HttpPost]
        [Route("login")]
        public async Task<IActionResult> Login([FromBody] LoginModel loginModel)
        {
            var username = loginModel.Username.Trim();

            var user = await _userManager.Users
                .FirstOrDefaultAsync(x => x.UserName == username);

            if (user == null)
            {
                return Unauthorized(new
                {
                    message = "User not found"
                });
            }

            var passwordValid =
                await _userManager.CheckPasswordAsync(user, loginModel.Password);

            if (!passwordValid)
            {
                return Unauthorized(new
                {
                    message = "Invalid password"
                });
            }

            // OTP
            if (user.TwoFactorEnabled)
            {
                await _signInManager.SignOutAsync();

                await _signInManager.PasswordSignInAsync(
                    user,
                    loginModel.Password,
                    false,
                    true
                );

                var token =
                    await _userManager.GenerateTwoFactorTokenAsync(user, "Email");

                var message = new Message(
                    new string[] { user.Email! },
                    "OTP Confirmation",
                    token
                );

                _emailService.SendEmail(message);

                return Ok(new
                {
                    message = $"OTP sent to {user.Email}"
                });
            }

            return Unauthorized();
        }

        [HttpPost]
        [Route("login-2FA")]
        public async Task<IActionResult> LoginWithOTP([FromQuery] string code, [FromQuery] string username)
        {
            // Agar session expire ho chuka ho, toh user object direct DB se safe fetch karein
            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null && !string.IsNullOrEmpty(username))
            {
                user = await _userManager.FindByNameAsync(username);
            }

            if (user == null)
            {
                return Unauthorized(new { message = "User session expired. Please login again." });
            }

            // VERIFY OTP
            var signIn = await _signInManager.TwoFactorSignInAsync("Email", code, false, false);

            if (!signIn.Succeeded)
            {
                return Unauthorized(new { message = "Invalid or Expired OTP" });
            }

            // CLAIMS GENERATION
            var authClaims = new List<Claim>
    {
        new Claim(ClaimTypes.Name, user.UserName!),
        new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
    };

            var userRoles = await _userManager.GetRolesAsync(user);
            foreach (var role in userRoles)
            {
                authClaims.Add(new Claim(ClaimTypes.Role, role));
            }

            // JWT TOKEN GENERATION
            var jwtToken = GetToken(authClaims);
            var tokenString = new JwtSecurityTokenHandler().WriteToken(jwtToken);

            // --- Token ko Cookie me save karein ---
            Response.Cookies.Append("BearerToken", tokenString, new CookieOptions
            {
                HttpOnly = true, // Security fix ताकि JS access na kar sake
                Secure = true,   // Sirf HTTPS par chalega
                Expires = DateTime.UtcNow.AddDays(2)
            });

            // --- ROLE BASED REDIRECTION LOGIC ---
            string redirectUrl = "/Admin/Index"; // Default redirect path

            if (userRoles.Contains("User"))
            {
                redirectUrl = "/PersonalKhata/Index"; // Agar "User" role ho toh yahan bhejein
            }

            return Ok(new
            {
                token = tokenString,
                expiration = jwtToken.ValidTo,
                redirectTo = redirectUrl // Frontend is URL par user ko window.location.href se redirect karega
            });
        }
        private JwtSecurityToken GetToken(List<Claim> authClaims)
        {
            var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JWT:Secret"]));

            var token = new JwtSecurityToken(
                issuer: _configuration["JWT:ValidIssuer"],
                audience: _configuration["JWT:ValidAudience"],
                expires: DateTime.Now.AddDays(2),
                claims: authClaims,
                signingCredentials: new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256)
                );

            return token;
        }
    }
}*/
/*using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using TankerManagementSystem.Models;
using TankerManagementSystem.Models.Authentication.Login;
using TankerManagementSystem.Models.Authentication.SignUp;
using TankerManagementSystem.Models.Email;
using IEmailService = TankerManagementSystem.Services.IEmailService;

namespace TankerManagementSystem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthenticationController : ControllerBase
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;

        public AuthenticationController(UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager, IEmailService emailService,
            SignInManager<IdentityUser> signInManager, IConfiguration configuration)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _signInManager = signInManager;
            _emailService = emailService;
            _configuration = configuration;
        }

        [HttpPost]
        public async Task<IActionResult> Register([FromBody] RegisterUser registerUser, string role)
        {
            var userExist = await _userManager.FindByEmailAsync(registerUser.Email);
            if (userExist != null)
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    new Response { Status = "Error", Message = "User already exists!" });
            }

            IdentityUser user = new()
            {
                Email = registerUser.Email,
                SecurityStamp = Guid.NewGuid().ToString(),
                UserName = registerUser.Username,
                TwoFactorEnabled = true
            };

            if (await _roleManager.RoleExistsAsync(role))
            {
                var result = await _userManager.CreateAsync(user, registerUser.Password);
                if (!result.Succeeded)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError,
                        new Response { Status = "Error", Message = "User Failed to Create" });
                }

                await _userManager.AddToRoleAsync(user, role);

                // --- LINK GENERATION MATRIX ---
                var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                var confirmationLink = Url.Action(nameof(ConfirmEmail), "Authentication", new { token, email = user.Email }, Request.Scheme);

                // Absolute image resolution for structural mail clients (Gmail, Outlook)
                var logoUrl = $"{Request.Scheme}://{Request.Host}/img/flyeasy.png";

                // Email template design mapping HTML output injection
                string emailBody = $@"
                <div style='font-family:sans-serif; background-color:#fcfbfe; padding:30px; color:#3d2a42;'>
                    <div style='max-width:550px; background:#fff; margin:0 auto; border-radius:10px; border:1px solid #f1e9f3; overflow:hidden;'>
                        <div style='background:#86608E; padding:25px; text-align:center;'>
                            <img src='{logoUrl}' alt='FlyEasy' style='max-height:50px;'/>
                        </div>
                        <div style='padding:30px;'>
                            <h2 style='color:#2e1f33; margin:0 0 15px;'>Verify Email Registration</h2>
                            <p style='color:#6b5873; font-size:14px; line-height:1.5;'>Please complete authentication processes by clicking down below:</p>
                            <div style='text-align:center; margin:25px 0;'>
                                <a href='{confirmationLink}' style='background:#86608E; color:#fff; text-decoration:none; padding:12px 28px; border-radius:6px; font-weight:600; display:inline-block;'>Confirm Email Address</a>
                            </div>
                        </div>
                    </div>
                </div>";

                // CRITICAL FIX: Message parameters are assigned explicitly ensuring HTML container delivery
                var message = new Message(new string[] { user.Email! }, "FlyEasy Account Verification Link", emailBody);
                _emailService.SendEmail(message);

                return StatusCode(StatusCodes.Status200OK,
                    new Response { Status = "Success", Message = $"User created & Email Sent to {user.Email} SuccessFully" });
            }
            else
            {
                return StatusCode(StatusCodes.Status500InternalServerError,
                        new Response { Status = "Error", Message = "This Role Doesnot Exist." });
            }
        }

        [HttpGet("ConfirmEmail")]
        public async Task<IActionResult> ConfirmEmail(string token, string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user != null)
            {
                var result = await _userManager.ConfirmEmailAsync(user, token);
                if (result.Succeeded)
                {
                    return Content($@"
                    <html>
                    <body style='font-family:sans-serif; background-color:#fcfbfe; display:flex; align-items:center; justify-content:center; height:100vh; margin:0;'>
                        <div style='text-align:center; background:white; padding:40px; border-radius:12px; box-shadow:0 4px 12px rgba(134,96,142,0.1); border:1px solid #f1e9f3; max-width:400px;'>
                            <div style='color:#86608E; font-size:48px; margin-bottom:15px;'>&check;</div>
                            <h2 style='color:#2e1f33; margin:0 0 10px;'>Email Verified Successfully</h2>
                            <p style='color:#6b5873; font-size:14px; margin-bottom:25px;'>Your profile verification matrix has been fully synchronized.</p>
                            <a href='/Admin/Login' style='background:#86608E; color:white; text-decoration:none; padding:10px 24px; border-radius:6px; font-weight:600; font-size:14px;'>Proceed to Login</a>
                        </div>
                    </body>
                    </html>", "text/html");
                }
            }
            return StatusCode(StatusCodes.Status500InternalServerError,
                       new Response { Status = "Error", Message = "This User Doesnot exist!" });
        }

        [HttpPost]
        [Route("login")]
        public async Task<IActionResult> Login([FromBody] LoginModel loginModel)
        {
            var username = loginModel.Username.Trim();
            var user = await _userManager.Users.FirstOrDefaultAsync(x => x.UserName == username);

            if (user == null)
            {
                return Unauthorized(new { message = "User not found" });
            }

            var passwordValid = await _userManager.CheckPasswordAsync(user, loginModel.Password);
            if (!passwordValid)
            {
                return Unauthorized(new { message = "Invalid password" });
            }

            if (user.TwoFactorEnabled)
            {
                await _signInManager.SignOutAsync();
                await _signInManager.PasswordSignInAsync(user, loginModel.Password, false, true);

                var token = await _userManager.GenerateTwoFactorTokenAsync(user, "Email");
                var logoUrl = $"{Request.Scheme}://{Request.Host}/img/flyeasy.png";

                string otpBody = $@"
                <div style='font-family:sans-serif; background-color:#fcfbfe; padding:30px; color:#3d2a42;'>
                    <div style='max-width:450px; background:#fff; margin:0 auto; border-radius:10px; border:1px solid #f1e9f3; overflow:hidden; text-align:center;'>
                        <div style='background:#86608E; padding:20px; text-align:center;'>
                            <img src='{logoUrl}' alt='FlyEasy' style='max-height:45px;'/>
                        </div>
                        <div style='padding:30px;'>
                            <h3 style='color:#2e1f33; margin:0 0 10px; text-align:left;'>Two-Factor Verification Code</h3>
                            <p style='color:#6b5873; font-size:14px; text-align:left; line-height:1.4;'>Use the dynamic identity validation code down below to fulfill authorization parameters:</p>
                            <div style='background-color:#f9f6fa; border:2px dashed #86608E; border-radius:8px; padding:12px; font-size:28px; font-weight:700; color:#86608E; letter-spacing:5px; display:inline-block; margin:15px auto; min-width:150px;'>{token}</div>
                            <p style='color:#a392a8; font-size:11px; text-align:left; margin-top:15px;'>If you did not execute this validation command block, secure your operational data immediately.</p>
                        </div>
                    </div>
                </div>";

                var message = new Message(new string[] { user.Email! }, "FlyEasy Security Verification Key (OTP)", otpBody);
                _emailService.SendEmail(message);

                return Ok(new { message = $"OTP sent to {user.Email}" });
            }

            return Unauthorized();
        }

        [HttpPost]
        [Route("login-2FA")]
        public async Task<IActionResult> LoginWithOTP([FromQuery] string code, [FromQuery] string username)
        {
            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null && !string.IsNullOrEmpty(username))
            {
                user = await _userManager.FindByNameAsync(username);
            }

            if (user == null)
            {
                return Unauthorized(new { message = "User session expired. Please login again." });
            }

            var signIn = await _signInManager.TwoFactorSignInAsync("Email", code, false, false);
            if (!signIn.Succeeded)
            {
                return Unauthorized(new { message = "Invalid or Expired OTP" });
            }

            var authClaims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.UserName!),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            };

            var userRoles = await _userManager.GetRolesAsync(user);
            foreach (var role in userRoles)
            {
                authClaims.Add(new Claim(ClaimTypes.Role, role));
            }

            var jwtToken = GetToken(authClaims);
            var tokenString = new JwtSecurityTokenHandler().WriteToken(jwtToken);

            Response.Cookies.Append("BearerToken", tokenString, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                Expires = DateTime.UtcNow.AddDays(2)
            });

            string redirectUrl = "/Admin/Index";
            if (userRoles.Contains("User"))
            {
                redirectUrl = "/PersonalKhata/Index";
            }

            return Ok(new
            {
                token = tokenString,
                expiration = jwtToken.ValidTo,
                redirectTo = redirectUrl
            });
        }

        private JwtSecurityToken GetToken(List<Claim> authClaims)
        {
            var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JWT:Secret"]));

            var token = new JwtSecurityToken(
                issuer: _configuration["JWT:ValidIssuer"],
                audience: _configuration["JWT:ValidAudience"],
                expires: DateTime.Now.AddDays(2),
                claims: authClaims,
                signingCredentials: new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256)
                );

            return token;
        }
    }
}*/

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using TankerManagementSystem.Models;
using TankerManagementSystem.Models.Authentication.Login;
using TankerManagementSystem.Models.Authentication.SignUp;
using TankerManagementSystem.Models.Email;
using IEmailService = TankerManagementSystem.Services.IEmailService;

// Required namespaces for View Engine context parsing
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace TankerManagementSystem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthenticationController : ControllerBase
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly IRazorViewEngine _viewEngine;
        private readonly ITempDataProvider _tempDataProvider;

        public AuthenticationController(UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager, IEmailService emailService,
            SignInManager<IdentityUser> signInManager, IConfiguration configuration,
            IRazorViewEngine viewEngine, ITempDataProvider tempDataProvider)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _signInManager = signInManager;
            _emailService = emailService;
            _configuration = configuration;
            _viewEngine = viewEngine;
            _tempDataProvider = tempDataProvider;
        }

        [HttpPost]
        public async Task<IActionResult> Register([FromBody] RegisterUser registerUser, string role)
        {
            var userExist = await _userManager.FindByEmailAsync(registerUser.Email);
            if (userExist != null)
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    new Response { Status = "Error", Message = "User already exists!" });
            }

            IdentityUser user = new()
            {
                Email = registerUser.Email,
                SecurityStamp = Guid.NewGuid().ToString(),
                UserName = registerUser.Username,
                TwoFactorEnabled = true
            };

            if (await _roleManager.RoleExistsAsync(role))
            {
                var result = await _userManager.CreateAsync(user, registerUser.Password);
                if (!result.Succeeded)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError,
                        new Response { Status = "Error", Message = "User Failed to Create" });
                }

                await _userManager.AddToRoleAsync(user, role);

                var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                var confirmationLink = Url.Action(nameof(ConfirmEmail), "Authentication", new { token, email = user.Email }, Request.Scheme);
                var logoUrl = $"{Request.Scheme}://{Request.Host}/img/flyeasy.png";

                // Model mapping data passed directly into your .cshtml view template
                var emailData = new System.Dynamic.ExpandoObject() as dynamic;
                emailData.Username = user.UserName;
                emailData.ConfirmationLink = confirmationLink;
                emailData.LogoUrl = logoUrl;

                // FIX: Now calling the actual file template view
                string emailBody = await RenderViewToStringAsync("~/Views/Shared/_EmailConfirmationTemplate.cshtml", emailData);

                var message = new Message(new string[] { user.Email! }, "FlyEasy Account Verification Link", emailBody);
                _emailService.SendEmail(message);

                return StatusCode(StatusCodes.Status200OK,
                    new Response { Status = "Success", Message = $"User created & Email Sent to {user.Email} SuccessFully" });
            }
            else
            {
                return StatusCode(StatusCodes.Status500InternalServerError,
                        new Response { Status = "Error", Message = "This Role Doesnot Exist." });
            }
        }

        [HttpGet("ConfirmEmail")]
        public async Task<IActionResult> ConfirmEmail(string token, string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user != null)
            {
                var result = await _userManager.ConfirmEmailAsync(user, token);
                if (result.Succeeded)
                {
                    return Content($@"
                    <html>
                    <body style='font-family:sans-serif; background-color:#fcfbfe; display:flex; align-items:center; justify-content:center; height:100vh; margin:0;'>
                        <div style='text-align:center; background:white; padding:40px; border-radius:12px; box-shadow:0 4px 12px rgba(134,96,142,0.1); border:1px solid #f1e9f3; max-width:400px;'>
                            <div style='color:#86608E; font-size:48px; margin-bottom:15px;'>&check;</div>
                            <h2 style='color:#2e1f33; margin:0 0 10px;'>Email Verified Successfully</h2>
                            <p style='color:#6b5873; font-size:14px; margin-bottom:25px;'>Your profile verification matrix has been fully synchronized.</p>
                            <a href='/Admin/Login' style='background:#86608E; color:white; text-decoration:none; padding:10px 24px; border-radius:6px; font-weight:600; font-size:14px;'>Proceed to Login</a>
                        </div>
                    </body>
                    </html>", "text/html");
                }
            }
            return StatusCode(StatusCodes.Status500InternalServerError,
                       new Response { Status = "Error", Message = "This User Doesnot exist!" });
        }

        [HttpPost]
        [Route("login")]
        public async Task<IActionResult> Login([FromBody] LoginModel loginModel)
        {
            var username = loginModel.Username.Trim();
            var user = await _userManager.Users.FirstOrDefaultAsync(x => x.UserName == username);

            if (user == null)
            {
                return Unauthorized(new { message = "User not found" });
            }

            var passwordValid = await _userManager.CheckPasswordAsync(user, loginModel.Password);
            if (!passwordValid)
            {
                return Unauthorized(new { message = "Invalid password" });
            }

            if (user.TwoFactorEnabled)
            {
                await _signInManager.SignOutAsync();
                await _signInManager.PasswordSignInAsync(user, loginModel.Password, false, true);

                var token = await _userManager.GenerateTwoFactorTokenAsync(user, "Email");
                var logoUrl = $"http://malikbrother.runasp.net/img/flyeasy.png";

                var emailData = new System.Dynamic.ExpandoObject() as dynamic;
                emailData.OtpCode = token; // Make sure inside _OtpConfirmationTemplate you read @Model.OtpCode
                emailData.LogoUrl = logoUrl;

                // FIX: Now calling the actual file template view for OTP
                string otpBody = await RenderViewToStringAsync("~/Views/Shared/_OtpConfirmationTemplate.cshtml", emailData);

                var message = new Message(new string[] { user.Email! }, "Tanker Management System Verification Key (OTP)", otpBody);
                _emailService.SendEmail(message);

                return Ok(new { message = $"OTP sent to {user.Email}" });
            }

            return Unauthorized();
        }

        [HttpPost]
        [Route("login-2FA")]
        public async Task<IActionResult> LoginWithOTP([FromQuery] string code, [FromQuery] string username)
        {
            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null && !string.IsNullOrEmpty(username))
            {
                user = await _userManager.FindByNameAsync(username);
            }

            if (user == null)
            {
                return Unauthorized(new { message = "User session expired. Please login again." });
            }

            var signIn = await _signInManager.TwoFactorSignInAsync("Email", code, false, false);
            if (!signIn.Succeeded)
            {
                return Unauthorized(new { message = "Invalid or Expired OTP" });
            }

            var authClaims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.UserName!),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            };

            var userRoles = await _userManager.GetRolesAsync(user);
            foreach (var role in userRoles)
            {
                authClaims.Add(new Claim(ClaimTypes.Role, role));
            }

            var jwtToken = GetToken(authClaims);
            var tokenString = new JwtSecurityTokenHandler().WriteToken(jwtToken);

            /*  Response.Cookies.Append("BearerToken", tokenString, new CookieOptions
              {
                  HttpOnly = true,
                  Secure = true,
                  Expires = DateTime.UtcNow.AddHours(5)
              });*/

            Response.Cookies.Append("BearerToken", tokenString, new CookieOptions
            {
                HttpOnly = true,
                Secure = false,        // 🔥 ISS KO FALSE KAREIN: Taaki HTTP par bhi cookie save ho sake
                SameSite = SameSiteMode.Lax, // 🔥 ISS KO LAX KAREIN: Strict HTTP par block ho jata hai live server par
                Expires = DateTime.UtcNow.AddHours(5)
            });

            string redirectUrl = "/Admin/Index";
            if (userRoles.Contains("User"))
            {
                redirectUrl = "/PersonalKhata/Index";
            }

            return Ok(new
            {
                token = tokenString,
                expiration = jwtToken.ValidTo,
                redirectTo = redirectUrl
            });
        }

        private JwtSecurityToken GetToken(List<Claim> authClaims)
        {
            var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JWT:Secret"]));

            var token = new JwtSecurityToken(
                issuer: _configuration["JWT:ValidIssuer"],
                audience: _configuration["JWT:ValidAudience"],
                expires: DateTime.Now.AddHours(5),
                claims: authClaims,
                signingCredentials: new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256)
                );

            return token;
        }

        [HttpPost]
        [Route("logout")]
        public async Task<IActionResult> Logout()
        {
            // 1. Identity SignOut (Agar koi local session cookies hain toh unhein clear karne ke liye)
            await _signInManager.SignOutAsync();

            // 2. JWT Cookie ko delete karna (Expiry date past ki de kar)
            Response.Cookies.Append("BearerToken", "", new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                Expires = DateTime.UtcNow.AddDays(-1) // Purani date dene se cookie expire ho jati hai
            });

            return Ok(new { message = "Logged out successfully", redirectTo = "/Admin/Login" });
        }

        // Active View Engine String Generator Block
        private async Task<string> RenderViewToStringAsync(string viewPath, object model)
        {
            // Fix 1: Removed typo and properly initialized ActionContext
            var actionContext = new ActionContext(HttpContext, RouteData, new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());

            using (var sw = new StringWriter())
            {
                // Fix 2: Corrected GetView parameters (removed viewExecutingEnv)
                var viewResult = _viewEngine.GetView(executingFilePath: null, viewPath: viewPath, isMainPage: false);

                // Fix 3: Changed .Succeeded to .Success
                if (!viewResult.Success)
                {
                    viewResult = _viewEngine.FindView(actionContext, viewPath, isMainPage: false);
                }

                if (!viewResult.Success)
                {
                    throw new ArgumentNullException($"Template view file could not be located at path: {viewPath}");
                }

                var viewDictionary = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
                {
                    Model = model
                };

                var viewContext = new ViewContext(
                    actionContext,
                    viewResult.View,
                    viewDictionary,
                    new TempDataDictionary(actionContext.HttpContext, _tempDataProvider),
                    sw,
                    new HtmlHelperOptions()
                );

                await viewResult.View.RenderAsync(viewContext);
                return sw.ToString();
            }
        }
    }
}