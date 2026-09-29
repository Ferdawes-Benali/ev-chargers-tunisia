using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using EvChargers.API.Configuration;
using Xunit;

namespace EvChargers.Tests;

public class CorsOriginsOptionsTests
{
    private static ValidateOptionsResult Validate(string environment, params string[] origins) =>
        new CorsOriginsOptionsValidator(new TestHostEnvironment(environment))
            .Validate(null, new CorsOriginsOptions { AllowedOrigins = origins });

    [Theory]
    [InlineData("https://ev-chargers-tunisia.vercel.app")]
    [InlineData("https://evchargers.tn")]
    [InlineData("https://staging.evchargers.tn:8443")]
    public void Production_accepts_https_origins(string origin) =>
        Validate("Production", origin).Succeeded.Should().BeTrue();

    [Fact]
    public void Production_requires_at_least_one_origin()
    {
        var result = Validate("Production");

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("Cors__AllowedOrigins__0");
    }

    [Theory]
    [InlineData("http://ev-chargers-tunisia.vercel.app")]   // not https
    [InlineData("https://ev-chargers-tunisia.vercel.app/")] // trailing slash
    [InlineData("https://ev-chargers-tunisia.vercel.app/app")]
    [InlineData("https://ev-chargers-tunisia.vercel.app?x=1")]
    [InlineData(" https://ev-chargers-tunisia.vercel.app")]
    [InlineData("ev-chargers-tunisia.vercel.app")]          // not absolute
    [InlineData("*")]
    public void Production_rejects_anything_but_a_bare_https_origin(string origin)
    {
        var result = Validate("Production", origin);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("Cors__AllowedOrigins__0");
    }

    [Fact]
    public void Failure_names_the_env_var_of_the_bad_entry()
    {
        var result = Validate("Production", "https://ok.vercel.app", "http://bad.example.com");

        result.FailureMessage.Should().Contain("Cors__AllowedOrigins__1").And.NotContain("__0");
    }

    [Fact]
    public void Development_allows_http_origins() =>
        Validate("Development", "http://localhost:4173").Succeeded.Should().BeTrue();
}

public class SupabaseOptionsTests
{
    private static ValidateOptionsResult Validate(string environment, string? url) =>
        new SupabaseOptionsValidator(new TestHostEnvironment(environment)).Validate(null, new SupabaseOptions { Url = url });

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Production_requires_the_url(string? url)
    {
        var result = Validate("Production", url);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("Supabase__Url");
    }

    [Theory]
    [InlineData("http://abcd.supabase.co")]
    [InlineData("abcd.supabase.co")]
    public void Production_requires_an_https_url(string url) =>
        Validate("Production", url).Failed.Should().BeTrue();

    [Fact]
    public void Production_accepts_an_https_url() =>
        Validate("Production", "https://abcd.supabase.co").Succeeded.Should().BeTrue();

    [Theory]
    [InlineData("https://abcd.supabase.co")]
    [InlineData("https://abcd.supabase.co/")]
    public void Authority_is_the_auth_endpoint(string url) =>
        new SupabaseOptions { Url = url }.Authority.Should().Be("https://abcd.supabase.co/auth/v1");
}

public class DeploymentServicesTests
{
    private static ServiceProvider Build(string environment, Dictionary<string, string?>? settings = null)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings ?? []).Build();
        return new ServiceCollection()
            .AddSingleton<IConfiguration>(config)
            .AddSingleton<IHostEnvironment>(new TestHostEnvironment(environment))
            .AddLogging()
            .AddFrontendCors()
            .AddSupabaseAuthentication()
            .BuildServiceProvider();
    }

    [Fact]
    public void Development_defaults_to_the_vite_origin_and_the_current_supabase_project()
    {
        using var provider = Build("Development");

        provider.Invoking(p => p.GetRequiredService<IStartupValidator>().Validate()).Should().NotThrow();
        provider.GetRequiredService<IOptions<CorsOriginsOptions>>().Value.AllowedOrigins.Should().Equal("http://localhost:5173");
        provider.GetRequiredService<IOptions<SupabaseOptions>>().Value.Url.Should().Be(SupabaseOptions.DevelopmentUrl);
    }

    [Fact]
    public void Production_fails_at_startup_without_cors_and_supabase_settings()
    {
        using var provider = Build("Production");

        provider.Invoking(p => p.GetRequiredService<IStartupValidator>().Validate())
            .Should().Throw<AggregateException>()
            .Which.InnerExceptions.Select(e => e.Message)
            .Should().Contain(m => m.Contains("Cors__AllowedOrigins__0"))
            .And.Contain(m => m.Contains("Supabase__Url"));
    }

    [Fact]
    public void Production_uses_the_configured_origins_and_supabase_project()
    {
        using var provider = Build("Production", new()
        {
            ["Cors:AllowedOrigins:0"] = "https://ev-chargers-tunisia.vercel.app",
            ["Supabase:Url"] = "https://abcd.supabase.co",
        });

        provider.Invoking(p => p.GetRequiredService<IStartupValidator>().Validate()).Should().NotThrow();

        var policy = provider.GetRequiredService<IOptions<CorsOptions>>().Value.GetPolicy(HostingExtensions.FrontendCorsPolicy)!;
        policy.Origins.Should().Equal("https://ev-chargers-tunisia.vercel.app");
        policy.AllowAnyHeader.Should().BeTrue();
        policy.AllowAnyMethod.Should().BeTrue();
        policy.SupportsCredentials.Should().BeFalse();

        var jwt = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(JwtBearerDefaults.AuthenticationScheme);
        jwt.Authority.Should().Be("https://abcd.supabase.co/auth/v1");
        jwt.TokenValidationParameters.ValidAudience.Should().Be("authenticated");
    }
}

public class ApiRateLimitingTests
{
    [Theory]
    [InlineData("GET", "/api/v1/geocode/autocomplete", ApiRateLimiting.External)]
    [InlineData("POST", "/api/v1/trips/plan", ApiRateLimiting.External)]
    [InlineData("POST", "/api/v1/reach/estimate", ApiRateLimiting.External)]
    [InlineData("GET", "/api/v1/stations/3f2b9c1e-0000-4000-8000-000000000001/companion", ApiRateLimiting.External)]
    [InlineData("GET", "/api/v1/stations/3f2b9c1e-0000-4000-8000-000000000001/companion/route", ApiRateLimiting.External)]
    [InlineData("GET", "/API/V1/GEOCODE/autocomplete", ApiRateLimiting.External)]
    [InlineData("POST", "/api/v1/stations", ApiRateLimiting.Writes)]
    [InlineData("PUT", "/api/v1/me/favorites/3f2b9c1e-0000-4000-8000-000000000001", ApiRateLimiting.Writes)]
    [InlineData("DELETE", "/api/v1/me/favorites/3f2b9c1e-0000-4000-8000-000000000001", ApiRateLimiting.Writes)]
    [InlineData("POST", "/api/v1/stations/3f2b9c1e-0000-4000-8000-000000000001/reviews", ApiRateLimiting.Writes)]
    [InlineData("GET", "/api/v1/stations", null)]
    [InlineData("GET", "/api/v1/stations/bbox", null)]
    [InlineData("GET", "/api/v1/geocoder", null)] // prefix match is per path segment
    [InlineData("GET", "/health", null)]
    [InlineData("GET", "/health/ready", null)]
    public void Requests_are_classified(string method, string path, string? expected) =>
        ApiRateLimiting.Classify(method, path).Should().Be(expected);

    [Theory]
    [InlineData("203.0.113.7", "203.0.113.7")]
    [InlineData("::ffff:203.0.113.7", "203.0.113.7")]
    [InlineData("2001:db8::1", "2001:db8::1")]
    public void Partition_key_is_the_client_ip(string remoteIp, string expected)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(remoteIp);

        ApiRateLimiting.ClientPartitionKey(context).Should().Be(expected);
    }

    [Fact]
    public void Partition_key_without_an_ip_is_shared() =>
        ApiRateLimiting.ClientPartitionKey(new DefaultHttpContext()).Should().Be("unknown");

    private static RateLimiterOptions Options(string environment) =>
        new ServiceCollection().AddApiRateLimiting(new TestHostEnvironment(environment))
            .BuildServiceProvider().GetRequiredService<IOptions<RateLimiterOptions>>().Value;

    private static DefaultHttpContext Request(string method, string path, string ip = "203.0.113.7")
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        return context;
    }

    /// <summary>How many requests in a row are let through, up to <paramref name="max"/>.</summary>
    private static int Allowed(RateLimiterOptions options, Func<DefaultHttpContext> request, int max)
    {
        for (var i = 0; i < max; i++)
        {
            using var lease = options.GlobalLimiter!.AttemptAcquire(request());
            if (!lease.IsAcquired) return i;
        }
        return max;
    }

    [Theory]
    [InlineData("Production", "GET", "/api/v1/geocode/autocomplete", 30)]
    [InlineData("Production", "POST", "/api/v1/stations", 20)]
    [InlineData("Production", "GET", "/api/v1/stations", 300)]
    [InlineData("Development", "GET", "/api/v1/geocode/autocomplete", 300)]
    [InlineData("Development", "POST", "/api/v1/stations", 200)]
    public void Limits_per_minute(string environment, string method, string path, int expected) =>
        Allowed(Options(environment), () => Request(method, path), 5000).Should().Be(expected);

    [Fact]
    public void Each_client_ip_has_its_own_budget()
    {
        var options = Options("Production");
        Allowed(options, () => Request("GET", "/api/v1/geocode/autocomplete", "203.0.113.7"), 100).Should().Be(30);

        Allowed(options, () => Request("GET", "/api/v1/geocode/autocomplete", "198.51.100.9"), 100).Should().Be(30);
    }

    [Fact]
    public void Health_checks_are_never_limited() =>
        Allowed(Options("Production"), () => Request("GET", "/health/ready"), 1000).Should().Be(1000);

    [Fact]
    public async Task Rejection_is_a_problem_details_with_retry_after()
    {
        var options = Options("Production");
        options.RejectionStatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
        Allowed(options, () => Request("POST", "/api/v1/stations"), 20);
        var context = Request("POST", "/api/v1/stations");
        context.Response.Body = new MemoryStream();
        using var lease = options.GlobalLimiter!.AttemptAcquire(context);
        lease.IsAcquired.Should().BeFalse();

        await options.OnRejected!(new OnRejectedContext { HttpContext = context, Lease = lease }, CancellationToken.None);

        int.Parse(context.Response.Headers.RetryAfter.ToString()).Should().BeInRange(1, 60);
        context.Response.ContentType.Should().StartWith("application/problem+json");
        context.Response.Body.Position = 0;
        using var body = await JsonDocument.ParseAsync(context.Response.Body);
        body.RootElement.GetProperty("status").GetInt32().Should().Be(429);
        body.RootElement.GetProperty("detail").GetString().Should().Be("Too many requests, please try again in a minute.");
    }
}
