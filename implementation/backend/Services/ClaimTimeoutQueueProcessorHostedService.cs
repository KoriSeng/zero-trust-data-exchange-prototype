using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.DependencyInjection;
using ZeroTrust.Backend.Data;
using ZeroTrust.Backend.Models;

namespace ZeroTrust.Backend.Services;

public sealed class ClaimTimeoutQueueProcessorHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAmazonSQS _sqsClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ClaimTimeoutQueueProcessorHostedService> _logger;

    public ClaimTimeoutQueueProcessorHostedService(
        IServiceScopeFactory scopeFactory,
        IAmazonSQS sqsClient,
        IConfiguration configuration,
        ILogger<ClaimTimeoutQueueProcessorHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _sqsClient = sqsClient;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var claimTimeoutQueueUrl = _configuration["AWS:StepFunctions:ClaimTimeoutQueueUrl"];
        if (string.IsNullOrWhiteSpace(claimTimeoutQueueUrl))
        {
            _logger.LogInformation("Claim-timeout queue URL is not configured; timeout queue processor is idle.");
            return;
        }

        _logger.LogInformation("Claim-timeout queue processor started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var response = await _sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
                {
                    QueueUrl = claimTimeoutQueueUrl,
                    MaxNumberOfMessages = 5,
                    WaitTimeSeconds = 5
                }, stoppingToken);

                if (response.Messages is null || response.Messages.Count == 0)
                {
                    continue;
                }

                foreach (var message in response.Messages)
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
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Claim-timeout queue processor iteration failed");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
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

        if (request.Status is RequestStatus.Completed or RequestStatus.Denied or RequestStatus.Expired)
        {
            _logger.LogInformation("Skipping claim-timeout for closed request {requestId} with status {status}", request.RequestId, request.Status);
            return true;
        }

        if (request.Status != RequestStatus.Redeemed)
        {
            _logger.LogInformation("Skipping claim-timeout for non-redeemed request {requestId} with status {status}", request.RequestId, request.Status);
            return true;
        }

        request.Status = RequestStatus.Expired;
        request.UpdatedAt = DateTime.UtcNow;
        await dataService.UpdateDataAccessRequestAsync(request);

        await dataService.CreateAuditEventAsync(new AuditEvent
        {
            EventType = "CLAIM_WINDOW_EXPIRED",
            RequestId = request.RequestId,
            DatasetId = request.DatasetId,
            Description = $"Claim window expired after redemption for request {request.RequestId}",
            Result = AuditEventResult.Success
        });

        _logger.LogInformation("Claim window expired for request {requestId}", request.RequestId);
        _ = cancellationToken;
        return true;
    }
}
