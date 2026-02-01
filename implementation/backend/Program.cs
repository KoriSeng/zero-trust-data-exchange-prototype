using MongoDB.Driver;
using ZeroTrust.Backend.Data;
using ZeroTrust.Backend.Services;
using ZeroTrust.Backend.Models;
using Amazon.S3;
using Amazon.StepFunctions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;


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

builder.Services.AddScoped<IClaimsTransformation, JitProvisioningClaimsTransformation>();
System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler.DefaultMapInboundClaims = false;
Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler.DefaultMapInboundClaims = false;
builder.Services.AddAuthentication()
    .AddJwtBearer(options =>
    {
        // No signature validation here - handled at API Gateway
        var tokenHandler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        options.MapInboundClaims = false;
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var principal = context.Principal;
                if (principal == null)
                {
                    context.Fail("Missing principal");
                    return;
                }

                var dataService = context.HttpContext.RequestServices.GetRequiredService<IDataService>();

                var groups = principal.Claims
                    .Where(c => c.Type == "cognito:groups")
                    .Select(c => c.Value)
                    .ToList();

                if (groups.Count == 1 && groups[0].TrimStart().StartsWith("[") == true)
                {
                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(groups[0]);
                        if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            groups = doc.RootElement
                                .EnumerateArray()
                                .Where(e => e.ValueKind == System.Text.Json.JsonValueKind.String)
                                .Select(e => e.GetString())
                                .Where(s => !string.IsNullOrEmpty(s))
                                .ToList()!;
                        }
                    }
                    catch
                    {
                        // Ignore parsing errors and fall back to raw value
                    }
                }

                if (groups.Count == 0)
                {
                    context.Fail("Missing cognito:groups");
                    return;
                }

                var organization = await dataService.GetOrganizationByCognitoGroupAsync(groups[0]);
                if (organization == null)
                {
                    context.Fail("Organization not found for cognito:groups");
                }
            }
        };
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = false,
            ValidateIssuerSigningKey = false,
            NameClaimType = "sub",
            SignatureValidator = (token, parameters) => new Microsoft.IdentityModel.JsonWebTokens.JsonWebToken(token)           
        };
    });
builder.Services.AddAuthorizationBuilder()
    .AddDefaultPolicy("Authenticated", policy => 
        policy.RequireAuthenticatedUser()
        .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
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

app.UseAuthentication();
app.UseAuthorization();

// ============ Routes ============
// Health check
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
    .WithName("Health")
    .AllowAnonymous();


// Get current user information with roles and organization
// User is automatically JIT provisioned by JitProvisioningClaimsTransformation during authentication
app.MapGet("/me", async (HttpContext context, IDataService dataService) =>
{
    // User is already authenticated and provisioned
    var userId = context.User.FindFirst("user_id")?.Value;
    var userEmail = context.User.FindFirst("user_email")?.Value;
    var userDisplayName = context.User.FindFirst("user_display_name")?.Value;
    var organizationId = context.User.FindFirst("user_organization")?.Value;
    var organizationNameFromClaims = context.User.FindFirst("user_organization_name")?.Value;
    var identityProvider = context.User.FindFirst("identity_provider")?.Value;
    var status = context.User.FindFirst("user_status")?.Value;
    var createdAtClaim = context.User.FindFirst("user_created_at")?.Value;
    var lastAccessAtClaim = context.User.FindFirst("user_last_access_at")?.Value;

    if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(organizationId))
    {
        return Results.BadRequest(new { error = "User not properly provisioned" });
    }

    // Get organization information (fallback if not seeded into claims)
    string organizationName;
    if (!string.IsNullOrWhiteSpace(organizationNameFromClaims))
    {
        organizationName = organizationNameFromClaims;
    }
    else
    {
        var organization = await dataService.GetOrganizationByIdAsync(organizationId);
        if (organization == null)
        {
            return Results.BadRequest(new { error = "Organization not found for user" });
        }

        organizationName = organization.Name;
    }

    // Get user's roles
    var userRoles = await dataService.GetUserRolesAsync(userId);
    var roleNames = userRoles.Select(r => r.Name).ToList();

    // Build response
    var createdAt = DateTime.TryParse(createdAtClaim, out var createdAtParsed)
        ? createdAtParsed
        : DateTime.UtcNow;
    var lastAccessAt = DateTime.TryParse(lastAccessAtClaim, out var lastAccessAtParsed)
        ? lastAccessAtParsed
        : DateTime.UtcNow;
    var response = new CurrentUserResponse
    {
        UserId = userId,
        Email = userEmail ?? "unknown@example.com",
        DisplayName = userDisplayName ?? "Unknown User",
        IdentityProvider = identityProvider ?? "Unknown",
        OrganizationId = organizationId,
        OrganizationName = organizationName,
        Roles = roleNames,
        Status = status ?? "Unknown",
        CreatedAt = createdAt,
        LastAccessAt = lastAccessAt
    };

    return Results.Ok(response);
})
.WithName("GetCurrentUser")
.RequireAuthorization("Authenticated");

// Create new data access request
app.MapPost("/requests", async (HttpContext context, CreateDataAccessRequestDto request, IDataService dataService, IS3Service s3Service, IStepFunctionsService stepFunctionsService, ILogger<Program> logger) =>
{
    // User is already authenticated and provisioned
    var requesterId = context.User.FindFirst("user_id")?.Value;
    var requesterEmail = context.User.FindFirst("user_email")?.Value;
    var requesterOrg = context.User.FindFirst("user_organization")?.Value;

    if (string.IsNullOrEmpty(requesterId) || string.IsNullOrEmpty(requesterOrg))
    {
        return Results.BadRequest(new { error = "User not properly provisioned" });
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
        RequesterId = requesterId,
        RequesterEmail = requesterEmail ?? "unknown@example.com",
        RequesterOrg = requesterOrg ?? "UNKNOWN",
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
            { "requester_id", requesterId },
            { "requester_email", requesterEmail ?? "unknown@example.com" },
            { "requester_org", requesterOrg ?? "UNKNOWN" },
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
            requester_id = requesterId,
            requester_email = requesterEmail,
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
.WithName("CreateDataAccessRequest")
.RequireAuthorization("Authenticated");

// Get pending requests for user's organization
app.MapGet("/requests/pending", async (HttpContext context, IDataService dataService) =>
{
    // User is already authenticated and provisioned
    var organizationId = context.User.FindFirst("user_organization")?.Value;

    if (string.IsNullOrEmpty(organizationId))
    {
        return Results.BadRequest(new { error = "User has no organization assigned" });
    }

    var pendingRequests = await dataService.GetPendingRequestsByOrgAsync(organizationId);
    
    return Results.Ok(pendingRequests);
})
.WithName("GetPendingRequests")
.RequireAuthorization("Authenticated");

app.Run();
