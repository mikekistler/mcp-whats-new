using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Register the event stream store as a singleton, and also add a distributed cache for it to use.
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSingleton<SessionTrackingEventStreamStore>();

// Add the MCP services: the transport to use (http) and the tools to register.
builder.Services
    .AddMcpServer()
    .WithHttpTransport(options =>
    {
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
                    var eventStreamStore = httpContext.RequestServices.GetRequiredService<SessionTrackingEventStreamStore>();
                    await eventStreamStore.DeleteStreamsForSessionAsync(mcpServer.SessionId);
                }
            }
        };
    })
    .WithTools<RandomNumberTools>()
    .WithDistributedCacheEventStreamStore();

var app = builder.Build();
app.MapMcp();

app.Run();
