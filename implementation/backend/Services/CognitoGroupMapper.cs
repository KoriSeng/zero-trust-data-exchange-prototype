using ZeroTrust.Backend.Models;

namespace ZeroTrust.Backend.Services;

public static class CognitoGroupMapper
{
    public static Organization? FindMatchingOrganization(
        IEnumerable<Organization> organizations,
        string cognitoGroup)
    {
        var normalizedGroup = NormalizeGroupValue(cognitoGroup);
        if (string.IsNullOrWhiteSpace(normalizedGroup))
        {
            return null;
        }

        var allOrganizations = organizations.ToList();

        var exact = allOrganizations.FirstOrDefault(o =>
            string.Equals(o.CognitoGroupName, normalizedGroup, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(o.CognitoIdpName, normalizedGroup, StringComparison.OrdinalIgnoreCase));
        if (exact != null)
        {
            return exact;
        }

        var idpName = ExtractIdpName(normalizedGroup);
        if (string.IsNullOrEmpty(idpName))
        {
            return null;
        }

        return allOrganizations.FirstOrDefault(o =>
            string.Equals(o.CognitoIdpName, idpName, StringComparison.OrdinalIgnoreCase) ||
            o.CognitoGroupName.EndsWith($"_{idpName}", StringComparison.OrdinalIgnoreCase));
    }

    private static string? ExtractIdpName(string cognitoGroup)
    {
        var normalizedGroup = NormalizeGroupValue(cognitoGroup);
        if (string.IsNullOrWhiteSpace(normalizedGroup))
        {
            return null;
        }

        if (normalizedGroup.StartsWith("IDP-", StringComparison.OrdinalIgnoreCase))
        {
            return normalizedGroup;
        }

        if (normalizedGroup.Contains("_IDP-A", StringComparison.OrdinalIgnoreCase))
        {
            return "IDP-A";
        }
        if (normalizedGroup.Contains("_IDP-B", StringComparison.OrdinalIgnoreCase))
        {
            return "IDP-B";
        }

        var idx = normalizedGroup.LastIndexOf('_');
        if (idx < 0 || idx == normalizedGroup.Length - 1)
        {
            return null;
        }

        var suffix = normalizedGroup[(idx + 1)..];
        return suffix.StartsWith("IDP-", StringComparison.OrdinalIgnoreCase) ? suffix : null;
    }

    private static string NormalizeGroupValue(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var value = raw.Trim().Trim('"');
        if (value.StartsWith("[") && value.EndsWith("]"))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(value);
                if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array &&
                    doc.RootElement.GetArrayLength() > 0 &&
                    doc.RootElement[0].ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    return doc.RootElement[0].GetString()?.Trim() ?? string.Empty;
                }
            }
            catch
            {
                // fall through to raw value
            }
        }

        return value;
    }
}
