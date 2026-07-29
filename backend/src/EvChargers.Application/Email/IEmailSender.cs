namespace EvChargers.Application.Email;

/// <summary>Actually delivers one email. Returns false on failure (never throws).</summary>
public interface IEmailSender
{
    Task<bool> SendAsync(EmailMessage message, CancellationToken ct);
}