using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.DependencyInjection;
using ZeroTrust.Backend.Data;
using ZeroTrust.Backend.Models;

namespace ZeroTrust.Backend.Services;

public sealed class ApprovalDecisionQueueProcessorHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAmazonSQS _sqsClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ApprovalDecisionQueueProcessorHostedService> _logger;

    public ApprovalDecisionQueueProcessorHostedService(
        IServiceScopeFactory scopeFactory,
        IAmazonSQS sqsClient,
        IConfiguration configuration,
        ILogger<ApprovalDecisionQueueProcessorHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _sqsClient = sqsClient;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var approvalDecisionQueueUrl = _configuration["AWS:StepFunctions:ApprovalDecisionQueueUrl"];
        if (string.IsNullOrWhiteSpace(approvalDecisionQueueUrl))
        {
            _logger.LogInformation("Approval-decision queue URL is not configured; approval queue processor is idle.");
            return;
        }

        _logger.LogInformation("Approval-decision queue processor started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var response = await _sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
                {
                    QueueUrl = approvalDecisionQueueUrl,
                    MaxNumberOfMessages = 5,
                    WaitTimeSeconds = 5
                }, stoppingToken);

                if (response.Messages is null || response.Messages.Count == 0)
                {
                    continue;
                }

                foreach (var message in response.Messages)
                {
                    var processed = await ProcessApprovalDecisionMessageAsync(message, stoppingToken);
                    if (!processed)
                    {
                        continue;
                    }

                    await _sqsClient.DeleteMessageAsync(new DeleteMessageRequest
                    {
                        QueueUrl = approvalDecisionQueueUrl,
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
                _logger.LogError(ex, "Approval-decision queue processor iteration failed");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }

    private async Task<bool> ProcessApprovalDecisionMessageAsync(Message message, CancellationToken cancellationToken)
    {
        string? requestId;
        string? taskToken;
        try
        {
            using var body = System.Text.Json.JsonDocument.Parse(message.Body);
            if (!body.RootElement.TryGetProperty("requestId", out var requestIdElement))
            {
                _logger.LogWarning("Approval-decision message missing requestId");
                return true;
            }

            if (!body.RootElement.TryGetProperty("taskToken", out var tokenElement))
            {
                _logger.LogWarning("Approval-decision message missing taskToken");
                return true;
            }

            requestId = requestIdElement.GetString();
            taskToken = tokenElement.GetString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse approval-decision message body");
            return true;
        }

        if (string.IsNullOrWhiteSpace(requestId) || string.IsNullOrWhiteSpace(taskToken))
        {
            _logger.LogWarning("Approval-decision message had empty requestId or taskToken");
            return true;
        }

        using var scope = _scopeFactory.CreateScope();
        var dataService = scope.ServiceProvider.GetRequiredService<IDataService>();
        var request = await dataService.GetRequestByRequestIdAsync(requestId);
        if (request == null)
        {
            _logger.LogWarning("Approval-decision request not found: {requestId}", requestId);
            return true;
        }

        request.ApprovalTaskToken = taskToken;
        request.ApprovalTaskTokenReceivedAt = DateTime.UtcNow;
        request.UpdatedAt = DateTime.UtcNow;
        await dataService.UpdateDataAccessRequestAsync(request);

        await dataService.CreateAuditEventAsync(new AuditEvent
        {
            EventType = "APPROVAL_TOKEN_RECEIVED",
            RequestId = request.RequestId,
            DatasetId = request.DatasetId,
            Description = $"Approval task token received for request {request.RequestId}",
            Result = AuditEventResult.Success
        });

        _logger.LogInformation("Approval token stored for request {requestId}", request.RequestId);
        _ = cancellationToken;
        return true;
    }
}
