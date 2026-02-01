namespace ZeroTrust.Backend.Data;

using MongoDB.Driver;
using ZeroTrust.Backend.Models;


/// <summary>
/// MongoDB implementation of IDataService for local development and testing
/// In production, this would be replaced with DynamoDB implementation
/// </summary>
public class MongoDataService : IDataService
{
    private readonly IMongoDatabase _database;
    private readonly IMongoCollection<User> _usersCollection;
    private readonly IMongoCollection<Role> _rolesCollection;
    private readonly IMongoCollection<UserRole> _userRolesCollection;
    private readonly IMongoCollection<Organization> _organizationsCollection;
    private readonly IMongoCollection<UserAgreement> _agreementsCollection;
    private readonly IMongoCollection<DataAccessRequest> _requestsCollection;
    private readonly IMongoCollection<AuditEvent> _auditCollection;

    public MongoDataService(IMongoClient mongoClient, string databaseName = "zero_trust_db")
    {
        _database = mongoClient.GetDatabase(databaseName);
        
        _usersCollection = _database.GetCollection<User>("users");
        _rolesCollection = _database.GetCollection<Role>("roles");
        _userRolesCollection = _database.GetCollection<UserRole>("user_roles");
        _organizationsCollection = _database.GetCollection<Organization>("organizations");
        _agreementsCollection = _database.GetCollection<UserAgreement>("user_agreements");
        _requestsCollection = _database.GetCollection<DataAccessRequest>("requests");
        _auditCollection = _database.GetCollection<AuditEvent>("audit_events");
        
        InitializeIndexes();
    }

    private void InitializeIndexes()
    {
        // Users: Query by sub (Cognito subject)
        _usersCollection.Indexes.CreateOne(
            new CreateIndexModel<User>(
                Builders<User>.IndexKeys.Ascending(u => u.Sub),
                new CreateIndexOptions { Unique = true }
            )
        );
        
        // Users: Query by organization
        _usersCollection.Indexes.CreateOne(
            new CreateIndexModel<User>(
                Builders<User>.IndexKeys.Ascending(u => u.OrganizationId)
            )
        );

        // Organizations: Query by Cognito group name (critical for JIT provisioning)
        _organizationsCollection.Indexes.CreateOne(
            new CreateIndexModel<Organization>(
                Builders<Organization>.IndexKeys.Ascending(o => o.CognitoGroupName),
                new CreateIndexOptions { Unique = true }
            )
        );

        // UserRoles: Composite index for user+role queries
        _userRolesCollection.Indexes.CreateOne(
            new CreateIndexModel<UserRole>(
                Builders<UserRole>.IndexKeys.Ascending(ur => ur.UserId)
            )
        );

        // UserAgreements: Composite key index for version queries
        _agreementsCollection.Indexes.CreateOne(
            new CreateIndexModel<UserAgreement>(
                Builders<UserAgreement>.IndexKeys.Ascending(a => a.AgreementId)
                    .Ascending(a => a.Version),
                new CreateIndexOptions { Unique = true }
            )
        );

        // DataAccessRequests: Query by requester, status
        _requestsCollection.Indexes.CreateOne(
            new CreateIndexModel<DataAccessRequest>(
                Builders<DataAccessRequest>.IndexKeys.Ascending(r => r.RequesterId)
            )
        );
        
        _requestsCollection.Indexes.CreateOne(
            new CreateIndexModel<DataAccessRequest>(
                Builders<DataAccessRequest>.IndexKeys.Ascending(r => r.Status)
            )
        );

        // DataAccessRequests: Query by DataOwner org for pending approvals
        _requestsCollection.Indexes.CreateOne(
            new CreateIndexModel<DataAccessRequest>(
                Builders<DataAccessRequest>.IndexKeys.Ascending(r => r.DataOwnerOrg)
                    .Ascending(r => r.Status)
            )
        );
    }

    // ==================== User Operations ====================

    public async Task<User?> GetUserBySubAsync(string sub)
    {
        return await _usersCollection.Find(u => u.Sub == sub).FirstOrDefaultAsync();
    }

    public async Task<User?> GetUserByIdAsync(string userId)
    {
        return await _usersCollection.Find(u => u.Id == userId).FirstOrDefaultAsync();
    }

    public async Task PutUserAsync(User user)
    {
        await _usersCollection.InsertOneAsync(user);
    }

    public async Task UpdateUserAsync(User user)
    {
        await _usersCollection.ReplaceOneAsync(u => u.Id == user.Id, user);
    }

    public async Task<List<User>> GetUsersByOrganizationAsync(string organizationId)
    {
        return await _usersCollection
            .Find(u => u.OrganizationId == organizationId)
            .ToListAsync();
    }

    // ==================== Role Operations ====================

    public async Task<Role?> GetRoleByIdAsync(string roleId)
    {
        return await _rolesCollection.Find(r => r.Id == roleId).FirstOrDefaultAsync();
    }

    public async Task<Role?> GetRoleByNameAsync(string roleName)
    {
        return await _rolesCollection.Find(r => r.Name == roleName).FirstOrDefaultAsync();
    }

    public async Task<List<Role>> GetAllRolesAsync()
    {
        return await _rolesCollection.Find(_ => true).ToListAsync();
    }

    public async Task PutRoleAsync(Role role)
    {
        await _rolesCollection.InsertOneAsync(role);
    }

    // ==================== UserRole Operations ====================

    public async Task<List<Role>> GetUserRolesAsync(string userId)
    {
        var userRoles = await _userRolesCollection
            .Find(ur => ur.UserId == userId)
            .ToListAsync();

        var roles = new List<Role>();
        foreach (var userRole in userRoles)
        {
            var role = await GetRoleByIdAsync(userRole.RoleId);
            if (role != null)
            {
                roles.Add(role);
            }
        }

        return roles;
    }

    public async Task AssignRoleToUserAsync(string userId, string roleId, string? assignedBy = null)
    {
        var userRole = new UserRole
        {
            Id = $"{userId}#{roleId}",
            UserId = userId,
            RoleId = roleId,
            AssignedAt = DateTime.UtcNow,
            AssignedBy = assignedBy
        };

        await _userRolesCollection.InsertOneAsync(userRole);
    }

    public async Task RemoveRoleFromUserAsync(string userId, string roleId)
    {
        await _userRolesCollection.DeleteOneAsync(
            ur => ur.UserId == userId && ur.RoleId == roleId
        );
    }

    // ==================== Organization Operations ====================

    public async Task<Organization?> GetOrganizationByIdAsync(string organizationId)
    {
        return await _organizationsCollection.Find(o => o.Id == organizationId).FirstOrDefaultAsync();
    }

    public async Task<Organization?> GetOrganizationByCognitoGroupAsync(string cognitoGroupName)
    {
        return await _organizationsCollection
            .Find(o => o.CognitoGroupName == cognitoGroupName)
            .FirstOrDefaultAsync();
    }

    public async Task<List<Organization>> GetAllOrganizationsAsync()
    {
        return await _organizationsCollection.Find(_ => true).ToListAsync();
    }

    public async Task PutOrganizationAsync(Organization organization)
    {
        await _organizationsCollection.InsertOneAsync(organization);
    }

    public async Task UpdateOrganizationAsync(Organization organization)
    {
        await _organizationsCollection.ReplaceOneAsync(o => o.Id == organization.Id, organization);
    }

    // ==================== UserAgreement Operations ====================

    public async Task<UserAgreement?> GetActiveAgreementAsync(string? organizationId = null)
    {
        var filter = organizationId != null
            ? Builders<UserAgreement>.Filter.And(
                Builders<UserAgreement>.Filter.Eq(a => a.OrganizationId, organizationId),
                Builders<UserAgreement>.Filter.Eq(a => a.Status, AgreementStatus.Active)
              )
            : Builders<UserAgreement>.Filter.Eq(a => a.Status, AgreementStatus.Active);

        return await _agreementsCollection
            .Find(filter)
            .SortByDescending(a => a.Version)
            .FirstOrDefaultAsync();
    }

    public async Task<UserAgreement?> GetAgreementAsync(string agreementId, int version)
    {
        return await _agreementsCollection
            .Find(a => a.AgreementId == agreementId && a.Version == version)
            .FirstOrDefaultAsync();
    }

    public async Task<List<UserAgreement>> GetAgreementVersionsAsync(string agreementId)
    {
        return await _agreementsCollection
            .Find(a => a.AgreementId == agreementId)
            .SortByDescending(a => a.Version)
            .ToListAsync();
    }

    public async Task PutAgreementAsync(UserAgreement agreement)
    {
        await _agreementsCollection.InsertOneAsync(agreement);
    }

    // ==================== DataAccessRequest Operations ====================

    public async Task<DataAccessRequest?> GetRequestByIdAsync(string requestId)
    {
        return await _requestsCollection.Find(r => r.RequestId == requestId).FirstOrDefaultAsync();
    }

    public async Task<List<DataAccessRequest>> GetRequestsByRequesterAsync(string requesterId, int limit = 100)
    {
        return await _requestsCollection
            .Find(r => r.RequesterId == requesterId)
            .SortByDescending(r => r.CreatedAt)
            .Limit(limit)
            .ToListAsync();
    }

    public async Task<List<DataAccessRequest>> GetPendingRequestsByOrgAsync(string dataOwnerOrg, int limit = 100)
    {
        return await _requestsCollection
            .Find(r => r.DataOwnerOrg == dataOwnerOrg && 
                      (r.Status == RequestStatus.PendingAdminReview || 
                       r.Status == RequestStatus.PendingOwnerApproval))
            .SortByDescending(r => r.CreatedAt)
            .Limit(limit)
            .ToListAsync();
    }

    public async Task PutRequestAsync(DataAccessRequest request)
    {
        await _requestsCollection.InsertOneAsync(request);
    }

    public async Task UpdateRequestAsync(DataAccessRequest request)
    {
        await _requestsCollection.ReplaceOneAsync(r => r.RequestId == request.RequestId, request);
    }

    public async Task<List<DataAccessRequest>> GetRequestsByStatusAsync(RequestStatus status, int limit = 100)
    {
        return await _requestsCollection
            .Find(r => r.Status == status)
            .SortByDescending(r => r.CreatedAt)
            .Limit(limit)
            .ToListAsync();
    }

    public async Task<bool> CreateDataAccessRequestAsync(DataAccessRequest request)
    {
        try
        {
            await _requestsCollection.InsertOneAsync(request);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task UpdateDataAccessRequestAsync(DataAccessRequest request)
    {
        await _requestsCollection.ReplaceOneAsync(r => r.Id == request.Id, request);
    }
}
