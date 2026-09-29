using Microsoft.Extensions.Options;

namespace EvChargers.API.Configuration;

/// <summary>Browser origins allowed to call the API, bound from "Cors" (env vars: Cors__AllowedOrigins__0, __1, …).</summary>
public sealed class CorsOriginsOptions
{
    public const string SectionName = "Cors";

    /// <summary>The Vite dev server; only used as the Development default.</summary>
    public const string DevelopmentOrigin = "http://localhost:5173";

    public string[] AllowedOrigins { get; set; } = [];
}

/// <summary>
/// Outside Development: at least one origin, each an absolute https origin (scheme + host [+ port]),
/// with no path, query or trailing slash, because browsers compare the Origin header exactly.
/// </summary>
public sealed class CorsOriginsOptionsValidator(IHostEnvironment environment) : IValidateOptions<CorsOriginsOptions>
{
    public ValidateOptionsResult Validate(string? name, CorsOriginsOptions options)
    {
        if (environment.IsDevelopment()) return ValidateOptionsResult.Success;

        var origins = options.AllowedOrigins ?? [];
        if (origins.Length == 0 || origins.All(string.IsNullOrWhiteSpace))
        {
            return ValidateOptionsResult.Fail(
                "Cors:AllowedOrigins must list at least one origin outside Development " +
                "(set the Cors__AllowedOrigins__0 environment variable, e.g. https://your-app.vercel.app).");
        }

        var errors = origins
            .Select((origin, index) => (origin, index))
            .Where(o => !IsHttpsOrigin(o.origin))
            .Select(o => $"Cors:AllowedOrigins[{o.index}] ('{o.origin}') must be an https origin without path or trailing slash, " +
                         $"e.g. https://your-app.vercel.app (environment variable Cors__AllowedOrigins__{o.index}).")
            .ToList();

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }

    public static bool IsHttpsOrigin(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value == value.Trim()
        && !value.EndsWith('/')
        && Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && uri.AbsolutePath == "/"
        && string.IsNullOrEmpty(uri.Query)
        && string.IsNullOrEmpty(uri.Fragment)
        && string.IsNullOrEmpty(uri.UserInfo);
}
