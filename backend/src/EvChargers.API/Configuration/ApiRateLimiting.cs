using System.Net;
using System.Text.RegularExpressions;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EvChargers.API.Configuration;

/// <summary>
/// Per-client-IP rate limits. Every request counts against the "global" budget; some also against a stricter one:
/// "external" for endpoints that call paid or rate-limited third-party APIs (ORS, Overpass, weather),
/// "writes" for other POST/PUT/DELETE requests. Health checks are never limited.
/// Limits are 10x higher in Development, so they stay on without getting in the way.
/// </summary>
public static partial class ApiRateLimiting
{
    public const string External = "external";
    public const string Writes = "writes";
    public const string Global = "global";

    public const int ExternalPerMinute = 30;
    public const int WritesPerMinute = 20;
    public const int GlobalPerMinute = 300;
    public const int DevelopmentMultiplier = 10;

    public const string RejectionMessage = "Too many requests, please try again in a minute.";

    private static readonly string[] ExternalPrefixes = ["/api/v1/geocode", "/api/v1/trips", "/api/v1/reach"];

    [GeneratedRegex(@"^/api/v1/stations/[^/]+/companion(/|$)", RegexOptions.IgnoreCase)]
    private static partial Regex CompanionPath();

    public static bool IsHealthPath(PathString path) => path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The stricter policy a request falls under, or null when only the global limit applies.
    /// "external" wins over "writes": POST /api/v1/trips/plan and /api/v1/reach/estimate are external lookups.
    /// </summary>
    public static string? Classify(string method, PathString path)
    {
        if (IsHealthPath(path)) return null;
        if (ExternalPrefixes.Any(prefix => path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase))
            || CompanionPath().IsMatch(path.Value ?? ""))
        {
            return External;
        }
        return HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsDelete(method) ? Writes : null;
    }

    /// <summary>
    /// The client's IP, read after UseForwardedHeaders has replaced the proxy's address with the real client's.
    /// IPv4 clients seen over IPv6 ("::ffff:1.2.3.4") share the IPv4 partition.
    /// </summary>
    public static string ClientPartitionKey(HttpContext context)
    {
        var ip = context.Connection.RemoteIpAddress;
        if (ip is null) return "unknown";
        return (ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip).ToString();
    }

    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IHostEnvironment environment)
    {
        var multiplier = environment.IsDevelopment() ? DevelopmentMultiplier : 1;

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
                PartitionedRateLimiter.Create<HttpContext, string>(context =>
                    IsHealthPath(context.Request.Path)
                        ? RateLimitPartition.GetNoLimiter("health")
                        : PerMinute($"{Global}:{ClientPartitionKey(context)}", GlobalPerMinute * multiplier)),
                PartitionedRateLimiter.Create<HttpContext, string>(context =>
                    Classify(context.Request.Method, context.Request.Path) switch
                    {
                        External => PerMinute($"{External}:{ClientPartitionKey(context)}", ExternalPerMinute * multiplier),
                        Writes => PerMinute($"{Writes}:{ClientPartitionKey(context)}", WritesPerMinute * multiplier),
                        _ => RateLimitPartition.GetNoLimiter("none"),
                    }));

            options.OnRejected = async (context, ct) =>
            {
                var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var wait) && wait > TimeSpan.Zero
                    ? wait
                    : TimeSpan.FromMinutes(1);
                var response = context.HttpContext.Response;
                response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
                await response.WriteAsJsonAsync(new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Too many requests",
                    Detail = RejectionMessage,
                }, options: (System.Text.Json.JsonSerializerOptions?)null, contentType: "application/problem+json", ct);
            };
        });

        return services;
    }

    private static RateLimitPartition<string> PerMinute(string key, int permits) =>
        RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permits,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        });
}
