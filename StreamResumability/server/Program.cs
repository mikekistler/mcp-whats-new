using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Create a distributed cache and event stream store so we can reference it in the session handler
var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
var eventStreamStore = new SessionTrackingEventStreamStore(cache);

// Add the MCP services: the transport to use (http) and the tools to register.
builder.Services
    .AddMcpServer()
    .WithHttpTransport(options =>
    {
        options.EventStreamStore = eventStreamStore;
        // Use RunSessionHandler to clean up streams when a session ends
        options.RunSessionHandler = async (httpContext, mcpServer, cancellationToken) =>
        {
            try
            {
                await mcpServer.RunAsync(cancellationToken);
            }
            finally
            {
                // Delete all streams associated with this session when it ends
                if (!string.IsNullOrEmpty(mcpServer.SessionId))
                {
                    await eventStreamStore.DeleteStreamsForSessionAsync(mcpServer.SessionId);
                }
            }
        };
    })
    .WithTools<RandomNumberTools>();

var app = builder.Build();
app.MapMcp();

app.Run();
