using ZeroTrust.Backend.Models;

namespace ZeroTrust.Backend.Services;

public static class CognitoGroupMapper
{
    public static Organization? FindMatchingOrganization(
        IEnumerable<Organization> organizations,
        IEnumerable<string> cognitoGroups)
    {
        var allOrganizations = organizations.ToList();
        var normalizedGroups = cognitoGroups
            .Select(NormalizeGroupValue)
            .Where(g => !string.IsNullOrWhiteSpace(g))
            .ToList();

        if (normalizedGroups.Count == 0)
        {
            return null;
        }

        // Priority 1: exact Cognito group match from configured seed values.
        foreach (var group in normalizedGroups)
        {
            var exactGroup = allOrganizations.FirstOrDefault(o =>
                string.Equals(o.CognitoGroupName, group, StringComparison.OrdinalIgnoreCase));
            if (exactGroup != null)
            {
                return exactGroup;
            }
        }

        // Priority 2: direct IdP name match from incoming values.
        foreach (var group in normalizedGroups)
        {
            var exactIdp = allOrganizations.FirstOrDefault(o =>
                string.Equals(o.CognitoIdpName, group, StringComparison.OrdinalIgnoreCase));
            if (exactIdp != null)
            {
                return exactIdp;
            }
        }

        // Priority 3: derive IdP name from Cognito auto-generated group suffixes.
        foreach (var group in normalizedGroups)
        {
            var idpName = ExtractIdpName(group);
            if (string.IsNullOrWhiteSpace(idpName))
            {
                continue;
            }

            var byIdpName = allOrganizations.FirstOrDefault(o =>
                string.Equals(o.CognitoIdpName, idpName, StringComparison.OrdinalIgnoreCase));
            if (byIdpName != null)
            {
                return byIdpName;
            }

            var byGroupSuffix = allOrganizations.FirstOrDefault(o =>
                o.CognitoGroupName.EndsWith($"_{idpName}", StringComparison.OrdinalIgnoreCase));
            if (byGroupSuffix != null)
            {
                return byGroupSuffix;
            }
        }

        return null;
    }

    public static Organization? FindMatchingOrganization(
        IEnumerable<Organization> organizations,
        string cognitoGroup)
    {
        return FindMatchingOrganization(organizations, new[] { cognitoGroup });
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
