using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using EvChargers.Application.Email;

namespace EvChargers.Infrastructure.Email;

public class ResendEmailSender : IEmailSender
{
    private readonly HttpClient _http;
    private readonly ILogger<ResendEmailSender> _logger;
    private readonly string? _apiKey;
    private readonly string _from;
    private readonly string? _devRedirectTo;

    public ResendEmailSender(HttpClient http, IConfiguration config, ILogger<ResendEmailSender> logger)
    {
        _http = http;
        _logger = logger;
        _apiKey = config["ResendApiKey"];
        _from = config["EmailFrom"] ?? "EV Chargers Tunisia <onboarding@resend.dev>";
        // Resend test mode only delivers to the account owner, so redirect development emails.
        _devRedirectTo = string.IsNullOrWhiteSpace(config["EmailDevRedirectTo"]) ? null : config["EmailDevRedirectTo"]!.Trim();
        _http.BaseAddress = new Uri("https://api.resend.com/");
    }

    public async Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            _logger.LogWarning("Resend API key not configured; email not sent.");
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
