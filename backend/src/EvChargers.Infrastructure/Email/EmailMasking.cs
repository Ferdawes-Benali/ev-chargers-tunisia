using System.Text.RegularExpressions;

namespace EvChargers.Infrastructure.Email;

/// <summary>Email addresses are personal data: mask them before they reach the logs.</summary>
public static partial class EmailMasking
{
    /// <summary>"ferdawes@gmail.com" → "f***@gmail.com".</summary>
    public static string Mask(string email)
    {
        var at = email.IndexOf('@');
        return at <= 1 ? "***" : $"{email[0]}***{email[at..]}";
    }

    /// <summary>Masks every email address found in free text (e.g. an API error body).</summary>
    public static string MaskAll(string text) => EmailAddress().Replace(text, m => Mask(m.Value));

    [GeneratedRegex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}")]
    private static partial Regex EmailAddress();
}
