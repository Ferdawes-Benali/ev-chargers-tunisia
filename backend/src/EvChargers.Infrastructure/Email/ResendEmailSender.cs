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

    public ResendEmailSender(HttpClient http, IConfiguration config, ILogger<ResendEmailSender> logger)
    {
        _http = http;
        _logger = logger;
        _apiKey = config["ResendApiKey"];
        // Resend's test sender until we own a verified domain (Week 12)
        _from = config["EmailFrom"] ?? "EV Chargers Tunisia <onboarding@resend.dev>";
        _http.BaseAddress = new Uri("https://api.resend.com/");
    }

    public async Task<bool> SendAsync(EmailMessage message, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            _logger.LogWarning("Resend API key not configured; email not sent.");
            return false;
        }

        try
        {
            var body = new { from = _from, to = new[] { message.To }, subject = message.Subject, html = message.Html };

            using var request = new HttpRequestMessage(HttpMethod.Post, "emails")
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
            };
            // Unlike ORS, Resend uses the standard "Bearer <key>" format, so the normal header API works.
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            var response = await _http.SendAsync(request, ct);
            if (response.IsSuccessStatusCode) return true;

            var error = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("Resend rejected the email ({Status}): {Body}", response.StatusCode, error);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sending email via Resend threw an exception");
            return false;
        }
    }
}