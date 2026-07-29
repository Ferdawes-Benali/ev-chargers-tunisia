using System.Threading.Channels;
using EvChargers.Application.Email;

namespace EvChargers.Infrastructure.Email;

/// <summary>
/// In-memory, thread-safe queue of emails. Requests write, the EmailDispatcher reads.
/// Registered as a singleton: one shared line for the whole app.
/// </summary>
public class ChannelEmailQueue : IEmailQueue
{
    // Bounded = at most 500 waiting emails, so a bug can't eat all the memory.
    // If it's ever full, the writer waits for a free slot instead of losing an email.
    private readonly Channel<EmailMessage> _channel =
        Channel.CreateBounded<EmailMessage>(new BoundedChannelOptions(500)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true, // only the dispatcher reads — lets .NET optimize
        });

    public ValueTask EnqueueAsync(EmailMessage message, CancellationToken ct = default) =>
        _channel.Writer.WriteAsync(message, ct);

    /// <summary>Used only by the EmailDispatcher.</summary>
    public ChannelReader<EmailMessage> Reader => _channel.Reader;
}