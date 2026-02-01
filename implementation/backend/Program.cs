using MongoDB.Driver;
using ZeroTrust.Backend.Data;
using ZeroTrust.Backend.Services;
using ZeroTrust.Backend.Models;
using Amazon.S3;
using Amazon.StepFunctions;


var builder = WebApplication.CreateBuilder(args);

// ============ Configuration ============
var mongoConnectionString = builder.Configuration.GetConnectionString("MongoDB") 
    ?? "mongodb://localhost:27017";
var databaseName = builder.Configuration.GetValue<string>("MongoDb:DatabaseName") 
    ?? "zero_trust_db";

// AWS Configuration
var awsRegion = builder.Configuration["AWS:Region"] ?? "us-east-1";
var serviceUrl = builder.Configuration["AWS:ServiceUrl"];

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

// AWS Services (S3 and Step Functions)
if (!string.IsNullOrEmpty(serviceUrl))
{
    // LocalStack configuration
    var s3Config = new Amazon.S3.AmazonS3Config { ServiceURL = serviceUrl };
    var sfnConfig = new Amazon.StepFunctions.AmazonStepFunctionsConfig { ServiceURL = serviceUrl };
    
    builder.Services.AddSingleton<IAmazonS3>(sp => 
        new AmazonS3Client(new Amazon.Runtime.BasicAWSCredentials("test", "test"), s3Config)
    );
    builder.Services.AddSingleton<IAmazonStepFunctions>(sp => 
        new AmazonStepFunctionsClient(new Amazon.Runtime.BasicAWSCredentials("test", "test"), sfnConfig)
    );
}
else
{
    // AWS production configuration
    builder.Services.AddSingleton<IAmazonS3>(sp => new AmazonS3Client());
    builder.Services.AddSingleton<IAmazonStepFunctions>(sp => new AmazonStepFunctionsClient());
}

builder.Services.AddScoped<IS3Service, S3Service>();
builder.Services.AddScoped<IStepFunctionsService, StepFunctionsService>();

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

// Create new data access request
app.MapPost("/requests", async (HttpContext context, CreateDataAccessRequestDto request, IDataService dataService, IJwtExtractionService jwtService, IJitProvisioningService jitService, IS3Service s3Service, IStepFunctionsService stepFunctionsService, ILogger<Program> logger) =>
{
    // Extract JWT claims
    var (success, claims, errorMessage) = jwtService.ExtractClaims(context);
    if (!success || claims == null)
    {
        return Results.Unauthorized();
    }

    // Provision user if first login
    var requester = await jitService.ProvisionUserAsync(claims);
    if (requester == null)
    {
        return Results.BadRequest(new { error = "Could not provision requester" });
    }

    // Validate request
    if (string.IsNullOrWhiteSpace(request.Purpose))
    {
        return Results.BadRequest(new { error = "Purpose is required" });
    }

    if (request.ObjectKeys == null || request.ObjectKeys.Count == 0)
    {
        return Results.BadRequest(new { error = "At least one object key is required" });
    }

    // Check if objects exist in S3
    var dataBucket = context.RequestServices.GetRequiredService<IConfiguration>()["AWS:S3:DataBucket"] ?? "zero-trust-data";
    var objectsExist = await s3Service.ObjectsExistAsync(dataBucket, request.ObjectKeys);
    if (!objectsExist)
    {
        return Results.BadRequest(new { error = "One or more requested objects do not exist" });
    }

    // Create request in database
    var dataAccessRequest = new DataAccessRequest
    {
        Id = Guid.NewGuid().ToString(),
        RequestId = $"REQ-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}",
        RequesterId = requester.Id,
        RequesterEmail = requester.Email ?? "unknown@example.com",
        RequesterOrg = requester.OrganizationId ?? "UNKNOWN",
        DatasetId = request.DatasetId,
        DatasetName = request.DatasetName,
        ObjectKeys = request.ObjectKeys,
        Purpose = request.Purpose,
        DataOwnerOrg = request.DataOwnerOrg,
        Status = RequestStatus.Submitted,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        ExpiresAt = DateTime.UtcNow.AddDays(30)
    };

    // Save to database
    var saved = await dataService.CreateDataAccessRequestAsync(dataAccessRequest);
    if (!saved)
    {
        logger.LogError("Failed to save data access request {requestId}", dataAccessRequest.RequestId);
        return Results.StatusCode(StatusCodes.Status500InternalServerError);
    }

    // Start approval workflow
    string? workflowExecutionArn = null;
    try
    {
        var workflowContext = new Dictionary<string, object>
        {
            { "requester_id", requester.Id },
            { "requester_email", requester.Email ?? "unknown@example.com" },
            { "requester_org", requester.OrganizationId ?? "UNKNOWN" },
            { "dataset_id", request.DatasetId },
            { "purpose", request.Purpose },
            { "data_owner_org", request.DataOwnerOrg },
            { "object_keys", request.ObjectKeys }
        };

        workflowExecutionArn = await stepFunctionsService.StartApprovalWorkflowAsync(
            dataAccessRequest.RequestId, 
            workflowContext
        );

        // Update request with workflow ARN
        dataAccessRequest.WorkflowExecutionArn = workflowExecutionArn;
        dataAccessRequest.Status = RequestStatus.PendingOwnerApproval;
        await dataService.UpdateDataAccessRequestAsync(dataAccessRequest);

        logger.LogInformation("Started approval workflow for request {requestId}", dataAccessRequest.RequestId);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error starting approval workflow for request {requestId}", dataAccessRequest.RequestId);
        // Continue without workflow - request is still valid
    }

    // Upload request metadata to S3 audit trail
    try
    {
        var requestBucket = context.RequestServices.GetRequiredService<IConfiguration>()["AWS:S3:RequestsBucket"] ?? "zero-trust-requests";
        var metadata = System.Text.Json.JsonSerializer.Serialize(new
        {
            request_id = dataAccessRequest.RequestId,
            requester_id = requester.Id,
            requester_email = requester.Email,
            dataset_id = request.DatasetId,
            purpose = request.Purpose,
            created_at = DateTime.UtcNow,
            workflow_arn = workflowExecutionArn
        });

        await s3Service.UploadRequestMetadataAsync(requestBucket, dataAccessRequest.RequestId, metadata);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Could not upload request metadata for {requestId}", dataAccessRequest.RequestId);
    }

    var response = new DataAccessRequestResponse
    {
        Id = dataAccessRequest.Id,
        RequestId = dataAccessRequest.RequestId,
        Status = dataAccessRequest.Status.ToString(),
        WorkflowExecutionArn = workflowExecutionArn,
        CreatedAt = dataAccessRequest.CreatedAt,
        Message = "Request submitted successfully. Awaiting approval."
    };

    return Results.Created($"/requests/{dataAccessRequest.Id}", response);
})
.WithName("CreateDataAccessRequest");

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
