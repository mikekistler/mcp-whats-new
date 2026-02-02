var builder = WebApplication.CreateBuilder(args);

// Add the MCP services: the transport to use (http) and the tools to register.
builder.Services
    .AddMcpServer()
    .WithHttpTransport(
        options => options.EventStreamStore = new SimpleSseEventStreamStore()
    )
    .WithTools<RandomNumberTools>();

var app = builder.Build();
app.MapMcp();

app.Run();
