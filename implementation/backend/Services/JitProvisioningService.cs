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
    private const string IdentitiesClaimType = "identities";

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
            var (providerName, providerUserId) = ExtractFederatedIdentity(jwtClaims);
            var sub = _jwtExtractionService.GetClaimValue(jwtClaims, SubClaimType);
            if (string.IsNullOrWhiteSpace(sub) &&
                !string.IsNullOrWhiteSpace(providerName) &&
                !string.IsNullOrWhiteSpace(providerUserId))
            {
                sub = $"{providerName}_{providerUserId}";
                _logger.LogInformation(
                    "Using identities fallback for missing sub claim: provider={providerName}, userId={providerUserId}",
                    providerName,
                    providerUserId);
            }

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
            var primaryGroup = cognitoGroups.FirstOrDefault();
            Organization? organization = null;

            if (string.IsNullOrWhiteSpace(primaryGroup) && string.IsNullOrWhiteSpace(providerName))
            {
                _logger.LogWarning(
                    "User {sub} has no Cognito groups and no identities provider, cannot determine organization",
                    sub);
                return null;
            }

            var organizations = await _dataService.GetAllOrganizationsAsync();
            if (!string.IsNullOrWhiteSpace(primaryGroup))
            {
                organization = CognitoGroupMapper.FindMatchingOrganization(organizations, primaryGroup);
            }

            if (organization == null && !string.IsNullOrWhiteSpace(providerName))
            {
                organization = CognitoGroupMapper.FindMatchingOrganization(organizations, providerName);
                if (organization != null)
                {
                    _logger.LogInformation(
                        "Resolved organization from identities provider fallback: provider={providerName}, orgId={orgId}",
                        providerName,
                        organization.Id);
                }
            }

            if (organization == null)
            {
                _logger.LogWarning(
                    "No organization found for Cognito group {cognitoGroup} (provider fallback: {providerName})",
                    primaryGroup ?? "null",
                    providerName ?? "null"
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
                OrganizationId = organization?.Id,
                Status = UserStatus.Active,
                CreatedAt = DateTime.UtcNow,

                LastAccessAt = DateTime.UtcNow
            };

            await _dataService.PutUserAsync(newUser);
            
            _logger.LogInformation(
                "JIT provisioned user {userId} (sub={sub}) in organization {orgId}",
                newUser.Id,
                sub,
                organization?.Id ?? "UNKNOWN"
            );

            var requesterRole = await _dataService.GetRoleByNameAsync(RoleNames.Requester);
            if (requesterRole != null)
            {
                await _dataService.AssignRoleToUserAsync(newUser.Id, requesterRole.Id, "system");
                _logger.LogInformation("Assigned Requester role to new user {userId}", newUser.Id);
            }

            // In this prototype, Org A acts as dataset custodian/data owner.
            if (string.Equals(organization?.ShortName, "ORG-A", StringComparison.OrdinalIgnoreCase))
            {
                var dataOwnerRole = await _dataService.GetRoleByNameAsync(RoleNames.DataOwner);
                if (dataOwnerRole != null)
                {
                    await _dataService.AssignRoleToUserAsync(newUser.Id, dataOwnerRole.Id, "system");
                    _logger.LogInformation("Assigned DataOwner role to Org A user {userId}", newUser.Id);
                }
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
                else if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    var groupString = jsonElement.GetString();
                    AddGroupsFromString(groups, groupString);
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
            // If it's a single string, treat as single group or JSON array string
            else if (groupsValue is string groupString)
            {
                AddGroupsFromString(groups, groupString);
            }
        }

        return groups;
    }

    private static void AddGroupsFromString(List<string> groups, string? groupString)
    {
        if (string.IsNullOrWhiteSpace(groupString))
        {
            return;
        }

        // Handle JSON array string like "[\"group1\",\"group2\"]"
        if (groupString.TrimStart().StartsWith("["))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(groupString);
                if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var item in doc.RootElement.EnumerateArray())
                    {
                        var group = item.GetString();
                        if (!string.IsNullOrEmpty(group))
                        {
                            groups.Add(group);
                        }
                    }

                    return;
                }
            }
            catch
            {
                // Ignore parsing errors and fall through to treat as single group string
            }
        }

        groups.Add(groupString);
    }

    private static (string? ProviderName, string? ProviderUserId) ExtractFederatedIdentity(
        Dictionary<string, object> jwtClaims)
    {
        if (!jwtClaims.TryGetValue(IdentitiesClaimType, out var identitiesValue) || identitiesValue == null)
        {
            return (null, null);
        }

        if (identitiesValue is string identitiesString)
        {
            return TryParseFederatedIdentityFromJson(identitiesString);
        }

        if (identitiesValue is List<string> identitiesList)
        {
            foreach (var item in identitiesList)
            {
                var parsed = TryParseFederatedIdentityFromJson(item);
                if (!string.IsNullOrWhiteSpace(parsed.ProviderName) ||
                    !string.IsNullOrWhiteSpace(parsed.ProviderUserId))
                {
                    return parsed;
                }
            }
        }

        if (identitiesValue is System.Text.Json.JsonElement element)
        {
            return TryParseFederatedIdentityFromElement(element);
        }

        return (null, null);
    }

    private static (string? ProviderName, string? ProviderUserId) TryParseFederatedIdentityFromJson(
        string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return (null, null);
        }

        if (!(json.TrimStart().StartsWith("[") || json.TrimStart().StartsWith("{")))
        {
            return (null, null);
        }

        using var doc = System.Text.Json.JsonDocument.Parse(json);
        return TryParseFederatedIdentityFromElement(doc.RootElement);
    }

    private static (string? ProviderName, string? ProviderUserId) TryParseFederatedIdentityFromElement(
        System.Text.Json.JsonElement element)
    {
        if (element.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            var first = element.EnumerateArray().FirstOrDefault();
            if (first.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                return (
                    first.TryGetProperty("providerName", out var providerName) ? providerName.GetString() : null,
                    first.TryGetProperty("userId", out var userId) ? userId.GetString() : null
                );
            }

            return (null, null);
        }

        if (element.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            return (
                element.TryGetProperty("providerName", out var providerName) ? providerName.GetString() : null,
                element.TryGetProperty("userId", out var userId) ? userId.GetString() : null
            );
        }

        return (null, null);
    }
}
