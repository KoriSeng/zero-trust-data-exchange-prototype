using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.DependencyInjection;
using ZeroTrust.Backend.Data;
using ZeroTrust.Backend.Models;

namespace ZeroTrust.Backend.Services;

public sealed class WorkflowQueueProcessorHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAmazonSQS _sqsClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WorkflowQueueProcessorHostedService> _logger;

    public WorkflowQueueProcessorHostedService(
        IServiceScopeFactory scopeFactory,
        IAmazonSQS sqsClient,
        IConfiguration configuration,
        ILogger<WorkflowQueueProcessorHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _sqsClient = sqsClient;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var otpDispatchQueueUrl = _configuration["AWS:StepFunctions:OtpDispatchQueueUrl"];
        var claimTimeoutQueueUrl = _configuration["AWS:StepFunctions:ClaimTimeoutQueueUrl"];
        if (string.IsNullOrWhiteSpace(otpDispatchQueueUrl) && string.IsNullOrWhiteSpace(claimTimeoutQueueUrl))
        {
            _logger.LogInformation("Workflow queue URLs are not configured; queue processor is idle.");
            return;
        }

        _logger.LogInformation("Workflow queue processor started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(otpDispatchQueueUrl))
                {
                    var response = await _sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
                    {
                        QueueUrl = otpDispatchQueueUrl,
                        MaxNumberOfMessages = 5,
                        WaitTimeSeconds = 5
                    }, stoppingToken);

                    foreach (var message in response.Messages)
                    {
                        var processed = await ProcessOtpDispatchMessageAsync(message, stoppingToken);
                        if (!processed)
                        {
                            continue;
                        }

                        await _sqsClient.DeleteMessageAsync(new DeleteMessageRequest
                        {
                            QueueUrl = otpDispatchQueueUrl,
                            ReceiptHandle = message.ReceiptHandle
                        }, stoppingToken);
                    }
                }

                if (!string.IsNullOrWhiteSpace(claimTimeoutQueueUrl))
                {
                    var timeoutResponse = await _sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
                    {
                        QueueUrl = claimTimeoutQueueUrl,
                        MaxNumberOfMessages = 5,
                        WaitTimeSeconds = 5
                    }, stoppingToken);

                    foreach (var message in timeoutResponse.Messages)
                    {
                        var processed = await ProcessClaimTimeoutMessageAsync(message, stoppingToken);
                        if (!processed)
                        {
                            continue;
                        }

                        await _sqsClient.DeleteMessageAsync(new DeleteMessageRequest
                        {
                            QueueUrl = claimTimeoutQueueUrl,
                            ReceiptHandle = message.ReceiptHandle
                        }, stoppingToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Workflow queue processor iteration failed");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }

    private async Task<bool> ProcessOtpDispatchMessageAsync(Message message, CancellationToken cancellationToken)
    {
        string? requestId;
        try
        {
            using var body = System.Text.Json.JsonDocument.Parse(message.Body);
            if (!body.RootElement.TryGetProperty("requestId", out var requestIdElement))
            {
                _logger.LogWarning("OTP dispatch message missing requestId");
                return true;
            }

            requestId = requestIdElement.GetString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse OTP dispatch message body");
            return true;
        }

        if (string.IsNullOrWhiteSpace(requestId))
        {
            _logger.LogWarning("OTP dispatch message had empty requestId");
            return true;
        }

        using var scope = _scopeFactory.CreateScope();
        var dataService = scope.ServiceProvider.GetRequiredService<IDataService>();
        var otpService = scope.ServiceProvider.GetRequiredService<IOtpService>();

        var request = await dataService.GetRequestByRequestIdAsync(requestId);
        if (request == null)
        {
            _logger.LogWarning("OTP dispatch request not found: {requestId}", requestId);
            return true;
        }

        if (request.Status is RequestStatus.Denied or RequestStatus.Redeemed or RequestStatus.Completed)
        {
            _logger.LogInformation("Skipping OTP dispatch for closed request {requestId} with status {status}", request.RequestId, request.Status);
            return true;
        }

        var now = DateTime.UtcNow;
        var otpSendCooldown = TimeSpan.FromSeconds(30);
        var otpSendWindow = TimeSpan.FromMinutes(10);
        const int otpSendMaxPerWindow = 5;

        var latestOtpRecord = await dataService.GetLatestOtpRecordAsync(request.RequestId);
        if (latestOtpRecord is not null && (now - latestOtpRecord.CreatedAt) < otpSendCooldown)
        {
            _logger.LogInformation("OTP dispatch cooldown active for {requestId}", request.RequestId);
            return true;
        }

        var sendsInWindow = await dataService.CountOtpRecordsSinceAsync(request.RequestId, now - otpSendWindow);
        if (sendsInWindow >= otpSendMaxPerWindow)
        {
            _logger.LogWarning("OTP dispatch throttled for {requestId} due to send window limit", request.RequestId);
            return true;
        }

        var ownerOrg = await dataService.GetOrganizationByIdAsync(request.DataOwnerOrg);
        if (string.IsNullOrWhiteSpace(ownerOrg?.ContactEmail))
        {
            _logger.LogWarning("Owner org contact email missing for {requestId}", request.RequestId);
            return true;
        }

        var preview = await otpService.GenerateAndSendOtpAsync(request.RequestId, ownerOrg.ContactEmail);

        request.Status = RequestStatus.ClaimPending;
        request.OtpGeneratedAt = DateTime.UtcNow;
        request.OtpExpiresAt = preview.ExpiresAt;
        request.OtpEmailTo = preview.RecipientEmail;
        request.OtpEmailSubject = preview.Subject;
        request.OtpEmailTextBody = preview.TextBody;
        request.OtpCodePreview = preview.OtpCode;
        request.UpdatedAt = DateTime.UtcNow;
        await dataService.UpdateDataAccessRequestAsync(request);

        await dataService.CreateAuditEventAsync(new AuditEvent
        {
            EventType = "OTP_DISPATCHED",
            RequestId = request.RequestId,
            DatasetId = request.DatasetId,
            Description = $"OTP debug email generated for request {request.RequestId}",
            Result = AuditEventResult.Success
        });

        _logger.LogInformation("OTP dispatch completed for request {requestId}", request.RequestId);
        _ = cancellationToken;
        return true;
    }

    private async Task<bool> ProcessClaimTimeoutMessageAsync(Message message, CancellationToken cancellationToken)
    {
        string? requestId;
        try
        {
            using var body = System.Text.Json.JsonDocument.Parse(message.Body);
            if (!body.RootElement.TryGetProperty("requestId", out var requestIdElement))
            {
                _logger.LogWarning("Claim-timeout message missing requestId");
                return true;
            }

            requestId = requestIdElement.GetString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse claim-timeout message body");
            return true;
        }

        if (string.IsNullOrWhiteSpace(requestId))
        {
            _logger.LogWarning("Claim-timeout message had empty requestId");
            return true;
        }

        using var scope = _scopeFactory.CreateScope();
        var dataService = scope.ServiceProvider.GetRequiredService<IDataService>();
        var request = await dataService.GetRequestByRequestIdAsync(requestId);
        if (request == null)
        {
            _logger.LogWarning("Claim-timeout request not found: {requestId}", requestId);
            return true;
        }

        if (request.Status == RequestStatus.Redeemed || request.Status == RequestStatus.Completed || request.Status == RequestStatus.Denied)
        {
            _logger.LogInformation("Skipping claim-timeout for closed request {requestId} with status {status}", request.RequestId, request.Status);
            return true;
        }

        if (request.Status == RequestStatus.Redeemed)
        {
            request.Status = RequestStatus.Expired;
            request.UpdatedAt = DateTime.UtcNow;
            await dataService.UpdateDataAccessRequestAsync(request);

            await dataService.CreateAuditEventAsync(new AuditEvent
            {
                EventType = "CLAIM_WINDOW_EXPIRED",
                RequestId = request.RequestId,
                DatasetId = request.DatasetId,
                Description = $"Claim window expired before redemption for request {request.RequestId}",
                Result = AuditEventResult.Success
            });

            _logger.LogInformation("Claim window expired for request {requestId}", request.RequestId);
        }

        _ = cancellationToken;
        return true;
    }
}
