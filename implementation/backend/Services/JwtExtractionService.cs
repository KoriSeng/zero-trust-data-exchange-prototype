using System.Security.Claims;
using System.Text.Json;

namespace ZeroTrust.Backend.Services;

/// <summary>
/// JWT extraction service - parses JWT claims WITHOUT signature validation
/// Signature validation happens at API Gateway for production
/// This enables easy local testing with self-signed or test tokens
/// </summary>
public interface IJwtExtractionService
{
    /// <summary>
    /// Extract JWT claims from Authorization header (Bearer token)
    /// </summary>
    (bool Success, Dictionary<string, object>? Claims, string? ErrorMessage) ExtractClaims(HttpContext context);
    
    /// <summary>
    /// Get specific claim value from JWT
    /// </summary>
    string? GetClaimValue(Dictionary<string, object> claims, string claimType);
}

public class JwtExtractionService : IJwtExtractionService
{
    private const string AuthorizationHeader = "Authorization";
    private const string BearerScheme = "Bearer";

    public (bool Success, Dictionary<string, object>? Claims, string? ErrorMessage) ExtractClaims(HttpContext context)
    {
        var authHeader = context.Request.Headers[AuthorizationHeader].FirstOrDefault();

        if (string.IsNullOrEmpty(authHeader))
        {
            return (false, null, "Authorization header missing");
        }

        if (!authHeader.StartsWith(BearerScheme, StringComparison.OrdinalIgnoreCase))
        {
            return (false, null, "Invalid Authorization header format");
        }

        var token = authHeader[BearerScheme.Length..].Trim();

        try
        {
            // Split JWT into parts: header.payload.signature
            var parts = token.Split('.');
            if (parts.Length != 3)
            {
                return (false, null, "Invalid JWT format");
            }

            // Decode payload (second part)
            var payload = parts[1];
            
            // Add padding if needed for base64 decoding
            var paddedPayload = payload.PadRight(
                payload.Length + (4 - payload.Length % 4) % 4, '='
            );

            var decodedBytes = Convert.FromBase64String(paddedPayload);
            var json = System.Text.Encoding.UTF8.GetString(decodedBytes);

            // Parse JSON claims
            var claims = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
            
            if (claims == null)
            {
                return (false, null, "Failed to parse JWT claims");
            }

            return (true, claims, null);
        }
        catch (Exception ex)
        {
            return (false, null, $"JWT parsing error: {ex.Message}");
        }
    }

    public string? GetClaimValue(Dictionary<string, object> claims, string claimType)
    {
        if (claims.TryGetValue(claimType, out var value))
        {
            return value?.ToString();
        }

        return null;
    }
}
