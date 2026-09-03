namespace CvBuilder.Api.Api;

/// <summary>
/// Field hygiene for text arriving from a save file: trim, clamp to a sane length, and
/// fall back when empty. Every string the API accepts is untrusted.
/// </summary>
internal static class FieldText
{
    public static string Clamp(string? value, int maxLength, string fallback = "")
    {
        var trimmed = (value ?? "").Trim();
        if (trimmed.Length == 0) return fallback;
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
