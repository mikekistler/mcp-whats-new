var builder = WebApplication.CreateBuilder(args);

// Register IHttpContextAccessor for accessing HTTP context in tools
builder.Services.AddHttpContextAccessor();

// Add Razor Pages services
builder.Services.AddRazorPages();

// Add AntiForgery services
builder.Services.AddAntiforgery();

// Add the MCP services: the transport to use (http) and the tools to register.
builder.Services
    .AddMcpServer(options =>
    {
        options.ProtocolVersion = "2025-11-25";
    })
    .WithHttpTransport()
    .WithTools<ElicitationTools>();

var app = builder.Build();

app.UseAntiforgery();

// Enable Razor Pages
app.MapRazorPages();

app.MapMcp();

app.Run();
