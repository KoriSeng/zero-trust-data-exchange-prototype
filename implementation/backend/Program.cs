using MongoDB.Driver;
using ZeroTrust.Backend.Data;
using ZeroTrust.Backend.Services;
using ZeroTrust.Backend.Models;
using Amazon.S3;
using Amazon.StepFunctions;
using Amazon.Lambda.AspNetCoreServer.Hosting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;


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

                var organizations = await dataService.GetAllOrganizationsAsync();
                var organization = CognitoGroupMapper.FindMatchingOrganization(organizations, groups[0]);
                if (organization == null)
                {
                    context.Fail($"Organization not found for cognito:groups ({string.Join(", ", groups)})");
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
builder.Services.AddScoped<IOtpService, OtpService>();

// Database seeder hosted service — skipped in Lambda to prevent wiping DB on cold starts
if (builder.Configuration.GetValue<bool>("SeedDatabase", false) ||
    builder.Environment.IsDevelopment())
{
    builder.Services.AddHostedService<DatabaseSeederHostedService>();
}

// Serialize enums as strings in all JSON responses
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter()
    );
});

// Lambda hosting — active only when running inside Lambda (LAMBDA_TASK_ROOT env var present)
// No-op when running locally with Kestrel
builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);

// CORS — required when the SPA and API run on different origins (local dev, or separate domains)
var allowedOrigins = (builder.Configuration["Cors:AllowedOrigins"] ?? "http://localhost:5173")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()
    )
);

// ============ Build App ============
var app = builder.Build();

// ============ Middleware ============
//app.UseHttpsRedirection();

app.UseCors();
app.UseAuthorization();

// ============ Routes ============
var otpSendCooldown = TimeSpan.FromSeconds(30);
var otpSendWindow = TimeSpan.FromMinutes(10);
const int otpSendMaxPerWindow = 5;

// Health check
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
    .WithName("Health")
    .AllowAnonymous();


// Get current user information with roles and organization
// User is automatically JIT provisioned by JitProvisioningClaimsTransformation during authentication
app.MapGet("/me", async (HttpContext context, IDataService dataService) =>
{
    var meLogger = context.RequestServices
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("MeEndpoint");
    var claimsDump = string.Join(
        "; ",
        context.User.Claims.Select(c => $"{c.Type}={c.Value}"));
    var meTraceMessage =
        $"/me claims dump: authType={context.User.Identity?.AuthenticationType ?? "null"}, " +
        $"isAuthenticated={context.User.Identity?.IsAuthenticated ?? false}, " +
        $"claimCount={context.User.Claims.Count()}, claims=[{claimsDump}]";
    meLogger.LogWarning(meTraceMessage);
    Console.WriteLine(meTraceMessage);

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

    if (string.IsNullOrEmpty(userId))
    {
        return Results.BadRequest(new
        {
            error = "User not properly provisioned",
            debug = new
            {
                authType = context.User.Identity?.AuthenticationType ?? "null",
                isAuthenticated = context.User.Identity?.IsAuthenticated ?? false,
                claimCount = context.User.Claims.Count(),
                claims = context.User.Claims.Select(c => new { c.Type, c.Value }).ToList()
            }
        });
    }

    if (string.IsNullOrEmpty(organizationId))
    {
        return Results.BadRequest(new
        {
            error = "User not properly provisioned",
            debug = new
            {
                authType = context.User.Identity?.AuthenticationType ?? "null",
                isAuthenticated = context.User.Identity?.IsAuthenticated ?? false,
                claimCount = context.User.Claims.Count(),
                identityProvider,
                organizationId,
                claims = context.User.Claims.Select(c => new { c.Type, c.Value }).ToList()
            }
        });
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

// List published datasets for explorer UI.
app.MapGet("/datasets", async (IDataService dataService, IS3Service s3Service, IConfiguration configuration, ILogger<Program> logger) =>
{
    var items = await dataService.GetPublishedDatasetCatalogItemsAsync();
    var dataBucket = configuration["AWS:S3:DataBucket"] ?? "zero-trust-data";

    var payload = new List<object>(items.Count);
    foreach (var item in items)
    {
        string? thumbnailUrl = null;
        if (!string.IsNullOrWhiteSpace(item.ThumbnailObjectKey))
        {
            try
            {
                thumbnailUrl = await s3Service.GeneratePresignedUrlAsync(
                    dataBucket,
                    item.ThumbnailObjectKey!,
                    TimeSpan.FromHours(6));
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Could not generate thumbnail URL for dataset {datasetId}",
                    item.DatasetId);
            }
        }

        payload.Add(new
        {
            id = item.Id,
            datasetId = item.DatasetId,
            name = item.Name,
            summary = item.Summary,
            dataOwnerOrg = item.DataOwnerOrgShortName,
            dataOwnerOrgName = item.DataOwnerOrgName,
            objectKeys = item.ObjectKeys,
            tags = item.Tags,
            recordCount = item.RecordCount,
            lastUpdatedAt = item.LastUpdatedAt,
            thumbnailUrl
        });
    }

    return Results.Ok(payload);
})
.WithName("ListDatasets")
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
    dataAccessRequest.Status = RequestStatus.PendingOwnerApproval;
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

        dataAccessRequest.WorkflowExecutionArn = workflowExecutionArn;
        logger.LogInformation("Started approval workflow for request {requestId}", dataAccessRequest.RequestId);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error starting approval workflow for request {requestId}", dataAccessRequest.RequestId);
        // Continue without workflow - OTP approval is enforced at API layer.
    }

    await dataService.UpdateDataAccessRequestAsync(dataAccessRequest);

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

// Send OTP to data owner email before approval (data owner only)
app.MapPost("/requests/{id}/otp/send", async (string id, HttpContext context, IDataService dataService, IOtpService otpService, ILogger<Program> logger) =>
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

    if (request.DataOwnerOrg != approverOrg)
    {
        return Results.Forbid();
    }

    if (request.Status != RequestStatus.PendingOwnerApproval && request.Status != RequestStatus.OtpSent)
    {
        return Results.BadRequest(new { error = $"Request cannot issue OTP in current state: {request.Status}" });
    }

    var now = DateTime.UtcNow;
    var latestOtpRecord = await dataService.GetLatestOtpRecordAsync(request.RequestId);
    if (latestOtpRecord is not null)
    {
        var cooldownRemaining = otpSendCooldown - (now - latestOtpRecord.CreatedAt);
        if (cooldownRemaining > TimeSpan.Zero)
        {
            var retryAfterSeconds = Math.Max(1, (int)Math.Ceiling(cooldownRemaining.TotalSeconds));
            return Results.Json(
                new
                {
                    error = "OTP was sent recently. Please wait before requesting a new code.",
                    retryAfterSeconds
                },
                statusCode: StatusCodes.Status429TooManyRequests);
        }
    }

    var sendsInWindow = await dataService.CountOtpRecordsSinceAsync(request.RequestId, now - otpSendWindow);
    if (sendsInWindow >= otpSendMaxPerWindow)
    {
        return Results.Json(
            new
            {
                error = $"OTP send limit reached ({otpSendMaxPerWindow} per {(int)otpSendWindow.TotalMinutes} minutes). Try again later.",
                retryAfterSeconds = (int)otpSendWindow.TotalSeconds
            },
            statusCode: StatusCodes.Status429TooManyRequests);
    }

    var ownerOrg = await dataService.GetOrganizationByIdAsync(approverOrg);
    if (string.IsNullOrWhiteSpace(ownerOrg?.ContactEmail))
    {
        return Results.Problem("Data owner contact email is not configured", statusCode: StatusCodes.Status500InternalServerError);
    }

    var otpPreview = await otpService.GenerateAndSendOtpAsync(request.RequestId, ownerOrg.ContactEmail);

    request.Status = RequestStatus.OtpSent;
    request.OtpExpiresAt = DateTime.UtcNow.AddMinutes(10);
    request.UpdatedAt = DateTime.UtcNow;
    await dataService.UpdateDataAccessRequestAsync(request);

    logger.LogInformation("OTP sent for request {requestId} by data owner {approverId}", request.RequestId, approverId);

    return Results.Ok(new
    {
        requestId = request.RequestId,
        status = request.Status.ToString(),
        message = "OTP generated in debug mode (email delivery is simulated)",
        debugEmail = new
        {
            to = otpPreview.RecipientEmail,
            subject = otpPreview.Subject,
            textBody = otpPreview.TextBody,
            otpCode = otpPreview.OtpCode,
            expiresAt = otpPreview.ExpiresAt
        }
    });
})
.WithName("SendRequestOtp")
.RequireAuthorization("Authenticated");

// Approve a pending request (data owner only) — OTP must be validated first
app.MapPost("/requests/{id}/approve", async (string id, ApproveRequestDto dto, HttpContext context, IDataService dataService, IOtpService otpService, ILogger<Program> logger) =>
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

    if (request.DataOwnerOrg != approverOrg)
    {
        return Results.Forbid();
    }

    if (request.Status == RequestStatus.Approved)
    {
        return Results.BadRequest(new { error = "Request is already approved." });
    }

    if (request.Status != RequestStatus.PendingOwnerApproval &&
        request.Status != RequestStatus.OtpSent)
    {
        return Results.BadRequest(new { error = $"Request is not in an approvable state (current status: {request.Status})" });
    }

    if (string.IsNullOrWhiteSpace(dto.ApprovalCode))
    {
        return Results.BadRequest(new { error = "approvalCode is required" });
    }

    var otpResult = await otpService.ValidateOtpAsync(request.RequestId, dto.ApprovalCode);
    if (otpResult != OtpValidationResult.Success)
    {
        return otpResult switch
        {
            OtpValidationResult.Expired => Results.BadRequest(new { error = "OTP has expired. Request a new code." }),
            OtpValidationResult.MaxAttemptsExceeded => Results.Json(new { error = "Too many failed attempts. Request a new code." }, statusCode: StatusCodes.Status429TooManyRequests),
            OtpValidationResult.AlreadyUsed => Results.BadRequest(new { error = "OTP has already been used." }),
            OtpValidationResult.NotFound => Results.BadRequest(new { error = "No OTP found. Request a code first." }),
            _ => Results.BadRequest(new { error = "Invalid OTP code." })
        };
    }

    request.Status = RequestStatus.Approved;
    request.ApprovedAt = DateTime.UtcNow;
    request.ApprovedBy = approverId;
    request.ApprovalComments = dto.Comments;
    request.UpdatedAt = DateTime.UtcNow;
    request.OtpToken = null;
    request.OtpExpiresAt = null;
    await dataService.UpdateDataAccessRequestAsync(request);

    await dataService.CreateAuditEventAsync(new AuditEvent
    {
        EventType = "REQUEST_APPROVED",
        ActorId = approverId,
        OrganizationId = approverOrg,
        RequestId = request.RequestId,
        DatasetId = request.DatasetId,
        Description = $"Request {request.RequestId} approved by {approverId} after OTP validation",
        Result = AuditEventResult.Success
    });

    logger.LogInformation("Request {requestId} approved by {approverId} after OTP validation", request.RequestId, approverId);

    return Results.Ok(new
    {
        requestId = request.RequestId,
        status = request.Status.ToString(),
        message = "Request approved"
    });
})
.WithName("ApproveRequest")
.RequireAuthorization("Authenticated");

// Deny a pending request (data owner only) - OTP is intentionally not required for denials.
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

    if (request.Status != RequestStatus.PendingOwnerApproval && request.Status != RequestStatus.OtpSent)
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

// Redeem approved request and return pre-signed S3 URLs for the requester.
app.MapPost("/requests/{id}/redeem", async (string id, HttpContext context, IDataService dataService, IS3Service s3Service, ILogger<Program> logger) =>
{
    var redeemerId = context.User.FindFirst("user_id")?.Value;

    if (string.IsNullOrEmpty(redeemerId))
    {
        return Results.BadRequest(new { error = "User not properly provisioned" });
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

    if (request.Status != RequestStatus.Approved)
    {
        return Results.BadRequest(new { error = $"Request is not in a redeemable state (current status: {request.Status})" });
    }

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
    request.PresignedUrlExpiresAt = urlExpiresAt;
    request.UpdatedAt = DateTime.UtcNow;

    await dataService.UpdateDataAccessRequestAsync(request);

    await dataService.CreateAuditEventAsync(new AuditEvent
    {
        EventType = "REQUEST_REDEEMED",
        ActorId = redeemerId,
        RequestId = request.RequestId,
        DatasetId = request.DatasetId,
        Description = $"Approved request {request.RequestId} redeemed; {presignedUrls.Count} pre-signed URL(s) issued",
        Result = AuditEventResult.Success,
        Metadata = new Dictionary<string, object>
        {
            { "object_count", presignedUrls.Count },
            { "url_expires_at", urlExpiresAt.ToString("o") }
        }
    });

    logger.LogInformation("Request {requestId} redeemed by {redeemerId}", request.RequestId, redeemerId);

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
