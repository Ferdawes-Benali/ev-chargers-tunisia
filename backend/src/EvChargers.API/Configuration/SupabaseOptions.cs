using Microsoft.Extensions.Options;

namespace EvChargers.API.Configuration;

/// <summary>Supabase project that issues the users' JWTs, bound from "Supabase" (env var: Supabase__Url).</summary>
public sealed class SupabaseOptions
{
    public const string SectionName = "Supabase";

    /// <summary>The project used before this setting existed; only used as the Development default.</summary>
    public const string DevelopmentUrl = "https://ypeieloulsktqgwwwtft.supabase.co";

    /// <summary>Project URL, e.g. https://abcd.supabase.co (the same value as the frontend's VITE_SUPABASE_URL).</summary>
    public string? Url { get; set; }

    /// <summary>JWT issuer / OpenID authority of Supabase Auth.</summary>
    public string Authority => $"{Url?.Trim().TrimEnd('/')}/auth/v1";
}

/// <summary>Outside Development: Url is required and must be an absolute https URL.</summary>
public sealed class SupabaseOptionsValidator(IHostEnvironment environment) : IValidateOptions<SupabaseOptions>
{
    public ValidateOptionsResult Validate(string? name, SupabaseOptions options)
    {
        if (environment.IsDevelopment()) return ValidateOptionsResult.Success;

        if (string.IsNullOrWhiteSpace(options.Url))
        {
            return ValidateOptionsResult.Fail(
                "Supabase:Url is required outside Development (set the Supabase__Url environment variable, e.g. https://your-project.supabase.co).");
        }

        return Uri.TryCreate(options.Url.Trim(), UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                $"Supabase:Url ('{options.Url}') must be an absolute https URL such as https://your-project.supabase.co (environment variable Supabase__Url).");
    }
}
