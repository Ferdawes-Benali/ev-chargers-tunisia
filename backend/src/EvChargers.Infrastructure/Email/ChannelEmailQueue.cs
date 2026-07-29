using System.Threading.Channels;
using EvChargers.Application.Email;

namespace EvChargers.Infrastructure.Email;

public class ChannelEmailQueue : IEmailQueue
{
    // Limit the queue to 500 messages; writers wait when it is full.
    private readonly Channel<EmailMessage> _channel =
        Channel.CreateBounded<EmailMessage>(new BoundedChannelOptions(500)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true, // Enables channel optimizations for a single reader.
        });

    public ValueTask EnqueueAsync(EmailMessage message, CancellationToken ct = default) =>
        _channel.Writer.WriteAsync(message, ct);

    public ChannelReader<EmailMessage> Reader => _channel.Reader;
}