using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);

// Register the event stream store as a singleton, and also add a distributed cache for it to use.
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSingleton<SessionTrackingEventStreamStore>();
builder.Services.AddSingleton<ISseEventStreamStore>(sp => sp.GetRequiredService<SessionTrackingEventStreamStore>());

// Add the MCP services: the transport to use (http) and the tools to register.
builder.Services
    .AddMcpServer()
    .WithHttpTransport(options =>
    {
        // Use RunSessionHandler to clean up streams when a session ends
#pragma warning disable MCPEXP002 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        options.RunSessionHandler = async (httpContext, mcpServer, cancellationToken) =>
        {
            // Grab the event stream store from DI to use for cleanup when the session ends
            var eventStreamStore = httpContext.RequestServices.GetRequiredService<SessionTrackingEventStreamStore>();
            try
            {
                await mcpServer.RunAsync(cancellationToken);
            }
            finally
            {
                // Delete all streams associated with this session when it ends
                if (!string.IsNullOrEmpty(mcpServer.SessionId))
                {
                    await eventStreamStore.DeleteStreamsForSessionAsync(mcpServer.SessionId, cancellationToken);
                }
            }
        };
#pragma warning restore MCPEXP002 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    })
    .WithTools<RandomNumberTools>();

var app = builder.Build();
app.MapMcp();

app.Run();
