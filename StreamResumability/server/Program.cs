var builder = WebApplication.CreateBuilder(args);

// Create the event stream store so we can reference it in the session handler
var eventStreamStore = new SimpleSseEventStreamStore();

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
                    eventStreamStore.DeleteStreamsForSession(mcpServer.SessionId);
                }
            }
        };
    })
    .WithTools<RandomNumberTools>();

var app = builder.Build();
app.MapMcp();

app.Run();
