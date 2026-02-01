namespace ZeroTrust.Backend.Services;

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using ZeroTrust.Backend.Data;
using ZeroTrust.Backend.Models;

/// <summary>
/// JIT (Just-In-Time) User Provisioning Claims Transformer
/// Automatically provisions users from JWT claims during authentication
/// Runs for every authenticated request, ensuring user is always available in HttpContext.User
/// </summary>
public class JitProvisioningClaimsTransformation : IClaimsTransformation
{
    private readonly IJitProvisioningService _jitService;
    private readonly IDataService _dataService;
    private readonly ILogger<JitProvisioningClaimsTransformation> _logger;

    public JitProvisioningClaimsTransformation(
        IJitProvisioningService jitService,
        IDataService dataService,
        ILogger<JitProvisioningClaimsTransformation> logger)
    {
        _jitService = jitService;
        _dataService = dataService;
        _logger = logger;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        // Only process authenticated principals
        if (!principal.Identity?.IsAuthenticated ?? false)
        {
            return principal;
        }

        try
        {
            // Extract claims from the principal (preserve multi-valued claims)
            // Group claims by type to detect multi-valued claims like identities, cognito:groups
            var claimsByType = principal.Claims.GroupBy(c => c.Type).ToDictionary(g => g.Key, g => g.ToList());
            var claims = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    
            
            foreach (var kvp in claimsByType)
            {
                var claimType = kvp.Key;
                var claimValues = kvp.Value;
                
                if (claimValues.Count == 1)
                {
                    // Single-valued claim
                    var value = claimValues[0].Value;
                    
                    // Try to parse as JSON if it looks like JSON
                    if (value?.TrimStart().StartsWith("[") == true || value?.TrimStart().StartsWith("{") == true)
                    {
                        try
                        {
                            using var parsed = System.Text.Json.JsonDocument.Parse(value);
                            if (parsed.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                            {
                                var list = new List<string>();
                                foreach (var item in parsed.RootElement.EnumerateArray())
                                {
                                    if (item.ValueKind == System.Text.Json.JsonValueKind.String)
                                    {
                                        var itemValue = item.GetString();
                                        if (!string.IsNullOrEmpty(itemValue))
                                        {
                                            list.Add(itemValue);
                                        }
                                    }
                                }

                                claims[claimType] = list;
                            }
                            else
                            {
                                claims[claimType] = value; // Keep original string for object/other types
                            }
                        }
                        catch
                        {
                            claims[claimType] = value;
                        }
                    }
                    else
                    {
                        claims[claimType] = value;
                    }
                }
                else
                {
                    // Multi-valued claim - store as list
                    claims[claimType] = claimValues.Select(c => c.Value).ToList<string>();
                }
            }

    
            // Provision user via JIT service
            var user = await _jitService.ProvisionUserAsync(claims);

            if (user == null)
            {
                _logger.LogWarning("JIT provisioning failed for principal {name}. Claims: {@claims}", 
                    principal.Identity?.Name, 
                    claims.Keys);
                return principal;
            }

            // Add custom claims to represent the provisioned user
            var identity = principal.Identity as ClaimsIdentity;
            if (identity != null)
            {
                // Add user metadata claims
                AddClaimIfMissing(identity, "user_id", user.Id);
                AddClaimIfMissing(identity, "user_sub", user.Sub);
                AddClaimIfMissing(identity, "user_email", user.Email ?? "unknown@example.com");
                AddClaimIfMissing(identity, "user_display_name", user.DisplayName ?? "Unknown User");
                AddClaimIfMissing(identity, "user_organization", user.OrganizationId ?? "UNKNOWN");
                AddClaimIfMissing(identity, "user_organization_name", await ResolveOrganizationNameAsync(user.OrganizationId));
                AddClaimIfMissing(identity, "user_status", user.Status.ToString());
                AddClaimIfMissing(identity, "user_created_at", user.CreatedAt.ToString("O"));
                AddClaimIfMissing(identity, "user_last_access_at", user.LastAccessAt.ToString("O"));

                // Add identity provider claim (used by /me and smoke tests)
                var identityProvider = ResolveIdentityProvider(claims, principal);
                if (!string.IsNullOrEmpty(identityProvider))
                {
                    AddClaimIfMissing(identity, "identity_provider", identityProvider);
                }
                
                // Add roles if assigned
                var roles = await GetUserRolesAsync(user.Id);
                foreach (var role in roles)
                {
                    AddRoleIfMissing(identity, role);
                }

                _logger.LogInformation("JIT provisioned user {userId} ({email}) with organization {org}", 
                    user.Id, user.Email, user.OrganizationId);
            }

            return principal;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during JIT provisioning claims transformation");
            return principal;
        }
    }

    private static void AddClaimIfMissing(ClaimsIdentity identity, string type, string value)
    {
        if (!identity.HasClaim(c => c.Type == type))
        {
            identity.AddClaim(new Claim(type, value));
        }
    }

    private static void AddRoleIfMissing(ClaimsIdentity identity, string role)
    {
        if (!identity.HasClaim(c => c.Type == ClaimTypes.Role && c.Value == role))
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }
    }

    private async Task<string> ResolveOrganizationNameAsync(string? organizationId)
    {
        if (string.IsNullOrWhiteSpace(organizationId))
        {
            return "UNKNOWN";
        }

        var organization = await _dataService.GetOrganizationByIdAsync(organizationId);
        return organization?.Name ?? "UNKNOWN";
    }

    private static string? ResolveIdentityProvider(
        Dictionary<string, object> claims,
        ClaimsPrincipal principal)
    {
        var existing = principal.FindFirst("identity_provider")?.Value;
        if (!string.IsNullOrEmpty(existing))
        {
            return existing;
        }

        if (claims.TryGetValue("identities", out var identitiesValue))
        {
            var provider = TryParseProviderFromIdentities(identitiesValue);
            if (!string.IsNullOrEmpty(provider))
            {
                return provider;
            }
        }

        var sub = principal.FindFirst("sub")?.Value;
        if (!string.IsNullOrEmpty(sub))
        {
            var prefix = sub.Split('_', 2, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();
            if (!string.IsNullOrEmpty(prefix))
            {
                return prefix;
            }
        }

        var cognitoUsername = principal.FindFirst("cognito:username")?.Value;
        if (!string.IsNullOrEmpty(cognitoUsername))
        {
            var prefix = cognitoUsername.Split('_', 2, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();
            if (!string.IsNullOrEmpty(prefix))
            {
                return prefix;
            }
        }

        return null;
    }

    private static string? TryParseProviderFromIdentities(object identitiesValue)
    {
        try
        {
            if (identitiesValue is List<string> identitiesList)
            {
                foreach (var item in identitiesList)
                {
                    var provider = TryParseProviderFromIdentitiesString(item);
                    if (!string.IsNullOrEmpty(provider))
                    {
                        return provider;
                    }
                }
            }
            else if (identitiesValue is string identitiesString)
            {
                return TryParseProviderFromIdentitiesString(identitiesString);
            }
        }
        catch
        {
            // Ignore parsing errors and fall back to other methods
        }

        return null;
    }

    private static string? TryParseProviderFromIdentitiesString(string identitiesString)
    {
        if (string.IsNullOrWhiteSpace(identitiesString))
        {
            return null;
        }

        if (!(identitiesString.TrimStart().StartsWith("[") || identitiesString.TrimStart().StartsWith("{")))
        {
            return null;
        }

        using var doc = System.Text.Json.JsonDocument.Parse(identitiesString);
        if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            var first = doc.RootElement.EnumerateArray().FirstOrDefault();
            if (first.ValueKind == System.Text.Json.JsonValueKind.Object &&
                first.TryGetProperty("providerName", out var providerName))
            {
                return providerName.GetString();
            }
        }
        else if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object &&
                 doc.RootElement.TryGetProperty("providerName", out var providerName))
        {
            return providerName.GetString();
        }

        return null;
    }

    /// <summary>
    /// Helper method to get user roles (requires access to data service)
    /// Note: This is injected as a dependency in the handler if needed
    /// </summary>
    private async Task<List<string>> GetUserRolesAsync(string userId)
    {
        // This would need to be injected or retrieved from context
        // For now, return empty list - roles can be added after user provisioning
        return await Task.FromResult(new List<string>());
    }
}
