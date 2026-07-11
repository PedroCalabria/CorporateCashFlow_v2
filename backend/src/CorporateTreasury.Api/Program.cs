using System.IdentityModel.Tokens.Jwt;
using System.Text;
using CorporateTreasury.Api.Auth;
using CorporateTreasury.Application.DTOs.Auth;
using CorporateTreasury.Application.DTOs.Subsidiaries;
using CorporateTreasury.Application.Interfaces;
using CorporateTreasury.Application.Validators.Auth;
using CorporateTreasury.Application.Validators.Subsidiaries;
using CorporateTreasury.Infrastructure;
using CorporateTreasury.Infrastructure.Auth;
using CorporateTreasury.Infrastructure.Persistence;
using FluentValidation;
using Hangfire;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// --- Composition root: register each layer's services ---
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Infrastructure: EF Core write model (Users/RefreshTokens) + Hangfire + auth services.
builder.Services.AddInfrastructure(builder.Configuration);

// Request-scoped current user (reads JWT claims off HttpContext) and request validators.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IValidator<LoginRequest>, LoginRequestValidator>();
builder.Services.AddScoped<IValidator<CreateSubsidiaryRequest>, CreateSubsidiaryRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateSubsidiaryRequest>, UpdateSubsidiaryRequestValidator>();

// --- JWT bearer authentication (auth capability) ---
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("The 'Jwt' configuration section is missing.");

// Keep the JWT claims verbatim ("sub"/"role"/"subsidiaryId") instead of the legacy
// SOAP-style remapping, so ICurrentUserService reads the same names it issued.
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = JwtTokenService.RoleClaimType,
        };
    });

// Global-Manager-only policy for the subsidiaries capability (design.md §D2). The handler reads
// ICurrentUserService; RequireAuthenticatedUser makes anonymous requests 401 and failing-policy
// (subsidiary-scoped) requests 403.
builder.Services.AddScoped<IAuthorizationHandler, GlobalManagerHandler>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthorizationPolicies.GlobalManager, policy => policy
        .RequireAuthenticatedUser()
        .AddRequirements(new GlobalManagerRequirement()));
});

var app = builder.Build();

// --- Apply migrations + dev seed on startup (Development only) ---
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
    await DevelopmentDataSeeder.SeedAsync(db, passwordHasher);
}

// --- HTTP pipeline ---
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();

// Hangfire dashboard — now restricted to authenticated Managers (replaces the temporary
// AllowAll filter from project-bootstrap; docs/technical-architecture.md §5, item 1).
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new ManagerDashboardAuthorizationFilter() },
});

app.MapControllers();

// Health/root endpoints — anonymous scaffolding probes confirming the app started.
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/", () => Results.Ok(new { service = "CorporateTreasury.Api", status = "running" }));

app.Run();

// Exposed so the integration-test WebApplicationFactory can reference the entry point.
public partial class Program;
