namespace EvChargers.Application.Email;

/// <summary>Put an email in line to be sent in the background. Returns immediately.</summary>
public interface IEmailQueue
{
    ValueTask EnqueueAsync(EmailMessage message, CancellationToken ct = default);
}