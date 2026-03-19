using Amazon.S3;
using Amazon.S3.Model;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;
using System.Text.Json;

namespace ZeroTrust.Backend.Services;

/// <summary>
/// AWS S3 service implementation for LocalStack and AWS
/// </summary>
public class S3Service : IS3Service
{
    private readonly IAmazonS3 _s3Client;
    private readonly ILogger<S3Service> _logger;

    public S3Service(IAmazonS3 s3Client, ILogger<S3Service> logger)
    {
        _s3Client = s3Client;
        _logger = logger;
    }

    public async Task<bool> ObjectsExistAsync(string bucket, List<string> objectKeys)
    {
        try
        {
            foreach (var key in objectKeys)
            {
                try
                {
                    await _s3Client.GetObjectMetadataAsync(bucket, key);
                }
                catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("Object {key} not found in bucket {bucket}", key, bucket);
                    return false;
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking object existence in bucket {bucket}", bucket);
            return false;
        }
    }

    public async Task<string> GeneratePresignedUrlAsync(string bucket, string objectKey, TimeSpan expiration)
    {
        try
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = bucket,
                Key = objectKey,
                Expires = DateTime.UtcNow.Add(expiration),
                Verb = HttpVerb.GET
            };

            var url = _s3Client.GetPreSignedURL(request);
            _logger.LogInformation("Generated pre-signed URL for {bucket}/{key}", bucket, objectKey);
            return url;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating pre-signed URL for {bucket}/{key}", bucket, objectKey);
            throw;
        }
    }

    public async Task<List<string>> ListObjectsAsync(string bucket, string? prefix = null)
    {
        try
        {
            var request = new ListObjectsV2Request
            {
                BucketName = bucket,
                Prefix = prefix
            };

            var response = await _s3Client.ListObjectsV2Async(request);
            return response.S3Objects?.Select(o => o.Key).ToList() ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing objects in bucket {bucket}", bucket);
            throw;
        }
    }

    public async Task<bool> UploadRequestMetadataAsync(string bucket, string requestId, string metadata)
    {
        try
        {
            var key = $"audit/{requestId}/metadata.json";
            var request = new PutObjectRequest
            {
                BucketName = bucket,
                Key = key,
                ContentBody = metadata,
                ContentType = "application/json"
            };

            await _s3Client.PutObjectAsync(request);
            _logger.LogInformation("Uploaded request metadata for {requestId} to {bucket}/{key}", requestId, bucket, key);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading request metadata for {requestId}", requestId);
            return false;
        }
    }
}

/// <summary>
/// AWS Step Functions service implementation for LocalStack and AWS
/// </summary>
public class StepFunctionsService : IStepFunctionsService
{
    private readonly IAmazonStepFunctions _stepFunctionsClient;
    private readonly ILogger<StepFunctionsService> _logger;
    private readonly string _workflowArn;

    public StepFunctionsService(
        IAmazonStepFunctions stepFunctionsClient,
        ILogger<StepFunctionsService> logger,
        IConfiguration configuration)
    {
        _stepFunctionsClient = stepFunctionsClient;
        _logger = logger;
        _workflowArn = configuration["AWS:StepFunctions:ApprovalWorkflowArn"] 
            ?? "arn:aws:states:us-east-1:000000000000:stateMachine:ZeroTrustApprovalWorkflow";
    }

    public async Task<string> StartApprovalWorkflowAsync(string requestId)
    {
        try
        {
            var input = new
            {
                request_id = requestId,
                timestamp = DateTime.UtcNow
            };

            var request = new StartExecutionRequest
            {
                StateMachineArn = _workflowArn,
                Name = $"{requestId}-execution-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                Input = JsonSerializer.Serialize(input)
            };

            var response = await _stepFunctionsClient.StartExecutionAsync(request);
            _logger.LogInformation("Started approval workflow for request {requestId}, executionArn: {executionArn}", 
                requestId, response.ExecutionArn);
            
            return response.ExecutionArn;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting approval workflow for request {requestId}", requestId);
            throw;
        }
    }

    public async Task<WorkflowExecutionStatus> GetExecutionStatusAsync(string executionArn)
    {
        try
        {
            var request = new DescribeExecutionRequest { ExecutionArn = executionArn };
            var response = await _stepFunctionsClient.DescribeExecutionAsync(request);

            return new WorkflowExecutionStatus
            {
                ExecutionArn = response.ExecutionArn,
                Status = response.Status.Value,
                StartDate = response.StartDate ?? DateTime.UtcNow,
                StopDate = response.StopDate,
                Output = response.Output,
                Error = response.Error,
                Cause = response.Cause
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting execution status for {executionArn}", executionArn);
            throw;
        }
    }

    public async Task<List<ExecutionEvent>> GetExecutionHistoryAsync(string executionArn)
    {
        try
        {
            var request = new GetExecutionHistoryRequest { ExecutionArn = executionArn };
            var response = await _stepFunctionsClient.GetExecutionHistoryAsync(request);

            return response.Events.Select(e => new ExecutionEvent
            {
                Timestamp = new DateTimeOffset(e.Timestamp ?? DateTime.UtcNow).ToUnixTimeMilliseconds(),
                Type = e.Type.Value,
                Details = e.ToString()
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting execution history for {executionArn}", executionArn);
            throw;
        }
    }

    public async Task SendTaskSuccessAsync(string taskToken, Dictionary<string, object> output)
    {
        try
        {
            var request = new SendTaskSuccessRequest
            {
                TaskToken = taskToken,
                Output = JsonSerializer.Serialize(output)
            };

            await _stepFunctionsClient.SendTaskSuccessAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending task success callback to Step Functions");
            throw;
        }
    }

    public async Task SendTaskFailureAsync(string taskToken, string error, string cause)
    {
        try
        {
            var request = new SendTaskFailureRequest
            {
                TaskToken = taskToken,
                Error = error,
                Cause = cause
            };

            await _stepFunctionsClient.SendTaskFailureAsync(request);
            _logger.LogInformation("Sent task failure to Step Functions: {error}", error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending task failure callback to Step Functions");
            throw;
        }
    }
}
