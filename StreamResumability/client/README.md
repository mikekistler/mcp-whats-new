# Stream Resumability MCP Client

See [the main README](../../README.md#support-for-long-running-requests-over-http-with-polling) for a description of the Stream Resumability feature.

## Prerequisites

- The Stream Resumability server must be running. See the [server README](../server/README.md) for instructions.

## Running the client

You can run the client with the following command from the `StreamResumability/client` directory:

```bash
dotnet run [delay] [retryInterval]
```

### Arguments

| Argument | Description | Default |
|---|---|---|
| `delay` | Delay in seconds for the server to simulate a long-running operation | `0` |
| `retryInterval` | Retry interval in seconds for client polling | `0` |

You can also set the `ENDPOINT` environment variable to override the default server URL (`http://localhost:6173`).

## Demonstrating Stream Resumability

### Basic call (no delay)

```bash
dotnet run
```

This connects to the server, lists available tools, and calls `get_random_number` with no delay.
The call completes immediately.

### Long-running request without polling

```bash
dotnet run 10
```

This sets a 10-second delay on the server but no retry interval, so the client holds the HTTP
connection open for the full duration. The call takes approximately 10 seconds.

### Long-running request with polling (stream resumability)

```bash
dotnet run 10 2
```

This sets a 10-second delay and a 2-second retry interval. The server will:

1. Open an SSE stream and send an initial event with an Event ID and a Retry After field.
2. Disconnect the SSE stream.
3. The client automatically reconnects after the retry interval using the Event ID to resume the stream.
4. This polling cycle repeats until the server sends the final response.

The call still takes approximately 10 seconds, but the HTTP connection is not held open for the
entire duration. This is the key benefit of stream resumability — it allows long-running operations
to survive network interruptions and timeouts.
