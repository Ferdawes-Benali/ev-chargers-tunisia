using System.Collections.Concurrent;
using System.Threading.Channels;
using EvChargers.Application.Interfaces;

namespace EvChargers.Infrastructure.Companion;

/// <summary>Stations waiting for a places refresh, read by <see cref="CompanionWarmupService"/>. Each station is queued at most once.</summary>
public class ChannelPlacesRefreshQueue : IPlacesRefreshQueue
{
    private const int Capacity = 1000;

    // Wait mode so TryWrite reports a full queue instead of silently dropping
    private readonly Channel<Guid> _channel = Channel.CreateBounded<Guid>(new BoundedChannelOptions(Capacity)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true,
    });
    private readonly ConcurrentDictionary<Guid, byte> _waiting = new();

    public bool Request(Guid stationId)
    {
        if (!_waiting.TryAdd(stationId, 0)) return false;
        if (_channel.Writer.TryWrite(stationId)) return true;
        _waiting.TryRemove(stationId, out _);
        return false;
    }

    public bool TryDequeue(out Guid stationId)
    {
        if (!_channel.Reader.TryRead(out stationId)) return false;
        _waiting.TryRemove(stationId, out _);
        return true;
    }

    public ValueTask<bool> WaitToReadAsync(CancellationToken ct) => _channel.Reader.WaitToReadAsync(ct);
}
