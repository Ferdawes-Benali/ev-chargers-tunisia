using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using EvChargers.Application.Email;

namespace EvChargers.Infrastructure.Email;

public class ResendEmailSender : IEmailSender
{
    private readonly HttpClient _http;
    private readonly ILogger<ResendEmailSender> _logger;
    private readonly string? _apiKey;
    private readonly string? _from;
    private readonly string? _devRedirectTo;

    public ResendEmailSender(
        HttpClient http,
        IOptions<EmailOptions> emailOptions,
        IOptions<ResendOptions> resendOptions,
        IHostEnvironment environment,
        ILogger<ResendEmailSender> logger)
    {
        _http = http;
        _logger = logger;
        var email = emailOptions.Value;
        _apiKey = resendOptions.Value.ApiKey;
        _from = string.IsNullOrWhiteSpace(email.FromAddress) ? null
            : string.IsNullOrWhiteSpace(email.FromName) ? email.FromAddress.Trim()
            : $"{email.FromName.Trim()} <{email.FromAddress.Trim()}>";
        // Resend test mode only delivers to the account owner, so redirect emails, but only in Development:
        // anywhere else real users must receive their own emails.
        _devRedirectTo = environment.IsDevelopment() && !string.IsNullOrWhiteSpace(email.DevRedirectTo)
            ? email.DevRedirectTo.Trim()
            : null;
        _http.BaseAddress = new Uri("https://api.resend.com/");
    }

    public async Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            _logger.LogWarning("Resend API key not configured; email not sent.");
            return EmailSendResult.PermanentFailure;
        }
        if (_from is null)
        {
            // Unreachable outside Development (startup validation), kept as a safety net
            _logger.LogWarning("Email sender address not configured; email not sent.");
            return EmailSendResult.PermanentFailure;
        }

        var to = message.To;
        var subject = message.Subject;
        if (_devRedirectTo is not null)
        {
            subject = $"[DEV → {message.To}] {subject}";
            to = _devRedirectTo;
        }

        try
        {
            var body = new { from = _from, to = new[] { to }, subject, html = message.Html };
            using var request = new HttpRequestMessage(HttpMethod.Post, "emails")
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            using var response = await _http.SendAsync(request, ct);
            if (response.IsSuccessStatusCode) return EmailSendResult.Sent;

            var status = (int)response.StatusCode;
            var transient = status == 429 || status >= 500;
            _logger.LogWarning("Resend rejected the email ({Status}, transient: {Transient}): {Body}",
                status, transient, EmailMasking.MaskAll(await response.Content.ReadAsStringAsync(ct)));
            return transient ? EmailSendResult.TransientFailure : EmailSendResult.PermanentFailure;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Network problem reaching Resend (transient: {Transient})", true);
            return EmailSendResult.TransientFailure;
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            // Not our token, so this is the HttpClient timeout
            _logger.LogWarning(ex, "Resend request timed out (transient: {Transient})", true);
            return EmailSendResult.TransientFailure;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error sending email via Resend (transient: {Transient})", false);
            return EmailSendResult.PermanentFailure;
        }
    }
}
