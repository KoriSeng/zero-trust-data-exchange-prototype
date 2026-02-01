using ZeroTrust.Backend.Models;

namespace ZeroTrust.Backend.Data;

/// <summary>
/// Data access service interface for MongoDB/DynamoDB abstraction
/// Supports MongoDB for local development, DynamoDB for production
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
    Task<List<DataAccessRequest>> GetRequestsByRequesterAsync(string requesterId, int limit = 100);
    Task<List<DataAccessRequest>> GetPendingRequestsByOrgAsync(string dataOwnerOrg, int limit = 100);
    Task PutRequestAsync(DataAccessRequest request);
    Task UpdateRequestAsync(DataAccessRequest request);
    Task<List<DataAccessRequest>> GetRequestsByStatusAsync(RequestStatus status, int limit = 100);
}
