using ZeroTrust.Backend.Models;

namespace ZeroTrust.Backend.Data;

/// <summary>
/// Data access service interface for MongoDB/DocumentDB abstraction
/// Supports MongoDB for local development, DocumentDB for AWS deployment
/// </summary>
public interface IDataService
{
    // User operations
    Task<User?> GetUserBySubAsync(string sub);
    Task<User?> GetUserByIdAsync(string userId);
    Task PutUserAsync(User user);
    Task UpdateUserAsync(User user);
    Task<List<User>> GetUsersByOrganizationAsync(string organizationId);

    // Role operations
    Task<Role?> GetRoleByIdAsync(string roleId);
    Task<Role?> GetRoleByNameAsync(string roleName);
    Task<List<Role>> GetAllRolesAsync();
    Task PutRoleAsync(Role role);

    // UserRole operations
    Task<List<Role>> GetUserRolesAsync(string userId);
    Task AssignRoleToUserAsync(string userId, string roleId, string? assignedBy = null);
    Task RemoveRoleFromUserAsync(string userId, string roleId);

    // Organization operations
    Task<Organization?> GetOrganizationByIdAsync(string organizationId);
    Task<Organization?> GetOrganizationByCognitoGroupAsync(string cognitoGroupName);
    Task<Organization?> GetOrganizationByShortNameAsync(string shortName);
    Task<List<Organization>> GetAllOrganizationsAsync();
    Task PutOrganizationAsync(Organization organization);
    Task UpdateOrganizationAsync(Organization organization);

    // UserAgreement operations
    Task<UserAgreement?> GetActiveAgreementAsync(string? organizationId = null);
    Task<UserAgreement?> GetAgreementAsync(string agreementId, int version);
    Task<List<UserAgreement>> GetAgreementVersionsAsync(string agreementId);
    Task PutAgreementAsync(UserAgreement agreement);

    // DataAccessRequest operations
    Task<DataAccessRequest?> GetRequestByIdAsync(string requestId);
    Task<DataAccessRequest?> GetRequestByRequestIdAsync(string requestId);
    Task<List<DataAccessRequest>> GetRequestsByRequesterAsync(string requesterId, int limit = 100);
    Task<List<DataAccessRequest>> GetPendingRequestsByOrgAsync(string dataOwnerOrg, int limit = 100);
    Task<bool> CreateDataAccessRequestAsync(DataAccessRequest request);
    Task PutRequestAsync(DataAccessRequest request);
    Task UpdateDataAccessRequestAsync(DataAccessRequest request);
    Task<List<DataAccessRequest>> GetRequestsByStatusAsync(RequestStatus status, int limit = 100);
    Task<List<DatasetCatalogItem>> GetPublishedDatasetCatalogItemsAsync(int limit = 200);
    Task<DatasetCatalogItem?> GetDatasetCatalogItemByDatasetIdAsync(string datasetId);
    Task PutDatasetCatalogItemAsync(DatasetCatalogItem item);

    // AuditEvent operations
    Task CreateAuditEventAsync(AuditEvent auditEvent);
    Task<List<AuditEvent>> GetAuditEventsAsync(int limit = 100);
    Task<List<AuditEvent>> GetAuditEventsByRequestAsync(string requestId, int limit = 100);

    // OTP operations
    Task CreateOtpRecordAsync(OtpRecord record);
    Task<OtpRecord?> GetLatestOtpRecordAsync(string requestId);
    Task UpdateOtpRecordAsync(OtpRecord record);
    Task<bool> TryMarkOtpUsedAsync(string otpRecordId, int expectedAttemptCount);
    Task<bool> TryIncrementOtpAttemptAsync(string otpRecordId, int expectedAttemptCount, DateTime? lockedAt);
    Task<int> CountOtpRecordsSinceAsync(string requestId, DateTime sinceUtc);
}
