using MedFlow.Api.Extensions;
using MedFlow.Core.DTOs;
using MedFlow.Infrastructure.Data;
using MedFlow.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MedFlow.Core.Entities;
using Google.Apis.Auth;

namespace MedFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IConfiguration _config;
    private readonly AppDbContext _db;

    public AuthController(UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IConfiguration config, AppDbContext db)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _config = config;
        _db = db;
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
}
