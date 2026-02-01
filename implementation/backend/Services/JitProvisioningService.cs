using ZeroTrust.Backend.Data;
using ZeroTrust.Backend.Models;

namespace ZeroTrust.Backend.Services;

/// <summary>
/// Just-In-Time (JIT) user provisioning service
/// Automatically provisions users from Cognito JWT claims based on Cognito group membership
/// Maps cognitoGroupName (Cognito auto-generated group ID) to Organization
/// Assigns roles based on organizational setup
/// </summary>
public interface IJitProvisioningService
{
    /// <summary>
    /// Provision or update user based on JWT claims
    /// Returns user entity after creation/update or null if provisioning fails
    /// </summary>
    Task<User?> ProvisionUserAsync(Dictionary<string, object> jwtClaims);
}

public class JitProvisioningService : IJitProvisioningService
{
    private readonly IDataService _dataService;
    private readonly IJwtExtractionService _jwtExtractionService;
    private readonly ILogger<JitProvisioningService> _logger;

    // Expected JWT claim names from Cognito
    private const string SubClaimType = "sub";
    private const string EmailClaimType = "email";
    private const string NameClaimType = "name";
    private const string CognitoGroupsClaimType = "cognito:groups";

    public JitProvisioningService(
        IDataService dataService,
        IJwtExtractionService jwtExtractionService,
        ILogger<JitProvisioningService> logger)
    {
        _dataService = dataService;
        _jwtExtractionService = jwtExtractionService;
        _logger = logger;
    }

    public async Task<User?> ProvisionUserAsync(Dictionary<string, object> jwtClaims)
    {
        try
        {
            // Extract required claims
            var sub = _jwtExtractionService.GetClaimValue(jwtClaims, SubClaimType);
            var email = _jwtExtractionService.GetClaimValue(jwtClaims, EmailClaimType);
            var name = _jwtExtractionService.GetClaimValue(jwtClaims, NameClaimType);

            if (string.IsNullOrEmpty(sub))
            {
                _logger.LogWarning("JWT missing 'sub' claim");
                return null;
            }

            // Check if user already exists
            var existingUser = await _dataService.GetUserBySubAsync(sub);
            if (existingUser != null)
            {
                // Update last login
                existingUser.LastAccessAt = DateTime.UtcNow;
                await _dataService.UpdateUserAsync(existingUser);
                return existingUser;
            }

            // Extract Cognito groups (auto-generated group IDs like "ap-southeast-1_lq8PdxNkF_IDP-A")
            var cognitoGroups = ExtractCognitoGroups(jwtClaims);
            
            if (cognitoGroups.Count == 0)
            {
                _logger.LogWarning("User {sub} has no Cognito groups, cannot determine organization", sub);
                return null;
            }

            // Find organization matching the first Cognito group
            // In practice, a user should have exactly one group for their organization
            var organization = await _dataService.GetOrganizationByCognitoGroupAsync(cognitoGroups[0]);
            
            if (organization == null)
            {
                _logger.LogWarning(
                    "No organization found for Cognito group {cognitoGroup}",
                    cognitoGroups[0]
                );
                return null;
            }

            // Create new user
            var newUser = new User
            {
                Id = Guid.NewGuid().ToString(),
                Sub = sub,
                Email = email ?? "unknown@example.com",
                DisplayName = name ?? email ?? "Unknown User",
                OrganizationId = organization.Id,
                Status = UserStatus.Active,
                CreatedAt = DateTime.UtcNow,

                LastAccessAt = DateTime.UtcNow
            };

            await _dataService.PutUserAsync(newUser);
            
            _logger.LogInformation(
                "JIT provisioned user {userId} (sub={sub}) in organization {orgId}",
                newUser.Id,
                sub,
                organization.Id
            );

            // Assign default role (Requester) to all users
            // Organization-specific role assignments would happen through separate admin operations
            var requesterRole = await _dataService.GetRoleByNameAsync(RoleNames.Requester);
            if (requesterRole != null)
            {
                await _dataService.AssignRoleToUserAsync(newUser.Id, requesterRole.Id, "system");
                _logger.LogInformation("Assigned Requester role to new user {userId}", newUser.Id);
            }

            return newUser;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during JIT user provisioning");
            return null;
        }
    }

    /// <summary>
    /// Extract Cognito groups from JWT claims
    /// Cognito typically stores groups as a JSON array in the "cognito:groups" claim
    /// </summary>
    private List<string> ExtractCognitoGroups(Dictionary<string, object> jwtClaims)
    {
        var groups = new List<string>();

        if (jwtClaims.TryGetValue(CognitoGroupsClaimType, out var groupsValue))
        {
            // If it's already a JsonElement array, deserialize it
            if (groupsValue is System.Text.Json.JsonElement jsonElement)
            {
                if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var item in jsonElement.EnumerateArray())
                    {
                        var group = item.GetString();
                        if (!string.IsNullOrEmpty(group))
                        {
                            groups.Add(group);
                        }
                    }
                }
            }
            // If it's already a list, use it directly
            else if (groupsValue is List<string> groupsList)
            {
                groups.AddRange(groupsList);
            }
            // If it's a string array, convert it
            else if (groupsValue is string[] groupsArray)
            {
                groups.AddRange(groupsArray.Where(g => !string.IsNullOrEmpty(g)));
            }
        }

        return groups;
    }
}
