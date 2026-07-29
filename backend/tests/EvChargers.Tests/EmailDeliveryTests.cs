using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
        Func<HttpResponseMessage> respond, string? apiKey = "re_test", string? redirectTo = null, ILogger<ResendEmailSender>? logger = null)
    {
        var handler = new FakeHandler(respond);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ResendApiKey"] = apiKey,
            ["EmailDevRedirectTo"] = redirectTo,
        }).Build();
        return (new ResendEmailSender(new HttpClient(handler), config, logger ?? NullLogger<ResendEmailSender>.Instance), handler);
    }

    private static (string To, string Subject) SentPayload(FakeHandler handler)
    {
        using var doc = JsonDocument.Parse(handler.LastBody!);
        return (doc.RootElement.GetProperty("to")[0].GetString()!, doc.RootElement.GetProperty("subject").GetString()!);
    }

    [Fact]
    public async Task Redirect_changes_recipient_and_subject()
    {
        var (sender, handler) = Create(() => new HttpResponseMessage(HttpStatusCode.OK), redirectTo: "dev@example.com");

        var result = await sender.SendAsync(Message, CancellationToken.None);

        result.Should().Be(EmailSendResult.Sent);
        SentPayload(handler).Should().Be(("dev@example.com", "[DEV → amira@example.com] Bienvenue"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task No_redirect_when_setting_is_empty(string? redirectTo)
    {
        var (sender, handler) = Create(() => new HttpResponseMessage(HttpStatusCode.OK), redirectTo: redirectTo);

        await sender.SendAsync(Message, CancellationToken.None);

        SentPayload(handler).Should().Be(("amira@example.com", "Bienvenue"));
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
