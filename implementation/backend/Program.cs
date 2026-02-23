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

// Serialize enums as strings in all JSON responses
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter()
    );
});

// ============ Build App ============
var app = builder.Build();

// ============ Middleware ============
//app.UseHttpsRedirection();

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

    if (string.IsNullOrWhiteSpace(request.DataOwnerOrg))
    {
        return Results.BadRequest(new { error = "DataOwnerOrg is required" });
    }

    // Resolve DataOwnerOrg short name (e.g. "ORG-B") to the internal org ID
    var dataOwnerOrg = await dataService.GetOrganizationByShortNameAsync(request.DataOwnerOrg);
    if (dataOwnerOrg == null)
    {
        return Results.BadRequest(new { error = $"Organization '{request.DataOwnerOrg}' not found" });
    }

    // Check if objects exist in S3 — warn only; the hard check occurs at redemption
    // when pre-signed URLs are generated. In a local/test environment S3 may not be
    // seeded yet and we do not want to block submission.
    var dataBucket = context.RequestServices.GetRequiredService<IConfiguration>()["AWS:S3:DataBucket"] ?? "zero-trust-data";
    var objectsExist = await s3Service.ObjectsExistAsync(dataBucket, request.ObjectKeys);
    if (!objectsExist)
    {
        logger.LogWarning(
            "Submitted request for objects that were not found in S3 bucket {bucket}: {keys}",
            dataBucket,
            string.Join(", ", request.ObjectKeys)
        );
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
        DataOwnerOrg = dataOwnerOrg.Id,
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
            { "data_owner_org", dataOwnerOrg.Id },
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

    // Emit audit event
    await dataService.CreateAuditEventAsync(new AuditEvent
    {
        EventType = "REQUEST_SUBMITTED",
        ActorId = requesterId,
        OrganizationId = requesterOrg,
        RequestId = dataAccessRequest.RequestId,
        DatasetId = request.DatasetId,
        Description = $"Data access request {dataAccessRequest.RequestId} submitted for dataset {request.DatasetId}",
        Result = AuditEventResult.Success
    });

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

// Get current user's own requests
app.MapGet("/requests/my", async (HttpContext context, IDataService dataService) =>
{
    var requesterId = context.User.FindFirst("user_id")?.Value;

    if (string.IsNullOrEmpty(requesterId))
    {
        return Results.BadRequest(new { error = "User not properly provisioned" });
    }

    var requests = await dataService.GetRequestsByRequesterAsync(requesterId);
    return Results.Ok(requests);
})
.WithName("GetMyRequests")
.RequireAuthorization("Authenticated");

// Get a specific request by internal UUID
app.MapGet("/requests/{id}", async (string id, HttpContext context, IDataService dataService) =>
{
    var callerId = context.User.FindFirst("user_id")?.Value;
    var callerOrg = context.User.FindFirst("user_organization")?.Value;

    var request = await dataService.GetRequestByIdAsync(id);
    if (request == null)
    {
        return Results.NotFound(new { error = "Request not found" });
    }

    // Only the requester or a member of the data owner org may view the request
    if (request.RequesterId != callerId && request.DataOwnerOrg != callerOrg)
    {
        return Results.Forbid();
    }

    return Results.Ok(request);
})
.WithName("GetRequestById")
.RequireAuthorization("Authenticated");

// Get audit trail for a specific request
app.MapGet("/requests/{id}/audit", async (string id, HttpContext context, IDataService dataService) =>
{
    var callerId = context.User.FindFirst("user_id")?.Value;
    var callerOrg = context.User.FindFirst("user_organization")?.Value;

    var request = await dataService.GetRequestByIdAsync(id);
    if (request == null)
    {
        return Results.NotFound(new { error = "Request not found" });
    }

    // Only the requester or a member of the data owner org may view the audit trail
    if (request.RequesterId != callerId && request.DataOwnerOrg != callerOrg)
    {
        return Results.Forbid();
    }

    var events = await dataService.GetAuditEventsByRequestAsync(request.RequestId);
    return Results.Ok(events);
})
.WithName("GetRequestAudit")
.RequireAuthorization("Authenticated");

// Approve a pending request (data owner only) — generates an OTP for the requester
app.MapPost("/requests/{id}/approve", async (string id, ApproveRequestDto dto, HttpContext context, IDataService dataService, ILogger<Program> logger) =>
{
    var approverId = context.User.FindFirst("user_id")?.Value;
    var approverOrg = context.User.FindFirst("user_organization")?.Value;

    if (string.IsNullOrEmpty(approverId) || string.IsNullOrEmpty(approverOrg))
    {
        return Results.BadRequest(new { error = "User not properly provisioned" });
    }

    var request = await dataService.GetRequestByIdAsync(id);
    if (request == null)
    {
        return Results.NotFound(new { error = "Request not found" });
    }

    // Only the data owner org may approve
    if (request.DataOwnerOrg != approverOrg)
    {
        return Results.Forbid();
    }

    if (request.Status != RequestStatus.PendingOwnerApproval)
    {
        return Results.BadRequest(new { error = $"Request is not awaiting approval (current status: {request.Status})" });
    }

    // Generate a 6-digit OTP using a cryptographically secure random number
    var rawOtp = System.Security.Cryptography.RandomNumberGenerator.GetInt32(100000, 999999).ToString();
    var otpHash = Convert.ToHexString(
        System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawOtp))
    );
    var otpExpiry = DateTime.UtcNow.AddMinutes(15);

    request.Status = RequestStatus.OtpSent;
    request.ApprovedAt = DateTime.UtcNow;
    request.OtpToken = otpHash;
    request.OtpExpiresAt = otpExpiry;
    request.UpdatedAt = DateTime.UtcNow;

    await dataService.UpdateDataAccessRequestAsync(request);

    await dataService.CreateAuditEventAsync(new AuditEvent
    {
        EventType = "REQUEST_APPROVED",
        ActorId = approverId,
        OrganizationId = approverOrg,
        RequestId = request.RequestId,
        DatasetId = request.DatasetId,
        Description = $"Request {request.RequestId} approved by {approverId}; OTP issued",
        Result = AuditEventResult.Success
    });

    logger.LogInformation("Request {requestId} approved by {approverId}; OTP expires at {expiry}",
        request.RequestId, approverId, otpExpiry);

    return Results.Ok(new
    {
        requestId = request.RequestId,
        status = request.Status.ToString(),
        otp = rawOtp,               // In production this would be e-mailed, not returned here
        otpExpiresAt = otpExpiry,
        message = "Request approved. Share the OTP with the requester via secure channel."
    });
})
.WithName("ApproveRequest")
.RequireAuthorization("Authenticated");

// Deny a pending request (data owner only)
app.MapPost("/requests/{id}/deny", async (string id, DenyRequestDto dto, HttpContext context, IDataService dataService, ILogger<Program> logger) =>
{
    var denierId = context.User.FindFirst("user_id")?.Value;
    var denierOrg = context.User.FindFirst("user_organization")?.Value;

    if (string.IsNullOrEmpty(denierId) || string.IsNullOrEmpty(denierOrg))
    {
        return Results.BadRequest(new { error = "User not properly provisioned" });
    }

    var request = await dataService.GetRequestByIdAsync(id);
    if (request == null)
    {
        return Results.NotFound(new { error = "Request not found" });
    }

    if (request.DataOwnerOrg != denierOrg)
    {
        return Results.Forbid();
    }

    if (request.Status != RequestStatus.PendingOwnerApproval)
    {
        return Results.BadRequest(new { error = $"Request is not awaiting approval (current status: {request.Status})" });
    }

    request.Status = RequestStatus.Denied;
    request.DeniedBy = denierId;
    request.DeniedAt = DateTime.UtcNow;
    request.DenialReason = dto.Reason;
    request.UpdatedAt = DateTime.UtcNow;

    await dataService.UpdateDataAccessRequestAsync(request);

    await dataService.CreateAuditEventAsync(new AuditEvent
    {
        EventType = "REQUEST_DENIED",
        ActorId = denierId,
        OrganizationId = denierOrg,
        RequestId = request.RequestId,
        DatasetId = request.DatasetId,
        Description = $"Request {request.RequestId} denied by {denierId}: {dto.Reason ?? "no reason given"}",
        Result = AuditEventResult.Success
    });

    logger.LogInformation("Request {requestId} denied by {denierId}", request.RequestId, denierId);

    return Results.Ok(new { requestId = request.RequestId, status = request.Status.ToString() });
})
.WithName("DenyRequest")
.RequireAuthorization("Authenticated");

// Redeem OTP — validates one-time password and returns pre-signed S3 URLs
app.MapPost("/requests/{id}/redeem", async (string id, RedeemRequestDto dto, HttpContext context, IDataService dataService, IS3Service s3Service, ILogger<Program> logger) =>
{
    var redeemerId = context.User.FindFirst("user_id")?.Value;

    if (string.IsNullOrEmpty(redeemerId))
    {
        return Results.BadRequest(new { error = "User not properly provisioned" });
    }

    if (string.IsNullOrWhiteSpace(dto.Otp))
    {
        return Results.BadRequest(new { error = "OTP is required" });
    }

    var request = await dataService.GetRequestByIdAsync(id);
    if (request == null)
    {
        return Results.NotFound(new { error = "Request not found" });
    }

    // Only the original requester may redeem
    if (request.RequesterId != redeemerId)
    {
        return Results.Forbid();
    }

    if (request.Status != RequestStatus.OtpSent)
    {
        return Results.BadRequest(new { error = $"Request is not in a redeemable state (current status: {request.Status})" });
    }

    // Validate OTP expiry
    if (request.OtpExpiresAt == null || DateTime.UtcNow > request.OtpExpiresAt)
    {
        await dataService.CreateAuditEventAsync(new AuditEvent
        {
            EventType = "OTP_EXPIRED",
            ActorId = redeemerId,
            RequestId = request.RequestId,
            DatasetId = request.DatasetId,
            Description = $"OTP redemption failed for request {request.RequestId}: OTP expired",
            Result = AuditEventResult.Failure
        });
        return Results.BadRequest(new { error = "OTP has expired" });
    }

    // Validate OTP hash
    var submittedHash = Convert.ToHexString(
        System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(dto.Otp.Trim()))
    );
    if (!string.Equals(request.OtpToken, submittedHash, StringComparison.Ordinal))
    {
        await dataService.CreateAuditEventAsync(new AuditEvent
        {
            EventType = "OTP_INVALID",
            ActorId = redeemerId,
            RequestId = request.RequestId,
            DatasetId = request.DatasetId,
            Description = $"OTP redemption failed for request {request.RequestId}: invalid OTP",
            Result = AuditEventResult.Failure
        });
        return Results.BadRequest(new { error = "Invalid OTP" });
    }

    // Generate pre-signed URLs (15-minute expiry)
    var dataBucket = context.RequestServices.GetRequiredService<IConfiguration>()["AWS:S3:DataBucket"] ?? "zero-trust-data";
    var urlExpiry = TimeSpan.FromMinutes(15);
    var presignedUrls = new Dictionary<string, string>();
    foreach (var key in request.ObjectKeys)
    {
        try
        {
            presignedUrls[key] = await s3Service.GeneratePresignedUrlAsync(dataBucket, key, urlExpiry);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate pre-signed URL for {key}", key);
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    var urlExpiresAt = DateTime.UtcNow.Add(urlExpiry);

    request.Status = RequestStatus.Redeemed;
    request.RedeemedAt = DateTime.UtcNow;
    request.OtpToken = null;   // Invalidate OTP after use
    request.PresignedUrlExpiresAt = urlExpiresAt;
    request.UpdatedAt = DateTime.UtcNow;

    await dataService.UpdateDataAccessRequestAsync(request);

    await dataService.CreateAuditEventAsync(new AuditEvent
    {
        EventType = "OTP_REDEEMED",
        ActorId = redeemerId,
        RequestId = request.RequestId,
        DatasetId = request.DatasetId,
        Description = $"OTP successfully redeemed for request {request.RequestId}; {presignedUrls.Count} pre-signed URL(s) issued",
        Result = AuditEventResult.Success,
        Metadata = new Dictionary<string, object>
        {
            { "object_count", presignedUrls.Count },
            { "url_expires_at", urlExpiresAt.ToString("o") }
        }
    });

    logger.LogInformation("OTP redeemed for request {requestId} by {redeemerId}", request.RequestId, redeemerId);

    return Results.Ok(new
    {
        requestId = request.RequestId,
        status = request.Status.ToString(),
        presignedUrls,
        expiresAt = urlExpiresAt,
        message = "Access granted. URLs expire in 15 minutes."
    });
})
.WithName("RedeemRequest")
.RequireAuthorization("Authenticated");

app.Run();
