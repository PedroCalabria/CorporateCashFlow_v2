using CorporateTreasury.Api;
using CorporateTreasury.Infrastructure;
using Hangfire;

var builder = WebApplication.CreateBuilder(args);

// --- Composition root: register each layer's services ---
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Infrastructure: EF Core write model (empty) + Hangfire (Postgres-backed).
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// --- HTTP pipeline ---
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Hangfire dashboard. Left open in the project-bootstrap change — a Manager-only
// IDashboardAuthorizationFilter is deferred until auth exists (see
// docs/technical-architecture.md §5, item 1).
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new AllowAllDashboardAuthorizationFilter() },
});

app.MapControllers();

// Health/root endpoints — confirm the app started and connected, satisfying the
// "API responds" scaffolding scenario. No business behavior.
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/", () => Results.Ok(new { service = "CorporateTreasury.Api", status = "running" }));

app.Run();
