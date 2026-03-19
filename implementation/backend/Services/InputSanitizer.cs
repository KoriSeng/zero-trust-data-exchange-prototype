using Ganss.Xss;

namespace ZeroTrust.Backend.Services;

public interface IInputSanitizer
{
    string SanitizeText(string? value);
}

public class InputSanitizer : IInputSanitizer
{
    private const int MaxLength = 1024;
    private static readonly HtmlSanitizer Sanitizer = CreateSanitizer();

    private static HtmlSanitizer CreateSanitizer()
    {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Clear();
        sanitizer.AllowedAttributes.Clear();
        return sanitizer;
    }

    public string SanitizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > MaxLength)
        {
            trimmed = trimmed.Substring(0, MaxLength);
        }

        return Sanitizer.Sanitize(trimmed).Trim();
    }
}
