using ModelContextProtocol;

var builder = WebApplication.CreateBuilder(args);

// Create a task store with custom parameters for managing task state.
#pragma warning disable MCPEXP001 // Type is for evaluation purposes only and is subject to change or removal in future updates.
var taskStore = new InMemoryMcpTaskStore(
    defaultTtl: TimeSpan.FromMinutes(10),     // Default task retention time
    maxTtl: TimeSpan.FromHours(1),            // Maximum allowed TTL
    pollInterval: TimeSpan.FromSeconds(2),    // Suggested client poll interval
    cleanupInterval: TimeSpan.FromMinutes(1), // Background cleanup frequency
    pageSize: 50                              // Tasks per page for listing
);
#pragma warning restore MCPEXP001

#pragma warning disable MCPEXP001
builder.Services.AddSingleton<IMcpTaskStore>(taskStore);
#pragma warning restore MCPEXP001

builder.Services
    .AddMcpServer(options =>
    {
        // Enable tasks by providing the task store
#pragma warning disable MCPEXP001
        options.TaskStore = taskStore;

        // Enable status notifications so clients are informed of task state changes
        options.SendTaskStatusNotifications = true;
#pragma warning restore MCPEXP001
    })
    .WithHttpTransport()
    .WithTools<TaskTools>();

var app = builder.Build();
app.MapMcp();

app.Run();
