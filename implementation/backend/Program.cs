using MongoDB.Driver;
using ZeroTrust.Backend.Data;
using ZeroTrust.Backend.Services;
using ZeroTrust.Backend.Models;


var builder = WebApplication.CreateBuilder(args);

// ============ Configuration ============
var mongoConnectionString = builder.Configuration.GetConnectionString("MongoDB") 
    ?? "mongodb://localhost:27017";
var databaseName = builder.Configuration.GetValue<string>("MongoDb:DatabaseName") 
    ?? "zero_trust_db";

// ============ Services ============

// MongoDB
var mongoClient = new MongoClient(mongoConnectionString);
builder.Services.AddSingleton<IMongoClient>(mongoClient);
builder.Services.AddSingleton<IDataService>(sp => 
    new MongoDataService(mongoClient, databaseName)
);

// JWT and JIT provisioning services
builder.Services.AddSingleton<IJwtExtractionService, JwtExtractionService>();
builder.Services.AddScoped<IJitProvisioningService, JitProvisioningService>();

// Database seeder hosted service
builder.Services.AddHostedService<DatabaseSeederHostedService>();

// Add logging
builder.Services.AddLogging(config =>
{
    config.AddConsole();
    config.AddDebug();
});

// ============ Build App ============
var app = builder.Build();

// ============ Middleware ============
app.UseHttpsRedirection();

// ============ Routes ============

// Health check
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
    .WithName("Health");

// Get current user information with roles and organization
// This endpoint demonstrates JIT provisioning - it will auto-provision never-before-seen users
// and return their organization context immediately
app.MapGet("/me", async (HttpContext context, IDataService dataService, IJwtExtractionService jwtService, IJitProvisioningService jitService) =>
{
    // Extract JWT claims from Authorization header
    var (success, claims, errorMessage) = jwtService.ExtractClaims(context);
    if (!success || claims == null)
    {
        return Results.Unauthorized();
    }

    // Provision user if first login (JIT provisioning)
    var user = await jitService.ProvisionUserAsync(claims);
    if (user == null)
    {
        return Results.BadRequest(new { error = "Could not provision user from JWT claims" });
    }

    // Determine if user was just created (for JIT provisioning testing)
    var isNewlyProvisioned = user.CreatedAt > DateTime.UtcNow.AddSeconds(-5);

    // Get organization information
    if (string.IsNullOrEmpty(user.OrganizationId))
    {
        return Results.BadRequest(new { error = "User has no organization assigned" });
    }

    var organization = await dataService.GetOrganizationByIdAsync(user.OrganizationId);
    if (organization == null)
    {
        return Results.BadRequest(new { error = "Organization not found for user" });
    }

    // Get user's roles
    var userRoles = await dataService.GetUserRolesAsync(user.Id);
    var roleNames = userRoles.Select(r => r.Name).ToList();

    // Extract IdP from claims if available
    var idpFromClaims = jwtService.GetClaimValue(claims, "identity_provider") 
        ?? user.IdentityProvider 
        ?? "Unknown";

    // Build response
    var response = new CurrentUserResponse
    {
        UserId = user.Id,
        Email = user.Email ?? "unknown@example.com",
        DisplayName = user.DisplayName ?? "Unknown User",
        IdentityProvider = idpFromClaims,
        OrganizationId = organization.Id,
        OrganizationName = organization.Name,
        Roles = roleNames,
        Status = user.Status.ToString(),
        CreatedAt = user.CreatedAt,
        LastAccessAt = user.LastAccessAt,
        IsNewlyProvisioned = isNewlyProvisioned
    };

    return Results.Ok(response);
})
.WithName("GetCurrentUser");

// Example endpoint requiring JWT
app.MapGet("/requests/pending", async (HttpContext context, IDataService dataService, IJwtExtractionService jwtService, IJitProvisioningService jitService) =>
{
    // Extract JWT claims
    var (success, claims, errorMessage) = jwtService.ExtractClaims(context);
    if (!success || claims == null)
    {
        return Results.Unauthorized();
    }

    // Provision user if first login
    var user = await jitService.ProvisionUserAsync(claims);
    if (user == null)
    {
        return Results.BadRequest(new { error = "Could not provision user" });
    }

    // Get pending requests for user's organization
    if (string.IsNullOrEmpty(user.OrganizationId))
    {
        return Results.BadRequest(new { error = "User has no organization assigned" });
    }

    var pendingRequests = await dataService.GetPendingRequestsByOrgAsync(user.OrganizationId);
    
    return Results.Ok(pendingRequests);
})
.WithName("GetPendingRequests");

app.Run();
