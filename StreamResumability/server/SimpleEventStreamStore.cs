using System.Collections.Concurrent;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

// Provides storage and retrieval of SSE event streams, enabling resumability and redelivery of events.
public sealed class SimpleSseEventStreamStore : ISseEventStreamStore
{
    // Holds the state for each stream by a composite key of session ID and stream ID
    private readonly ConcurrentDictionary<string, StreamState> _streams = new();
    // Maps event IDs to their corresponding stream and sequence number
    private readonly ConcurrentDictionary<string, (StreamState Stream, long Sequence)> _eventLookup = new();
    // Tracks stored event IDs and reconnection intervals for testing purposes
    private readonly List<string> _storedEventIds = [];
    private readonly List<TimeSpan> _storedReconnectionIntervals = [];
    private readonly object _storedEventIdsLock = new();
    private long _globalSequence;

    /// <summary>
    /// Gets the list of stored event IDs in order.
    /// </summary>
    public IReadOnlyList<string> StoredEventIds
    {
        get
        {
            lock (_storedEventIdsLock)
            {
                return [.. _storedEventIds];
            }
        }
    }

    /// <summary>
    /// Gets the list of stored reconnection intervals in order.
    /// </summary>
    public IReadOnlyList<TimeSpan> StoredReconnectionIntervals
    {
        get
        {
            lock (_storedEventIdsLock)
            {
                return [.. _storedReconnectionIntervals];
            }
        }
    }

    // Creates a new SSE event stream with the specified options.
    // - options: The configuration options for the new stream.
    // - cancellationToken: A token to cancel the operation.
    // Returns a writer for the newly created event stream.
    public ValueTask<ISseEventStreamWriter> CreateStreamAsync(SseEventStreamOptions options, CancellationToken cancellationToken = default)
    {
        var streamKey = GetStreamKey(options.SessionId, options.StreamId);
        var state = new StreamState(options.SessionId, options.StreamId, options.Mode);
        if (!_streams.TryAdd(streamKey, state))
        {
            throw new InvalidOperationException($"A stream with key '{streamKey}' has already been created.");
        }
        var writer = new SimpleEventStreamWriter(this, state);
        return new ValueTask<ISseEventStreamWriter>(writer);
    }

    // Gets a reader for an existing event stream based on the last event ID.
    // - lastEventId: The ID of the last event received by the client, used to resume from that point.
    // - cancellationToken: A token to cancel the operation.
    // Returns a reader for the event stream, or <c>null</c> if no matching stream is found.
    public ValueTask<ISseEventStreamReader?> GetStreamReaderAsync(string lastEventId, CancellationToken cancellationToken = default)
    {
        // Look up the event by its ID to find which stream it belongs to
        if (!_eventLookup.TryGetValue(lastEventId, out var lookup))
        {
            return new ValueTask<ISseEventStreamReader?>((ISseEventStreamReader?)null);
        }

        var reader = new SimpleEventStreamReader(lookup.Stream, lookup.Sequence);
        return new ValueTask<ISseEventStreamReader?>(reader);
    }

    /// <summary>
    /// Deletes all streams associated with the specified session ID.
    /// </summary>
    /// <param name="sessionId">The session ID whose streams should be deleted.</param>
    public void DeleteStreamsForSession(string sessionId)
    {
        // Find all stream keys that belong to this session
        var keysToRemove = _streams.Keys.Where(key => key.StartsWith($"{sessionId}:")).ToList();

        foreach (var key in keysToRemove)
        {
            if (_streams.TryRemove(key, out var streamState))
            {
                // Remove all event lookups associated with this stream
                var eventIdsToRemove = _eventLookup
                    .Where(kvp => kvp.Value.Stream == streamState)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var eventId in eventIdsToRemove)
                {
                    _eventLookup.TryRemove(eventId, out _);
                }

                // Complete the stream to signal any waiting readers
                streamState.Complete();
            }
        }
    }

    private string GenerateEventId() => Interlocked.Increment(ref _globalSequence).ToString();

    private void TrackEvent(string eventId, StreamState stream, long sequence, TimeSpan? reconnectionInterval = null)
    {
        _eventLookup[eventId] = (stream, sequence);
        lock (_storedEventIdsLock)
        {
            _storedEventIds.Add(eventId);
            if (reconnectionInterval.HasValue)
            {
                _storedReconnectionIntervals.Add(reconnectionInterval.Value);
            }
        }
    }

    private static string GetStreamKey(string sessionId, string streamId) => $"{sessionId}:{streamId}";

    // Holds the state for a single stream.
    private sealed class StreamState
    {
        private readonly List<(SseItem<JsonRpcMessage?> Item, long Sequence)> _events = [];
        private readonly object _lock = new();
        private TaskCompletionSource _newEventSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private long _sequence;

        public StreamState(string sessionId, string streamId, SseEventStreamMode mode)
        {
            SessionId = sessionId;
            StreamId = streamId;
            Mode = mode;
        }

        public string SessionId { get; }
        public string StreamId { get; }
        public SseEventStreamMode Mode { get; set; }
        public bool IsCompleted { get; private set; }

        public long NextSequence() => Interlocked.Increment(ref _sequence);

        public void AddEvent(SseItem<JsonRpcMessage?> item, long sequence)
        {
            lock (_lock)
            {
                if (IsCompleted)
                {
                    throw new InvalidOperationException("Cannot add events to a completed stream.");
                }

                _events.Add((item, sequence));

                var oldSignal = _newEventSignal;
                _newEventSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                oldSignal.TrySetResult();
            }
        }

        public (List<SseItem<JsonRpcMessage?>> Events, long LastSequence, Task NewEventSignal) GetEventsAfter(long sequence)
        {
            lock (_lock)
            {
                var result = new List<SseItem<JsonRpcMessage?>>();
                long lastSequence = sequence;

                foreach (var (item, seq) in _events)
                {
                    if (seq > sequence)
                    {
                        result.Add(item);
                        lastSequence = seq;
                    }
                }

                return (result, lastSequence, _newEventSignal.Task);
            }
        }

        public void Complete()
        {
            lock (_lock)
            {
                IsCompleted = true;
                _newEventSignal.TrySetResult();
            }
        }
    }

    private sealed class SimpleEventStreamWriter : ISseEventStreamWriter
    {
        private readonly SimpleSseEventStreamStore _store;
        private readonly StreamState _state;
        private bool _disposed;

        public SimpleEventStreamWriter(SimpleSseEventStreamStore store, StreamState state)
        {
            _store = store;
            _state = state;
        }

        public ValueTask SetModeAsync(SseEventStreamMode mode, CancellationToken cancellationToken = default)
        {
            _state.Mode = mode;
            return default;
        }

        public ValueTask<SseItem<JsonRpcMessage?>> WriteEventAsync(SseItem<JsonRpcMessage?> sseItem, CancellationToken cancellationToken = default)
        {
            // Skip if already has an event ID
            if (sseItem.EventId is not null)
            {
                return new ValueTask<SseItem<JsonRpcMessage?>>(sseItem);
            }

            var sequence = _state.NextSequence();
            var eventId = _store.GenerateEventId();
            var newItem = sseItem with { EventId = eventId };

            _state.AddEvent(newItem, sequence);
            _store.TrackEvent(eventId, _state, sequence, sseItem.ReconnectionInterval);

            return new ValueTask<SseItem<JsonRpcMessage?>>(newItem);
        }

        public ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return default;
            }

            _disposed = true;
            _state.Complete();
            return default;
        }
    }

    private sealed class SimpleEventStreamReader : ISseEventStreamReader
    {
        private readonly StreamState _state;
        private readonly long _startSequence;

        public SimpleEventStreamReader(StreamState state, long startSequence)
        {
            _state = state;
            _startSequence = startSequence;
        }

        public string SessionId => _state.SessionId;
        public string StreamId => _state.StreamId;

        public async IAsyncEnumerable<SseItem<JsonRpcMessage?>> ReadEventsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            long lastSeenSequence = _startSequence;

            while (true)
            {
                // Get events after the last seen sequence
                var (events, lastSequence, newEventSignal) = _state.GetEventsAfter(lastSeenSequence);

                foreach (var evt in events)
                {
                    yield return evt;
                }

                // Update to the sequence we actually retrieved
                lastSeenSequence = lastSequence;

                // If in polling mode, stop after returning currently available events
                if (_state.Mode == SseEventStreamMode.Polling)
                {
                    yield break;
                }

                // If the stream is completed, stop
                if (_state.IsCompleted)
                {
                    yield break;
                }

                // Wait for new events or cancellation
                await newEventSignal.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
