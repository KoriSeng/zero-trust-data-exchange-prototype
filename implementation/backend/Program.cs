using MongoDB.Driver;
using ZeroTrust.Backend.Data;
using ZeroTrust.Backend.Services;
using ZeroTrust.Backend.Models;
using Amazon.S3;
using Amazon.SQS;
using Amazon.SQS.Model;
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
var configuredServiceUrl = builder.Configuration["AWS:ServiceUrl"];
var serviceUrl = builder.Environment.IsDevelopment() ? configuredServiceUrl : null;
if (!builder.Environment.IsDevelopment() && !string.IsNullOrWhiteSpace(configuredServiceUrl))
{
    Console.WriteLine("Ignoring AWS:ServiceUrl because environment is not Development.");
}

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
builder.Services.AddSingleton<IInputSanitizer, InputSanitizer>();
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
    var sqsConfig = new Amazon.SQS.AmazonSQSConfig { ServiceURL = serviceUrl };
    
    builder.Services.AddSingleton<IAmazonS3>(sp => 
        new AmazonS3Client(new Amazon.Runtime.BasicAWSCredentials("test", "test"), s3Config)
    );
    builder.Services.AddSingleton<IAmazonStepFunctions>(sp => 
        new AmazonStepFunctionsClient(new Amazon.Runtime.BasicAWSCredentials("test", "test"), sfnConfig)
    );
    builder.Services.AddSingleton<IAmazonSQS>(sp =>
        new AmazonSQSClient(new Amazon.Runtime.BasicAWSCredentials("test", "test"), sqsConfig)
    );
}
else
{
    // AWS production configuration
    builder.Services.AddSingleton<IAmazonS3>(sp => new AmazonS3Client());
    builder.Services.AddSingleton<IAmazonStepFunctions>(sp => new AmazonStepFunctionsClient());
    builder.Services.AddSingleton<IAmazonSQS>(sp => new AmazonSQSClient());
}

builder.Services.AddScoped<IS3Service, S3Service>();
builder.Services.AddScoped<IStepFunctionsService, StepFunctionsService>();
builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddHostedService<WorkflowQueueProcessorHostedService>();
builder.Services.AddHostedService<ClaimTimeoutQueueProcessorHostedService>();

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
    var userEmail = context.User.FindFirst("email")?.Value;
    var userDisplayName = context.User.FindFirst("name")?.Value;
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
app.MapPost("/requests", async (HttpContext context, CreateDataAccessRequestDto request, IDataService dataService, IS3Service s3Service, IStepFunctionsService stepFunctionsService, IInputSanitizer inputSanitizer, ILogger<Program> logger) =>
{
    // User is already authenticated and provisioned
    var requesterId = context.User.FindFirst("user_id")?.Value;
    var requesterEmail = context.User.FindFirst("email")?.Value;
    var requesterOrg = context.User.FindFirst("user_organization")?.Value;

    if (string.IsNullOrEmpty(requesterId) || string.IsNullOrEmpty(requesterOrg))
    {
        return Results.BadRequest(new { error = "User not properly provisioned" });
    }

    // Validate request
    if (string.IsNullOrWhiteSpace(request.DatasetId))
    {
        return Results.BadRequest(new { error = "DatasetId is required" });
    }

    if (string.IsNullOrWhiteSpace(request.Purpose))
    {
        return Results.BadRequest(new { error = "Purpose is required" });
    }

    var sanitizedPurpose = inputSanitizer.SanitizeText(request.Purpose);
    if (string.IsNullOrWhiteSpace(sanitizedPurpose))
    {
        return Results.BadRequest(new { error = "Purpose must contain readable text" });
    }

    var userAgentHeader = context.Request.Headers["User-Agent"].FirstOrDefault();
    var sanitizedUserAgent = inputSanitizer.SanitizeText(userAgentHeader);

    if (string.IsNullOrWhiteSpace(requesterEmail) || requesterEmail.Equals("unknown@example.com", StringComparison.OrdinalIgnoreCase))
    {
        var requesterUser = await dataService.GetUserBySubAsync(requesterId);
        requesterEmail = requesterUser?.Email;
    }

    // Fetch dataset metadata from catalog (single source of truth)
    var dataset = await dataService.GetDatasetCatalogItemByDatasetIdAsync(request.DatasetId.Trim());
    if (dataset == null)
    {
        return Results.NotFound(new { error = $"Dataset '{request.DatasetId}' not found in catalog" });
    }

    if (!dataset.IsPublished)
    {
        return Results.BadRequest(new { error = $"Dataset '{request.DatasetId}' is not published" });
    }

    if (dataset.ObjectKeys == null || dataset.ObjectKeys.Count == 0)
    {
        return Results.BadRequest(new { error = $"Dataset '{request.DatasetId}' has no object keys configured" });
    }

    // Resolve DataOwnerOrg short name to internal org ID
    var dataOwnerOrg = await dataService.GetOrganizationByShortNameAsync(dataset.DataOwnerOrgShortName);
    if (dataOwnerOrg == null)
    {
        return Results.Problem(
            title: "Configuration error",
            detail: $"Organization '{dataset.DataOwnerOrgShortName}' for dataset '{request.DatasetId}' not found in database",
            statusCode: 500
        );
    }

    // Check if objects exist in S3 — warn only; the hard check occurs at redemption
    var dataBucket = context.RequestServices.GetRequiredService<IConfiguration>()["AWS:S3:DataBucket"] ?? "zero-trust-data";
    var objectsExist = await s3Service.ObjectsExistAsync(dataBucket, dataset.ObjectKeys);
    if (!objectsExist)
    {
        logger.LogWarning(
            "Dataset {datasetId} references objects not found in S3 bucket {bucket}: {keys}",
            request.DatasetId,
            dataBucket,
            string.Join(", ", dataset.ObjectKeys)
        );
    }

    var activeAgreement = await dataService.GetActiveAgreementAsync(dataOwnerOrg.Id)
        ?? await dataService.GetActiveAgreementAsync();

    // Create request in database using catalog data
    var dataAccessRequest = new DataAccessRequest
    {
        Id = Guid.NewGuid().ToString(),
        RequestId = $"REQ-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}",
        RequesterId = requesterId,
        RequesterEmail = requesterEmail ?? "unknown@example.com",
        RequesterOrg = requesterOrg ?? "UNKNOWN",
        DatasetId = dataset.DatasetId,
        DatasetName = dataset.Name,
        ObjectKeys = dataset.ObjectKeys,
        Purpose = sanitizedPurpose,
        DataOwnerOrg = dataOwnerOrg.Id,
        UserAgreementId = activeAgreement?.AgreementId,
        UserAgreementVersion = activeAgreement?.Version,
        AgreementContent = activeAgreement?.Content,
        Status = RequestStatus.Submitted,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        ExpiresAt = DateTime.UtcNow.AddDays(30)
    };

    var stepFunctionsEnabled = context.RequestServices
        .GetRequiredService<IConfiguration>()
        .GetValue<bool>("AWS:StepFunctions:Enabled", true);

    // Start approval workflow first when enabled; request creation is workflow-gated.
    string? workflowExecutionArn = null;
    if (stepFunctionsEnabled)
    {
        try
        {
            workflowExecutionArn = await stepFunctionsService.StartApprovalWorkflowAsync(
                dataAccessRequest.RequestId
            );

            logger.LogInformation("Started approval workflow for request {requestId}", dataAccessRequest.RequestId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error starting approval workflow for request {requestId}", dataAccessRequest.RequestId);
            return Results.Problem(
                title: "Workflow unavailable",
                detail: "Could not start approval workflow. Request was not created.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    dataAccessRequest.WorkflowExecutionArn = workflowExecutionArn;
    dataAccessRequest.Status = RequestStatus.PendingOwnerApproval;

    // Save to database after workflow start succeeds (or is disabled)
    var saved = await dataService.CreateDataAccessRequestAsync(dataAccessRequest);
    if (!saved)
    {
        logger.LogError("Failed to save data access request {requestId}", dataAccessRequest.RequestId);
        return Results.StatusCode(StatusCodes.Status500InternalServerError);
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
            purpose = sanitizedPurpose,
            user_agent = sanitizedUserAgent,
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
        Result = AuditEventResult.Success,
        Metadata = new Dictionary<string, object>
        {
            ["purpose"] = sanitizedPurpose,
            ["user_agent"] = sanitizedUserAgent
        }
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

// Approve a pending request (data owner only). OTP is generated by workflow after approval.
app.MapPost("/requests/{id}/approve", async (string id, ApproveRequestDto dto, HttpContext context, IDataService dataService, IStepFunctionsService stepFunctionsService, IInputSanitizer inputSanitizer, IAmazonSQS sqsClient, ILogger<Program> logger) =>
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

    if (request.Status != RequestStatus.PendingOwnerApproval)
    {
        return Results.BadRequest(new { error = $"Request is not in an approvable state (current status: {request.Status})" });
    }
    if (string.IsNullOrWhiteSpace(request.WorkflowExecutionArn))
    {
        return Results.Problem(
            title: "Workflow unavailable",
            detail: "Request has no workflow execution ARN and cannot be approved.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    var sanitizedComments = inputSanitizer.SanitizeText(dto.Comments);

    var callbackQueueUrl = context.RequestServices.GetRequiredService<IConfiguration>()["AWS:StepFunctions:ApprovalDecisionQueueUrl"];
    if (string.IsNullOrWhiteSpace(callbackQueueUrl))
    {
        return Results.Problem(
            title: "Workflow callback unavailable",
            detail: "Approval callback queue is not configured.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    var receive = await sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
    {
        QueueUrl = callbackQueueUrl,
        MaxNumberOfMessages = 10,
        WaitTimeSeconds = 1
    });

    Message? callbackMessage = null;
    string? callbackToken = null;
    foreach (var message in receive.Messages)
    {
        try
        {
            using var body = System.Text.Json.JsonDocument.Parse(message.Body);
            if (!body.RootElement.TryGetProperty("requestId", out var requestIdElement))
            {
                continue;
            }

            var callbackRequestId = requestIdElement.GetString();
            if (!string.Equals(callbackRequestId, request.RequestId, StringComparison.Ordinal))
            {
                continue;
            }

            if (!body.RootElement.TryGetProperty("taskToken", out var tokenElement))
            {
                continue;
            }

            callbackToken = tokenElement.GetString();
            if (string.IsNullOrWhiteSpace(callbackToken))
            {
                continue;
            }

            callbackMessage = message;
            break;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Invalid approval callback message in queue");
        }
    }

    if (string.IsNullOrWhiteSpace(callbackToken) || callbackMessage is null)
    {
        return Results.Problem(
            title: "Approval gate not ready",
            detail: "No workflow approval gate token was available for this request yet. Please retry shortly.",
            statusCode: StatusCodes.Status409Conflict);
    }

    var requestedSeconds = dto.AccessDurationSeconds
        ?? (dto.AccessDurationHours.HasValue ? dto.AccessDurationHours.Value * 3600 : 3600);
    var claimWindowSeconds = Math.Max(1, requestedSeconds);
    await stepFunctionsService.SendTaskSuccessAsync(callbackToken, new Dictionary<string, object>
    {
        { "request_id", request.RequestId },
        { "decision", "approved" },
        { "approvedBy", approverId! },
        { "approvedAt", DateTime.UtcNow.ToString("o") },
        { "comments", sanitizedComments },
        { "claim_window_seconds", claimWindowSeconds }
    });

    await sqsClient.DeleteMessageAsync(new DeleteMessageRequest
    {
        QueueUrl = callbackQueueUrl,
        ReceiptHandle = callbackMessage.ReceiptHandle
    });

    request.Status = RequestStatus.Approved;
    request.ApprovedAt = DateTime.UtcNow;
    request.ApprovedBy = approverId;
    request.ApprovalComments = sanitizedComments;
    request.ClaimWindowSeconds = claimWindowSeconds;
    request.UpdatedAt = DateTime.UtcNow;
    await dataService.UpdateDataAccessRequestAsync(request);

    await dataService.CreateAuditEventAsync(new AuditEvent
    {
        EventType = "REQUEST_APPROVED",
        ActorId = approverId,
        OrganizationId = approverOrg,
        RequestId = request.RequestId,
        DatasetId = request.DatasetId,
        Description = $"Request {request.RequestId} approved by {approverId}; workflow advanced to OTP stage",
        Result = AuditEventResult.Success
    });

    logger.LogInformation("Request {requestId} approved by {approverId}; workflow callback sent", request.RequestId, approverId);

    return Results.Ok(new
    {
        requestId = request.RequestId,
        status = request.Status.ToString(),
        message = "Request approved. OTP email preview will be generated for requester shortly."
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

    if (request.Status != RequestStatus.PendingOwnerApproval &&
        request.Status != RequestStatus.OtpSent &&
        request.Status != RequestStatus.ClaimPending)
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

// View generated debug OTP email for requester (prototype only)
app.MapGet("/requests/{id}/otp/email", async (string id, HttpContext context, IDataService dataService) =>
{
    var requesterId = context.User.FindFirst("user_id")?.Value;
    if (string.IsNullOrWhiteSpace(requesterId))
    {
        return Results.BadRequest(new { error = "User not properly provisioned" });
    }

    var request = await dataService.GetRequestByIdAsync(id);
    if (request == null)
    {
        return Results.NotFound(new { error = "Request not found" });
    }

    if (request.RequesterId != requesterId)
    {
        return Results.Forbid();
    }

    if (string.IsNullOrWhiteSpace(request.OtpEmailTextBody) || string.IsNullOrWhiteSpace(request.OtpCodePreview))
    {
        return Results.BadRequest(new { error = "OTP email is not available yet. Wait for approval and OTP generation." });
    }

    return Results.Ok(new
    {
        requestId = request.RequestId,
        to = request.OtpEmailTo,
        subject = request.OtpEmailSubject,
        textBody = request.OtpEmailTextBody,
        otpCode = request.OtpCodePreview,
        generatedAt = request.OtpGeneratedAt,
        expiresAt = request.OtpExpiresAt
    });
})
.WithName("GetRequestOtpEmail")
.RequireAuthorization("Authenticated");

// Generate a short-lived download URL during an active redeemed access window.
app.MapGet("/requests/{id}/download-url", async (string id, string key, HttpContext context, IDataService dataService, IS3Service s3Service, ILogger<Program> logger) =>
{
    var requesterId = context.User.FindFirst("user_id")?.Value;
    if (string.IsNullOrWhiteSpace(requesterId))
    {
        return Results.BadRequest(new { error = "User not properly provisioned" });
    }

    if (string.IsNullOrWhiteSpace(key))
    {
        return Results.BadRequest(new { error = "key is required" });
    }

    var request = await dataService.GetRequestByIdAsync(id);
    if (request == null)
    {
        return Results.NotFound(new { error = "Request not found" });
    }

    if (request.RequesterId != requesterId)
    {
        return Results.Forbid();
    }

    if (request.Status != RequestStatus.Redeemed)
    {
        return Results.BadRequest(new { error = $"Request is not in an active download state (current status: {request.Status})" });
    }

    if (!request.PresignedUrlExpiresAt.HasValue || request.PresignedUrlExpiresAt.Value <= DateTime.UtcNow)
    {
        return Results.Problem(
            title: "Access window expired",
            detail: "This request access window has expired.",
            statusCode: StatusCodes.Status409Conflict);
    }

    if (!request.ObjectKeys.Contains(key))
    {
        return Results.BadRequest(new { error = "Requested object key is not part of this request." });
    }

    var dataBucket = context.RequestServices.GetRequiredService<IConfiguration>()["AWS:S3:DataBucket"] ?? "zero-trust-data";
    var linkExpiry = TimeSpan.FromMinutes(1);
    var expiresAt = DateTime.UtcNow.Add(linkExpiry);
    string url;
    try
    {
        url = await s3Service.GeneratePresignedUrlAsync(dataBucket, key, linkExpiry);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to generate short-lived download URL for request {requestId} key {key}", request.RequestId, key);
        return Results.StatusCode(StatusCodes.Status500InternalServerError);
    }

    await dataService.CreateAuditEventAsync(new AuditEvent
    {
        EventType = "DOWNLOAD_URL_ISSUED",
        ActorId = requesterId,
        RequestId = request.RequestId,
        DatasetId = request.DatasetId,
        Description = $"Short-lived download URL issued for key {key}",
        Result = AuditEventResult.Success,
        Metadata = new Dictionary<string, object>
        {
            { "object_key", key },
            { "url_expires_at", expiresAt.ToString("o") }
        }
    });

    return Results.Ok(new
    {
        requestId = request.RequestId,
        objectKey = key,
        url,
        expiresAt
    });
})
.WithName("GetRequestDownloadUrl")
.RequireAuthorization("Authenticated");

// Redeem approved request and return pre-signed S3 URLs for the requester.
app.MapPost("/requests/{id}/redeem", async (string id, RedeemRequestDto dto, HttpContext context, IDataService dataService, IS3Service s3Service, IStepFunctionsService stepFunctionsService, IAmazonSQS sqsClient, IOtpService otpService, ILogger<Program> logger) =>
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

    if (request.Status != RequestStatus.Approved &&
        request.Status != RequestStatus.OtpSent &&
        request.Status != RequestStatus.ClaimPending)
    {
        return Results.BadRequest(new { error = $"Request is not in a redeemable state (current status: {request.Status})" });
    }

    if (string.IsNullOrWhiteSpace(dto.Otp))
    {
        return Results.BadRequest(new { error = "otp is required" });
    }

    Message? claimCallbackMessage = null;
    string? claimCallbackToken = null;
    if (!string.IsNullOrWhiteSpace(request.WorkflowExecutionArn))
    {
        var claimQueueUrl = context.RequestServices.GetRequiredService<IConfiguration>()["AWS:StepFunctions:ClaimCallbackQueueUrl"];
        if (string.IsNullOrWhiteSpace(claimQueueUrl))
        {
            return Results.Problem(
                title: "Workflow callback unavailable",
                detail: "Claim callback queue is not configured.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        // Poll with retries and immediately re-queue unmatched messages so we don't starve other requests.
        for (var attempt = 0; attempt < 10 && string.IsNullOrWhiteSpace(claimCallbackToken); attempt++)
        {
            var receive = await sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
            {
                QueueUrl = claimQueueUrl,
                MaxNumberOfMessages = 10,
                WaitTimeSeconds = 2,
                VisibilityTimeout = 10
            });

            foreach (var message in receive.Messages)
            {
                try
                {
                    using var body = System.Text.Json.JsonDocument.Parse(message.Body);
                    if (!body.RootElement.TryGetProperty("requestId", out var requestIdElement))
                    {
                        await sqsClient.ChangeMessageVisibilityAsync(new ChangeMessageVisibilityRequest
                        {
                            QueueUrl = claimQueueUrl,
                            ReceiptHandle = message.ReceiptHandle,
                            VisibilityTimeout = 0
                        });
                        continue;
                    }

                    var callbackRequestId = requestIdElement.GetString();
                    if (!string.Equals(callbackRequestId, request.RequestId, StringComparison.Ordinal))
                    {
                        await sqsClient.ChangeMessageVisibilityAsync(new ChangeMessageVisibilityRequest
                        {
                            QueueUrl = claimQueueUrl,
                            ReceiptHandle = message.ReceiptHandle,
                            VisibilityTimeout = 0
                        });
                        continue;
                    }

                    if (!body.RootElement.TryGetProperty("taskToken", out var tokenElement))
                    {
                        await sqsClient.ChangeMessageVisibilityAsync(new ChangeMessageVisibilityRequest
                        {
                            QueueUrl = claimQueueUrl,
                            ReceiptHandle = message.ReceiptHandle,
                            VisibilityTimeout = 0
                        });
                        continue;
                    }

                    var token = tokenElement.GetString();
                    if (string.IsNullOrWhiteSpace(token))
                    {
                        await sqsClient.ChangeMessageVisibilityAsync(new ChangeMessageVisibilityRequest
                        {
                            QueueUrl = claimQueueUrl,
                            ReceiptHandle = message.ReceiptHandle,
                            VisibilityTimeout = 0
                        });
                        continue;
                    }

                    claimCallbackToken = token;
                    claimCallbackMessage = message;
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Invalid claim callback message in queue");
                    await sqsClient.ChangeMessageVisibilityAsync(new ChangeMessageVisibilityRequest
                    {
                        QueueUrl = claimQueueUrl,
                        ReceiptHandle = message.ReceiptHandle,
                        VisibilityTimeout = 0
                    });
                }
            }
        }

        if (string.IsNullOrWhiteSpace(claimCallbackToken) || claimCallbackMessage is null)
        {
            return Results.Problem(
                title: "Claim gate not ready",
                detail: "No workflow claim gate token was available for this request yet. Please retry shortly.",
                statusCode: StatusCodes.Status409Conflict);
        }
    }

    var otpResult = await otpService.ValidateOtpAsync(request.RequestId, dto.Otp);
    if (otpResult != OtpValidationResult.Success)
    {
        return otpResult switch
        {
            OtpValidationResult.Expired => Results.BadRequest(new { error = "OTP has expired. Request a new OTP email by re-approval." }),
            OtpValidationResult.MaxAttemptsExceeded => Results.Json(new { error = "Too many failed attempts. Ask data owner to re-approve and re-issue OTP." }, statusCode: StatusCodes.Status429TooManyRequests),
            OtpValidationResult.AlreadyUsed => Results.BadRequest(new { error = "OTP has already been used." }),
            OtpValidationResult.NotFound => Results.BadRequest(new { error = "No OTP found for this request yet." }),
            _ => Results.BadRequest(new { error = "Invalid OTP code." })
        };
    }

    var dataBucket = context.RequestServices.GetRequiredService<IConfiguration>()["AWS:S3:DataBucket"] ?? "zero-trust-data";
    var windowSeconds = request.ClaimWindowSeconds.HasValue
        ? Math.Max(1, request.ClaimWindowSeconds.Value)
        : 3600;
    var urlExpiry = TimeSpan.FromSeconds(windowSeconds);
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

    if (!string.IsNullOrWhiteSpace(claimCallbackToken) && claimCallbackMessage is not null)
    {
        await stepFunctionsService.SendTaskSuccessAsync(claimCallbackToken, new Dictionary<string, object>
        {
            { "request_id", request.RequestId },
            { "claimedBy", redeemerId! },
            { "claimedAt", DateTime.UtcNow.ToString("o") },
            { "requestId", request.RequestId }
        });

        var claimQueueUrl = context.RequestServices.GetRequiredService<IConfiguration>()["AWS:StepFunctions:ClaimCallbackQueueUrl"]!;
        await sqsClient.DeleteMessageAsync(new DeleteMessageRequest
        {
            QueueUrl = claimQueueUrl,
            ReceiptHandle = claimCallbackMessage.ReceiptHandle
        });
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
        message = $"Access granted. URLs expire in {Math.Max(1, (int)Math.Round(urlExpiry.TotalMinutes))} minutes."
    });
})
.WithName("RedeemRequest")
.RequireAuthorization("Authenticated");

app.Run();
