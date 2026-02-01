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
