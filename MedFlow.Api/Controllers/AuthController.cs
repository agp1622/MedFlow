using MedFlow.Api.Extensions;
using MedFlow.Core.DTOs;
using MedFlow.Infrastructure.Data;
using MedFlow.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MedFlow.Core.Entities;
using Google.Apis.Auth;
using MedFlow.Core.Interfaces;
using Microsoft.AspNetCore.RateLimiting;
using System.Net;

namespace MedFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IConfiguration _config;
    private readonly AppDbContext _db;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<AuthController> _logger;

    public AuthController(UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IConfiguration config, AppDbContext db,
        IEmailSender emailSender, ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _config = config;
        _db = db;
        _emailSender = emailSender;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest req)
    {
        var existing = await _userManager.FindByEmailAsync(req.Email);
        if (existing != null) return BadRequest(new { error = "Email already registered." });

        var user = new ApplicationUser
        {
            UserName = req.Email,
            Email = req.Email,
            FirstName = req.FirstName,
            LastName = req.LastName,
            Specialty = req.Specialty
        };

        var result = await _userManager.CreateAsync(user, req.Password);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        // Create linked Doctor record
        var doctor = new Doctor
        {
            UserId = user.Id,
            FirstName = req.FirstName,
            LastName = req.LastName,
            Specialty = req.Specialty
        };
        _db.Doctors.Add(doctor);
        await _db.SaveChangesAsync();

        var token = user.GenerateToken(_config);
        return Ok(new AuthResponse(token, Guid.NewGuid().ToString(), DateTime.UtcNow.AddHours(1),
            new UserDto(user.Id, user.Email!, user.FirstName, user.LastName, user.Specialty)));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest req)
    {
        var user = await _userManager.FindByEmailAsync(req.Email);
        if (user == null) return Unauthorized(new { error = "Invalid credentials." });

        var result = await _signInManager.CheckPasswordSignInAsync(user, req.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            if (result.IsLockedOut) return Unauthorized(new { error = "Account locked. Try again later." });
            return Unauthorized(new { error = "Invalid credentials." });
        }

        var token = user.GenerateToken(_config);
        return Ok(new AuthResponse(token, Guid.NewGuid().ToString(), DateTime.UtcNow.AddHours(1),
            new UserDto(user.Id, user.Email!, user.FirstName, user.LastName, user.Specialty)));
    }

    [HttpPost("google-login")]
    public async Task<ActionResult<AuthResponse>> GoogleLogin([FromBody] GoogleLoginRequest req)
    {
        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { _config["Authentication:Google:ClientId"] }
            };
            var payload = await GoogleJsonWebSignature.ValidateAsync(req.Credential, settings);

            var user = await _userManager.FindByEmailAsync(payload.Email);
            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = payload.Email,
                    Email = payload.Email,
                    FirstName = payload.GivenName ?? "Unknown",
                    LastName = payload.FamilyName ?? "Unknown",
                    Specialty = "General Practice" // Default for Google Sign-In
                };

                var result = await _userManager.CreateAsync(user); // No password for Google Sign-In users
                if (!result.Succeeded)
                    return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

                // Create linked Doctor record
                var doctor = new Doctor
                {
                    UserId = user.Id,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Specialty = user.Specialty
                };
                _db.Doctors.Add(doctor);
                await _db.SaveChangesAsync();
            }

            var token = user.GenerateToken(_config);
            return Ok(new AuthResponse(token, Guid.NewGuid().ToString(), DateTime.UtcNow.AddHours(1),
                new UserDto(user.Id, user.Email!, user.FirstName, user.LastName, user.Specialty)));
        }
        catch (InvalidJwtException)
        {
            return Unauthorized(new { error = "Invalid Google token." });
        }
    }

    [HttpPost("forgot-password")]
    [EnableRateLimiting("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(req.Email))
            return BadRequest(new { errors = new[] { "A valid email is required." } });

        const string genericMessage = "If that email is registered, a password reset link has been sent.";
        var frontendUrl = (_config.GetSection("AllowedOrigins").Get<string[]>()?.FirstOrDefault() ?? "http://localhost:5173").TrimEnd('/');

        try
        {
            var user = await _userManager.FindByEmailAsync(req.Email);
            if (user == null)
            {
                _logger.LogInformation("Password reset requested for unregistered email");
            }
            else if (await _userManager.HasPasswordAsync(user))
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var link = $"{frontendUrl}/reset-password?token={WebUtility.UrlEncode(token)}&email={WebUtility.UrlEncode(user.Email)}";
                await _emailSender.SendAsync(user.Email!, "Reset your MedFlow password",
                    $"<p>Hello {WebUtility.HtmlEncode(user.FirstName)},</p>" +
                    "<p>We received a request to reset your <strong>MedFlow</strong> password. Click the link below to choose a new one:</p>" +
                    $"<p><a href=\"{link}\">Reset my password</a></p>" +
                    "<p>If you did not request this, you can safely ignore this email.</p><p>— The MedFlow team</p>");
                _logger.LogInformation("Password reset email sent for user {UserId}", user.Id);
            }
            else
            {
                await _emailSender.SendAsync(user.Email!, "Sign in to MedFlow with Google",
                    $"<p>Hello {WebUtility.HtmlEncode(user.FirstName)},</p>" +
                    "<p>Your <strong>MedFlow</strong> account uses Google Sign-In and has no password to reset.</p>" +
                    $"<p>Please <a href=\"{frontendUrl}/login\">go to MedFlow</a> and choose \"Sign in with Google\".</p>" +
                    "<p>— The MedFlow team</p>");
                _logger.LogInformation("Google sign-in guidance email sent for user {UserId}", user.Id);
            }
        }
        catch (Exception ex)
        {
            // Never change the response based on delivery outcome (no enumeration signal)
            _logger.LogError(ex, "Failed to process password reset request");
        }

        return Ok(new { message = genericMessage });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req)
    {
        const string invalidLink = "This reset link is invalid or has expired. Please request a new one.";

        if (req.NewPassword != req.ConfirmPassword)
            return BadRequest(new { errors = new[] { "Passwords do not match." } });

        var user = string.IsNullOrWhiteSpace(req.Email) ? null : await _userManager.FindByEmailAsync(req.Email);
        if (user == null || string.IsNullOrWhiteSpace(req.Token))
            return BadRequest(new { error = invalidLink });

        var result = await _userManager.ResetPasswordAsync(user, req.Token, req.NewPassword);
        if (!result.Succeeded)
        {
            if (result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.InvalidToken)))
            {
                _logger.LogWarning("Password reset failed (invalid token) for user {UserId}", user.Id);
                return BadRequest(new { error = invalidLink });
            }
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });
        }

        await _userManager.SetLockoutEndDateAsync(user, null);
        await _userManager.ResetAccessFailedCountAsync(user);
        _logger.LogInformation("Password reset completed for user {UserId}", user.Id);

        return Ok(new { message = "Your password has been reset. You can now sign in." });
    }
}
