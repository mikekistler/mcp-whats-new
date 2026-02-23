using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using ModelContextProtocol.Server;

// Wraps DistributedCacheEventStreamStore to add session-level stream cleanup.
// Tracks which streams belong to each session and provides DeleteStreamsForSessionAsync
// to proactively remove all cached data when a session ends.
public sealed class SessionTrackingEventStreamStore : ISseEventStreamStore
{
    private readonly DistributedCacheEventStreamStore _innerStore;
    private readonly IDistributedCache _cache;
    private readonly ConcurrentDictionary<string, ConcurrentBag<string>> _sessionStreams = new();

    private static readonly DistributedCacheEventStreamStoreOptions DefaultOptions = new()
    {
        // Timeouts for individual SSE events in the cache
        EventSlidingExpiration = TimeSpan.FromMinutes(5),
        EventAbsoluteExpiration = TimeSpan.FromMinutes(30),

        // Timeouts for per-stream metadata (typically longer than event entries)
        MetadataSlidingExpiration = TimeSpan.FromMinutes(30),
        MetadataAbsoluteExpiration = TimeSpan.FromHours(2),
    };

    public SessionTrackingEventStreamStore(IDistributedCache cache, DistributedCacheEventStreamStoreOptions? options = null)
    {
        _cache = cache;
        _innerStore = new DistributedCacheEventStreamStore(cache, options ?? DefaultOptions);
    }

    public ValueTask<ISseEventStreamWriter> CreateStreamAsync(SseEventStreamOptions options, CancellationToken cancellationToken = default)
    {
        _sessionStreams.GetOrAdd(options.SessionId, _ => new ConcurrentBag<string>()).Add(options.StreamId);
        return _innerStore.CreateStreamAsync(options, cancellationToken);
    }

    public ValueTask<ISseEventStreamReader?> GetStreamReaderAsync(string lastEventId, CancellationToken cancellationToken = default)
    {
        return _innerStore.GetStreamReaderAsync(lastEventId, cancellationToken);
    }

    /// <summary>
    /// Deletes all streams and their events for the specified session from the cache.
    /// </summary>
    public async Task DeleteStreamsForSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (!_sessionStreams.TryRemove(sessionId, out var streamIds))
        {
            return;
        }

        var sessionBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(sessionId));

        foreach (var streamId in streamIds)
        {
            var streamBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(streamId));
            var metadataKey = $"mcp:sse:v1:meta:{sessionBase64}:{streamBase64}";

            var metadataBytes = await _cache.GetAsync(metadataKey, cancellationToken);
            if (metadataBytes is not null)
            {
                var metadata = JsonSerializer.Deserialize<StreamMetadataDto>(metadataBytes);
                if (metadata is not null)
                {
                    // Remove all cached events for this stream
                    for (long seq = 1; seq <= metadata.LastSequence; seq++)
                    {
                        var eventKey = $"mcp:sse:v1:event:{sessionBase64}:{streamBase64}:{seq}";
                        await _cache.RemoveAsync(eventKey, cancellationToken);
                    }
                }

                await _cache.RemoveAsync(metadataKey, cancellationToken);
            }
        }
    }

    // Minimal DTO to deserialize stream metadata from the cache.
    private sealed class StreamMetadataDto
    {
        public long LastSequence { get; set; }
    }
}
