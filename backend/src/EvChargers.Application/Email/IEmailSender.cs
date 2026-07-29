namespace EvChargers.Application.Email;

public enum EmailSendResult
{
    Sent,
    /// <summary>Worth retrying: rate limit, server error, network problem.</summary>
    TransientFailure,
    /// <summary>Retrying won't help: invalid key, rejected recipient, bad request.</summary>
    PermanentFailure,
}

/// <summary>Actually delivers one email. Never throws.</summary>
public interface IEmailSender
{
    Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken ct);
}