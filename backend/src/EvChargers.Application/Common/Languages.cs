namespace EvChargers.Application.Common;

/// <summary>Languages the app (and its emails) are available in. French is the default.</summary>
public static class Languages
{
    public const string Default = "fr";
    public static readonly IReadOnlyList<string> Supported = ["fr", "ar", "en"];

    public static bool IsSupported(string? code) => code is not null && Supported.Contains(code);

    /// <summary>A supported code as-is (case-insensitive); anything else becomes French.</summary>
    public static string Normalize(string? code)
    {
        var lower = code?.Trim().ToLowerInvariant();
        return IsSupported(lower) ? lower! : Default;
    }
}
