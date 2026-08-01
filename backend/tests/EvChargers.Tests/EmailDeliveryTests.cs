using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using EvChargers.Infrastructure;
using EvChargers.Application.Email;
using EvChargers.Infrastructure.Email;
using Xunit;

namespace EvChargers.Tests;

public class EmailDispatcherTests
{
    private static readonly EmailMessage Target = new("user@example.com", "Hello", "<p>hi</p>");
    // Queued after Target: once it is sent, the dispatcher is done with Target (one email at a time)
    private static readonly EmailMessage Sentinel = new("sentinel@example.com", "Done", "<p>done</p>");

    private sealed class FakeSender(EmailSendResult resultForTarget) : IEmailSender
    {
        public ConcurrentQueue<EmailMessage> Calls { get; } = new();
        public TaskCompletionSource SentinelSent { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken ct)
        {
            Calls.Enqueue(message);
            if (message == Sentinel)
            {
                SentinelSent.TrySetResult();
                return Task.FromResult(EmailSendResult.Sent);
            }
            return Task.FromResult(resultForTarget);
        }
    }

    private static async Task<int> AttemptsFor(EmailSendResult result)
    {
        var sender = new FakeSender(result);
        var queue = new ChannelEmailQueue();
        using var provider = new ServiceCollection().AddSingleton<IEmailSender>(sender).BuildServiceProvider();
        var dispatcher = new EmailDispatcher(queue, provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<EmailDispatcher>.Instance)
        {
            BackoffUnit = TimeSpan.Zero,
        };

        await dispatcher.StartAsync(CancellationToken.None);
        await queue.EnqueueAsync(Target);
        await queue.EnqueueAsync(Sentinel);
        await sender.SentinelSent.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await dispatcher.StopAsync(CancellationToken.None);

        return sender.Calls.Count(m => m == Target);
    }

    [Fact]
    public async Task Sent_email_is_attempted_once() =>
        (await AttemptsFor(EmailSendResult.Sent)).Should().Be(1);

    [Fact]
    public async Task Permanent_failure_is_attempted_once() =>
        (await AttemptsFor(EmailSendResult.PermanentFailure)).Should().Be(1);

    [Fact]
    public async Task Transient_failure_is_retried_up_to_three_times() =>
        (await AttemptsFor(EmailSendResult.TransientFailure)).Should().Be(3);
}

public class ResendEmailSenderTests
{
    private sealed class FakeHandler(Func<HttpResponseMessage> respond) : HttpMessageHandler
    {
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return respond();
        }
    }

    private static readonly EmailMessage Message = new("amira@example.com", "Bienvenue", "<p>hi</p>");

    private sealed class CapturingLogger : ILogger<ResendEmailSender>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }

    private static (ResendEmailSender Sender, FakeHandler Handler) Create(
        Func<HttpResponseMessage> respond,
        string? apiKey = "re_test",
        string? redirectTo = null,
        ILogger<ResendEmailSender>? logger = null,
        string environment = "Development",
        string? fromAddress = "noreply@evchargers.tn")
    {
        var handler = new FakeHandler(respond);
        var email = Options.Create(new EmailOptions { FromAddress = fromAddress, DevRedirectTo = redirectTo });
        var resend = Options.Create(new ResendOptions { ApiKey = apiKey });
        var sender = new ResendEmailSender(new HttpClient(handler), email, resend, new TestHostEnvironment(environment),
            logger ?? NullLogger<ResendEmailSender>.Instance);
        return (sender, handler);
    }

    private static (string To, string Subject) SentPayload(FakeHandler handler)
    {
        using var doc = JsonDocument.Parse(handler.LastBody!);
        return (doc.RootElement.GetProperty("to")[0].GetString()!, doc.RootElement.GetProperty("subject").GetString()!);
    }

    [Fact]
    public async Task Development_with_redirect_replaces_the_recipient()
    {
        var (sender, handler) = Create(() => new HttpResponseMessage(HttpStatusCode.OK), redirectTo: "dev@example.com", environment: "Development");

        var result = await sender.SendAsync(Message, CancellationToken.None);

        result.Should().Be(EmailSendResult.Sent);
        SentPayload(handler).Should().Be(("dev@example.com", "[DEV → amira@example.com] Bienvenue"));
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task Outside_development_the_redirect_is_ignored_and_the_real_recipient_is_kept(string environment)
    {
        var (sender, handler) = Create(() => new HttpResponseMessage(HttpStatusCode.OK), redirectTo: "dev@example.com", environment: environment);

        var result = await sender.SendAsync(Message, CancellationToken.None);

        result.Should().Be(EmailSendResult.Sent);
        SentPayload(handler).Should().Be(("amira@example.com", "Bienvenue"));
    }

    [Theory]
    [InlineData(null, "Development")]
    [InlineData("", "Development")]
    [InlineData("  ", "Development")]
    [InlineData(null, "Production")]
    public async Task Without_a_redirect_the_real_recipient_is_kept(string? redirectTo, string environment)
    {
        var (sender, handler) = Create(() => new HttpResponseMessage(HttpStatusCode.OK), redirectTo: redirectTo, environment: environment);

        await sender.SendAsync(Message, CancellationToken.None);

        SentPayload(handler).Should().Be(("amira@example.com", "Bienvenue"));
    }

    [Fact]
    public async Task From_header_combines_display_name_and_address()
    {
        var (sender, handler) = Create(() => new HttpResponseMessage(HttpStatusCode.OK));

        await sender.SendAsync(Message, CancellationToken.None);

        using var doc = JsonDocument.Parse(handler.LastBody!);
        doc.RootElement.GetProperty("from").GetString().Should().Be("EV Chargers Tunisia <noreply@evchargers.tn>");
    }

    [Fact]
    public async Task Missing_sender_address_is_permanent_and_sends_nothing()
    {
        var (sender, handler) = Create(() => new HttpResponseMessage(HttpStatusCode.OK), fromAddress: null);

        (await sender.SendAsync(Message, CancellationToken.None)).Should().Be(EmailSendResult.PermanentFailure);
        handler.LastBody.Should().BeNull();
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, EmailSendResult.TransientFailure)]
    [InlineData(HttpStatusCode.InternalServerError, EmailSendResult.TransientFailure)]
    [InlineData(HttpStatusCode.ServiceUnavailable, EmailSendResult.TransientFailure)]
    [InlineData(HttpStatusCode.Forbidden, EmailSendResult.PermanentFailure)]
    [InlineData(HttpStatusCode.BadRequest, EmailSendResult.PermanentFailure)]
    [InlineData(HttpStatusCode.UnprocessableEntity, EmailSendResult.PermanentFailure)]
    public async Task Http_status_is_mapped(HttpStatusCode status, EmailSendResult expected)
    {
        var (sender, _) = Create(() => new HttpResponseMessage(status) { Content = new StringContent("{}") });

        (await sender.SendAsync(Message, CancellationToken.None)).Should().Be(expected);
    }

    [Fact]
    public async Task Network_error_and_timeout_are_transient()
    {
        var (network, _) = Create(() => throw new HttpRequestException("down"));
        var (timeout, _) = Create(() => throw new TaskCanceledException("timeout"));

        (await network.SendAsync(Message, CancellationToken.None)).Should().Be(EmailSendResult.TransientFailure);
        (await timeout.SendAsync(Message, CancellationToken.None)).Should().Be(EmailSendResult.TransientFailure);
    }

    [Fact]
    public async Task Cancellation_by_our_token_is_not_treated_as_timeout()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var (sender, _) = Create(() => throw new TaskCanceledException());

        (await sender.SendAsync(Message, cts.Token)).Should().Be(EmailSendResult.PermanentFailure);
    }

    [Fact]
    public async Task Missing_api_key_or_unexpected_error_is_permanent()
    {
        var (noKey, handler) = Create(() => new HttpResponseMessage(HttpStatusCode.OK), apiKey: null);
        var (broken, _) = Create(() => throw new InvalidOperationException("bug"));

        (await noKey.SendAsync(Message, CancellationToken.None)).Should().Be(EmailSendResult.PermanentFailure);
        handler.LastBody.Should().BeNull(); // nothing was sent
        (await broken.SendAsync(Message, CancellationToken.None)).Should().Be(EmailSendResult.PermanentFailure);
    }

    [Fact]
    public async Task Email_addresses_in_the_logged_response_body_are_masked()
    {
        var logger = new CapturingLogger();
        const string body = """{"message":"You can only send testing emails to your own email address (ferdawes.benali11@gmail.com). To send to amira@example.tn, verify a domain."}""";
        var (sender, _) = Create(() => new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StringContent(body) }, logger: logger);

        await sender.SendAsync(Message, CancellationToken.None);

        var logged = logger.Messages.Should().ContainSingle().Subject;
        logged.Should().Contain("You can only send testing emails")
            .And.Contain("f***@gmail.com")
            .And.Contain("a***@example.tn")
            .And.NotContain("ferdawes.benali11")
            .And.NotContain("amira@");
    }
}

public class EmailOptionsValidationTests
{
    private static ServiceProvider Build(string environment, Dictionary<string, string?> settings)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new ServiceCollection()
            .AddSingleton<IConfiguration>(config)
            .AddSingleton<IHostEnvironment>(new TestHostEnvironment(environment))
            .AddLogging()
            .AddEmailServices()
            .BuildServiceProvider();
    }

    private static readonly Dictionary<string, string?> Complete = new()
    {
        ["Email:FromAddress"] = "noreply@evchargers.tn",
        ["Email:FromName"] = "EV Chargers Tunisia",
        ["Resend:ApiKey"] = "re_live",
    };

    [Fact]
    public void Production_with_address_and_key_starts()
    {
        using var provider = Build("Production", Complete);

        provider.Invoking(p => p.GetRequiredService<IStartupValidator>().Validate()).Should().NotThrow();
        provider.GetRequiredService<IOptions<EmailOptions>>().Value.FromAddress.Should().Be("noreply@evchargers.tn");
        provider.GetRequiredService<IOptions<ResendOptions>>().Value.ApiKey.Should().Be("re_live");
    }

    [Theory]
    [InlineData("Email:FromAddress", "Email:FromAddress is required")]
    [InlineData("Resend:ApiKey", "Resend:ApiKey is required")]
    public void Production_fails_at_startup_without_a_required_setting(string missing, string expectedMessage)
    {
        var settings = new Dictionary<string, string?>(Complete) { [missing] = null };
        using var provider = Build("Production", settings);

        provider.Invoking(p => p.GetRequiredService<IStartupValidator>().Validate())
            .Should().Throw<OptionsValidationException>()
            .Which.Message.Should().Contain(expectedMessage);
    }

    [Fact]
    public void Production_rejects_a_display_name_in_the_address()
    {
        var settings = new Dictionary<string, string?>(Complete) { ["Email:FromAddress"] = "EV Chargers <noreply@evchargers.tn>" };
        using var provider = Build("Production", settings);

        provider.Invoking(p => p.GetRequiredService<IStartupValidator>().Validate())
            .Should().Throw<OptionsValidationException>()
            .Which.Message.Should().Contain("bare address");
    }

    [Fact]
    public void Development_defaults_to_the_resend_test_sender_and_does_not_require_a_key()
    {
        using var provider = Build("Development", new Dictionary<string, string?>());

        provider.Invoking(p => p.GetRequiredService<IStartupValidator>().Validate()).Should().NotThrow();
        provider.GetRequiredService<IOptions<EmailOptions>>().Value.FromAddress.Should().Be("onboarding@resend.dev");
    }

    [Fact]
    public void Resend_test_sender_is_not_a_default_outside_development()
    {
        using var provider = Build("Production", new Dictionary<string, string?> { ["Resend:ApiKey"] = "re_live" });

        provider.Invoking(p => p.GetRequiredService<IOptions<EmailOptions>>().Value)
            .Should().Throw<OptionsValidationException>();
    }
}

public class EmailConfigurationCheckTests
{
    private sealed class CapturingLogger : ILogger<EmailConfigurationCheck>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }

    private static async Task<CapturingLogger> RunCheck(string environment, string? redirectTo)
    {
        var logger = new CapturingLogger();
        var check = new EmailConfigurationCheck(
            Options.Create(new EmailOptions { FromAddress = "noreply@evchargers.tn", DevRedirectTo = redirectTo }),
            new TestHostEnvironment(environment), logger);
        await check.StartAsync(CancellationToken.None);
        return logger;
    }

    [Fact]
    public async Task Warns_when_the_redirect_is_set_outside_development()
    {
        var logger = await RunCheck("Production", "ferdawes@example.com");

        var entry = logger.Entries.Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Warning);
        entry.Message.Should().Contain("ignored").And.Contain("Production")
            .And.Contain("Email:DevRedirectTo").And.NotContain("ferdawes").And.NotContain("example.com");
    }

    [Theory]
    [InlineData("Development", "dev@example.com")]
    [InlineData("Production", null)]
    [InlineData("Production", "  ")]
    public async Task Stays_silent_otherwise(string environment, string? redirectTo) =>
        (await RunCheck(environment, redirectTo)).Entries.Should().BeEmpty();
}

internal sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
{
    public string EnvironmentName { get; set; } = environmentName;
    public string ApplicationName { get; set; } = "EvChargers.Tests";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
