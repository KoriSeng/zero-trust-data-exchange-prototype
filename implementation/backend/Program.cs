using MongoDB.Driver;
using ZeroTrust.Backend.Data;
using ZeroTrust.Backend.Services;

var builder = WebApplication.CreateBuilder(args);

// ============ Configuration ============
var mongoConnectionString = builder.Configuration.GetConnectionString("MongoDB") 
    ?? "mongodb://localhost:27017";
var databaseName = builder.Configuration.GetValue<string>("MongoDb:DatabaseName") 
    ?? "zero_trust_db";

// ============ Services ============

// MongoDB
var mongoClient = new MongoClient(mongoConnectionString);
builder.Services.AddSingleton(mongoClient);
builder.Services.AddSingleton<IDataService>(sp => 
    new MongoDataService(mongoClient, databaseName)
);

// JWT and JIT provisioning services
builder.Services.AddSingleton<IJwtExtractionService, JwtExtractionService>();
builder.Services.AddScoped<IJitProvisioningService, JitProvisioningService>();

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

// Initialize sample data (for development only)
app.MapPost("/dev/initialize", async (IDataService dataService) =>
{
    try
    {
        // Create default roles if they don't exist
        var roles = await dataService.GetAllRolesAsync();
        if (roles.Count == 0)
        {
            var requesterRole = new ZeroTrust.Backend.Models.Role
            {
                Id = Guid.NewGuid().ToString(),
                Name = ZeroTrust.Backend.Models.RoleNames.Requester
            };

            var dataOwnerRole = new ZeroTrust.Backend.Models.Role
            {
                Id = Guid.NewGuid().ToString(),
                Name = ZeroTrust.Backend.Models.RoleNames.DataOwner
            };

            var adminRole = new ZeroTrust.Backend.Models.Role
            {
                Id = Guid.NewGuid().ToString(),
                Name = ZeroTrust.Backend.Models.RoleNames.Admin
            };

            var auditorRole = new ZeroTrust.Backend.Models.Role
            {
                Id = Guid.NewGuid().ToString(),
                Name = ZeroTrust.Backend.Models.RoleNames.Auditor
            };

            await dataService.PutRoleAsync(requesterRole);
            await dataService.PutRoleAsync(dataOwnerRole);
            await dataService.PutRoleAsync(adminRole);
            await dataService.PutRoleAsync(auditorRole);
        }

        return Results.Ok(new { message = "Database initialized" });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("Initialize");

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
    var pendingRequests = await dataService.GetPendingRequestsByOrgAsync(user.OrganizationId);
    
    return Results.Ok(pendingRequests);
})
.WithName("GetPendingRequests");

app.Run();
