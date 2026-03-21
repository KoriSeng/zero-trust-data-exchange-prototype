namespace ZeroTrust.Backend.Services;

/// <summary>
/// AWS S3 service for managing data access and storage
/// </summary>
public interface IS3Service
{
    /// <summary>
    /// Check if requested objects exist in S3
    /// </summary>
    Task<bool> ObjectsExistAsync(string bucket, List<string> objectKeys);

    /// <summary>
    /// Generate pre-signed URL for object access
    /// </summary>
    Task<string> GeneratePresignedUrlAsync(string bucket, string objectKey, TimeSpan expiration);

    /// <summary>
    /// Generate pre-signed download URL with Content-Disposition header to force download
    /// </summary>
    Task<string> GeneratePresignedDownloadUrlAsync(string bucket, string objectKey, TimeSpan expiration);

    /// <summary>
    /// List objects in bucket with prefix
    /// </summary>
    Task<List<string>> ListObjectsAsync(string bucket, string? prefix = null);

    /// <summary>
    /// Upload request metadata to S3 (for audit trail)
    /// </summary>
    Task<bool> UploadRequestMetadataAsync(string bucket, string requestId, string metadata);
}

/// <summary>
/// AWS Step Functions service for request approval workflow
/// </summary>
public interface IStepFunctionsService
{
    /// <summary>
    /// Start approval workflow execution for a request
    /// </summary>
    Task<string> StartApprovalWorkflowAsync(string requestId);

    /// <summary>
    /// Get workflow execution status
    /// </summary>
    Task<WorkflowExecutionStatus> GetExecutionStatusAsync(string executionArn);

    /// <summary>
    /// Get execution history
    /// </summary>
    Task<List<ExecutionEvent>> GetExecutionHistoryAsync(string executionArn);

    /// <summary>
    /// Resolve a wait-for-task-token step by sending success callback
    /// </summary>
    Task SendTaskSuccessAsync(string taskToken, Dictionary<string, object> output);

    /// <summary>
    /// Terminate a wait-for-task-token step by sending failure callback
    /// </summary>
    Task SendTaskFailureAsync(string taskToken, string error, string cause);

    /// <summary>
    /// Stop a workflow execution when callback token is unavailable
    /// </summary>
    Task StopExecutionAsync(string executionArn, string error, string cause);
}

/// <summary>
/// Workflow execution status
/// </summary>
public class WorkflowExecutionStatus
{
    public string ExecutionArn { get; set; } = null!;
    public string Status { get; set; } = null!; // RUNNING, SUCCEEDED, FAILED, TIMED_OUT, ABORTED
    public DateTime StartDate { get; set; }
    public DateTime? StopDate { get; set; }
    public string? Output { get; set; }
    public string? Error { get; set; }
    public string? Cause { get; set; }
}

/// <summary>
/// Workflow execution event
/// </summary>
public class ExecutionEvent
{
    public long Timestamp { get; set; }
    public string Type { get; set; } = null!;
    public string? Details { get; set; }
}
