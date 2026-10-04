using System.Text;
using System.Threading.RateLimiting;
using System.Text.Json.Serialization;
using MedFlow.Api.Localization;
using MedFlow.Api.Middleware;
using MedFlow.Infrastructure;
using MedFlow.Infrastructure.Data;
using MedFlow.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();
builder.Host.UseSerilog();

// Infrastructure (EF, Identity, Repos)
builder.Services.AddInfrastructure(builder.Configuration);

// JWT Auth
var jwtSettings = builder.Configuration.GetSection("Jwt");
var key = Encoding.UTF8.GetBytes(jwtSettings["Key"]!);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ClockSkew = TimeSpan.Zero
    };
    
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/api/attachments"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization(options =>
{
    // One policy per permission; the handler checks the caller's clinic role against the permission matrix
    foreach (var permission in Enum.GetValues<MedFlow.Core.Permission>())
        options.AddPolicy(MedFlow.Api.Authorization.HasPermissionAttribute.PolicyPrefix + permission,
            policy => policy.RequireAuthenticatedUser()
                .AddRequirements(new MedFlow.Api.Authorization.PermissionRequirement(permission)));
});
builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, MedFlow.Api.Authorization.PermissionAuthorizationHandler>();
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
    .ConfigureApiBehaviorOptions(options =>
    {
        // Same ProblemDetails shape and errors as the default 400, with a title in the requested language
        options.InvalidModelStateResponseFactory = context =>
        {
            var problem = new ValidationProblemDetails(context.ModelState)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = Localizer.Get(context.HttpContext, "Error.Validation"),
                Instance = context.HttpContext.Request.Path
            };
            return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
        };
    });
builder.Services.AddEndpointsApiExplorer();

// Swagger with JWT
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "MedFlow API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header. Enter: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

// CORS for React client
builder.Services.AddCors(options =>
{
    options.AddPolicy("MedFlowClient", policy =>
        policy.WithOrigins(
            builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? ["http://localhost:5173"])
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

// Rate limiting (password recovery abuse protection)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("forgot-password", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(15),
                QueueLimit = 0
            }));
    options.AddPolicy("accept-invitation", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Configuration.GetValue("RateLimiting:AcceptInvitationPermitLimit", 10),
                Window = TimeSpan.FromMinutes(15),
                QueueLimit = 0
            }));
    options.AddPolicy("staff-invitation", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Configuration.GetValue("RateLimiting:StaffInvitationPermitLimit", 10),
                Window = TimeSpan.FromMinutes(15),
                QueueLimit = 0
            }));
    options.AddPolicy("intake-public", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Configuration.GetValue("RateLimiting:IntakePermitLimit", 10),
                Window = TimeSpan.FromMinutes(15),
                QueueLimit = 0
            }));
    options.AddPolicy("appointment-response", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Configuration.GetValue("RateLimiting:AppointmentResponsePermitLimit", 30),
                Window = TimeSpan.FromMinutes(15),
                QueueLimit = 0
            }));
    options.AddPolicy("two-factor", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Configuration.GetValue("RateLimiting:TwoFactorPermitLimit", 10),
                Window = TimeSpan.FromMinutes(15),
                QueueLimit = 0
            }));
    options.AddPolicy("waitlist-offer", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Configuration.GetValue("RateLimiting:WaitlistOfferPermitLimit", 30),
                Window = TimeSpan.FromMinutes(15),
                QueueLimit = 0
            }));
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { error = Localizer.Get(context.HttpContext, "Error.TooManyRequests") }, token);
    };
});

builder.Services.AddScoped<MedFlow.Api.Services.BookingConfirmationService>();
builder.Services.AddHostedService<MedFlow.Api.Services.ReminderBackgroundService>();

var app = builder.Build();

// Migrate DB on startup (dev convenience; use proper migration pipeline in prod)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(scope.ServiceProvider);
}

app.UseSerilogRequestLogging();
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "MedFlow API v1"));
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCors("MedFlowClient");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<ErrorHandlingMiddleware>();
app.MapControllers();

app.Run();

// Exposed so integration tests can use WebApplicationFactory<Program>
public partial class Program { }
