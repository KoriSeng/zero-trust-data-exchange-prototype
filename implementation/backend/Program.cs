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
// Creates comprehensive seed data with FIXED USER SUBJECTS for k6 testing
// WARNING: This endpoint DROPS all collections before re-seeding
app.MapPost("/dev/initialize", async (IDataService dataService, IMongoClient mongoClient, ILogger<Program> logger) =>
{
    logger.LogInformation("Starting database seed initialization...");
    
    // Drop all collections
    var database = mongoClient.GetDatabase(databaseName);
    var collections = await database.ListCollectionNamesAsync();
    foreach (var collectionName in collections.ToList())
    {
        logger.LogInformation($"Dropping collection: {collectionName}");
        await database.DropCollectionAsync(collectionName);
    }
    logger.LogInformation("✓ All collections dropped\n");
    
    // Create roles
    logger.LogInformation("Creating roles...");
    var roles = new[]
    {
        new Role { Id = Guid.NewGuid().ToString(), Name = RoleNames.Requester, CreatedAt = DateTime.UtcNow },
        new Role { Id = Guid.NewGuid().ToString(), Name = RoleNames.DataOwner, CreatedAt = DateTime.UtcNow },
        new Role { Id = Guid.NewGuid().ToString(), Name = RoleNames.Admin, CreatedAt = DateTime.UtcNow },
        new Role { Id = Guid.NewGuid().ToString(), Name = RoleNames.Auditor, CreatedAt = DateTime.UtcNow }
    };
    foreach (var role in roles)
    {
        await dataService.PutRoleAsync(role);
    }
    logger.LogInformation("✓ Created 4 roles");
    
    // Create organizations
    logger.LogInformation("Creating organizations...");
    var orgAId = Guid.NewGuid().ToString();
    var orgBId = Guid.NewGuid().ToString();

    var organizations = new[]
    {
        new Organization
        {
            Id = orgAId,
            Name = "Organization A (Data Custodian)",
            CognitoIdpName = "IDP-A",
            CognitoGroupName = "ap-southeast-1_xxxxx_IDP-A",
            IdpIssuer = "http://localhost:9001",
            Status = OrganizationStatus.Active,
            OnboardedAt = DateTime.UtcNow
        },
        new Organization
        {
            Id = orgBId,
            Name = "Organization B (Data Readers)",
            CognitoIdpName = "IDP-B",
            CognitoGroupName = "ap-southeast-1_yyyyy_IDP-B",
            IdpIssuer = "http://localhost:9002",
            Status = OrganizationStatus.Active,
            OnboardedAt = DateTime.UtcNow
        }
    };
    foreach (var org in organizations)
    {
        await dataService.PutOrganizationAsync(org);
    }
    logger.LogInformation("✓ Created 3 organizations");
    
    // Get role IDs for user assignment
    var requesterRole = await dataService.GetRoleByNameAsync(RoleNames.Requester);
    var dataOwnerRole = await dataService.GetRoleByNameAsync(RoleNames.DataOwner);
    var adminRole = await dataService.GetRoleByNameAsync(RoleNames.Admin);
    var auditorRole = await dataService.GetRoleByNameAsync(RoleNames.Auditor);
    
    // Create users
    logger.LogInformation("Creating test users with FIXED SUBJECTS...");
    var users = new[]
    {
        // IDP-A Requesters
        (user: new User
        {
            Id = Guid.NewGuid().ToString(),
            Sub = "IDP-A_A-001",
            CognitoUsername = "IDP-A_a.alex",
            FederatedSub = "A-001",
            IdentityProvider = "IDP-A",
            Issuer = "http://localhost:9001",
            Email = "a.alex@org-a.example.com",
            DisplayName = "Alex Kim",
            OrganizationId = orgAId,
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow,
            LastAccessAt = DateTime.UtcNow
        }, roleId: requesterRole?.Id),
        
        (user: new User
        {
            Id = Guid.NewGuid().ToString(),
            Sub = "IDP-A_A-002",
            CognitoUsername = "IDP-A_a.sam",
            FederatedSub = "A-002",
            IdentityProvider = "IDP-A",
            Issuer = "http://localhost:9001",
            Email = "a.sam@org-a.example.com",
            DisplayName = "Sam Lee",
            OrganizationId = orgAId,
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow,
            LastAccessAt = DateTime.UtcNow
        }, roleId: requesterRole?.Id),
        
        // IDP-B Data Owners
        (user: new User
        {
            Id = Guid.NewGuid().ToString(),
            Sub = "IDP-B_B-001",
            CognitoUsername = "IDP-B_b.alex",
            FederatedSub = "B-001",
            IdentityProvider = "IDP-B",
            Issuer = "http://localhost:9002",
            Email = "b.alex@org-b.example.com",
            DisplayName = "Alex Kim", // Collision test: same name as IDP-A user
            OrganizationId = orgBId,
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow,
            LastAccessAt = DateTime.UtcNow
        }, roleId: dataOwnerRole?.Id),
        
        (user: new User
        {
            Id = Guid.NewGuid().ToString(),
            Sub = "IDP-B_B-002",
            CognitoUsername = "IDP-B_b.jamie",
            FederatedSub = "B-002",
            IdentityProvider = "IDP-B",
            Issuer = "http://localhost:9002",
            Email = "b.jamie@org-b.example.com",
            DisplayName = "Jamie Tan",
            OrganizationId = orgBId,
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow,
            LastAccessAt = DateTime.UtcNow
        }, roleId: dataOwnerRole?.Id),
        
        // Admin
        (user: new User
        {
            Id = Guid.NewGuid().ToString(),
            Sub = "IDP-A_ADMIN-001",
            CognitoUsername = "IDP-A_admin",
            FederatedSub = "ADMIN-001",
            IdentityProvider = "IDP-A",
            Issuer = "http://localhost:9001",
            Email = "admin@org-custodian.example.com",
            DisplayName = "System Administrator",
            OrganizationId = orgAId,
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow,
            LastAccessAt = DateTime.UtcNow
        }, roleId: adminRole?.Id)
    };
    
    foreach (var (user, roleId) in users)
    {
        await dataService.PutUserAsync(user);
        if (roleId != null)
        {
            await dataService.AssignRoleToUserAsync(user.Id, roleId, "system");
        }
    }
    
    // Admin gets auditor role too
    if (adminRole?.Id != null && auditorRole?.Id != null)
    {
        await dataService.AssignRoleToUserAsync(users[4].user.Id, auditorRole.Id, "system");
    }
    logger.LogInformation("✓ Created 5 users with assigned roles");
    
    // Create user agreement
    logger.LogInformation("Creating user agreements...");
    var agreement = new UserAgreement
    {
        AgreementId = "AGREEMENT-001",
        Version = 1,
        Name = "Data Access Terms & Conditions",
        Content = @"# Data Access Agreement

## 1. Purpose
This agreement governs access to sensitive datasets managed by the Data Custodian.

## 2. Terms
- Data shall be used solely for approved research purposes
- Data shall not be shared with unauthorized parties
- Data shall be deleted after the access period expires
- Any breach shall be reported immediately

## 3. Compliance
Requester agrees to comply with all applicable data protection regulations.

## 4. Audit and Monitoring
All access is logged and subject to audit.

Effective Date: " + DateTime.UtcNow.ToString("yyyy-MM-dd"),
        EffectiveDate = DateTime.UtcNow,
        Status = AgreementStatus.Active,
        CreatedAt = DateTime.UtcNow,
        CreatedBy = "system"
    };
    await dataService.PutAgreementAsync(agreement);
    logger.LogInformation("✓ Created 1 user agreement");
    
    // Create sample request
    logger.LogInformation("Creating sample requests...");
    var request = new DataAccessRequest
    {
        Id = Guid.NewGuid().ToString(),
        RequestId = "REQ-2024-001",
        RequesterId = "IDP-A_A-001",
        RequesterEmail = "a.alex@org-a.example.com",
        RequesterOrg = "ORG-A",
        DataOwnerOrg = "ORG-B",
        DatasetId = "dataset-genomic-2024",
        DatasetName = "Genomic Research Dataset 2024",
        ObjectKeys = new List<string> { "samples/data-001.csv", "samples/data-002.csv" },
        Purpose = "Machine learning model training",
        Status = RequestStatus.Submitted,
        UserAgreementId = "AGREEMENT-001",
        UserAgreementVersion = 1,
        AgreementContent = agreement.Content,
        CreatedAt = DateTime.UtcNow.AddDays(-2),
        UpdatedAt = DateTime.UtcNow.AddDays(-2),
        ExpiresAt = DateTime.UtcNow.AddDays(5)
    };
    await dataService.PutRequestAsync(request);
    logger.LogInformation("✓ Created 1 sample request");
    
    logger.LogInformation("\n========================================");
    logger.LogInformation("Database initialized successfully!");
    logger.LogInformation("========================================");
    logger.LogInformation("Fixed Test Users for k6:");
    logger.LogInformation("  IDP-A: IDP-A_A-001 (a.alex), IDP-A_A-002 (a.sam)");
    logger.LogInformation("  IDP-B: IDP-B_B-001 (b.alex), IDP-B_B-002 (b.jamie)");
    logger.LogInformation("  Admin: IDP-A_ADMIN-001 (admin)");
    logger.LogInformation("Collision Test: Both IDP-A_A-001 and IDP-B_B-001 are 'Alex Kim'");
    logger.LogInformation("========================================\n");
    
    return Results.Ok();
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
    
    return Results.Ok();
})
.WithName("GetPendingRequests");

app.Run();
