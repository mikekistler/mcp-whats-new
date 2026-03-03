using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System.Text.Json;

var endpoint = Environment.GetEnvironmentVariable("ENDPOINT") ?? "http://localhost:6174";

var clientTransport = new HttpClientTransport(new()
{
    Endpoint = new Uri(endpoint),
    TransportMode = HttpTransportMode.StreamableHttp,
});

// Set up client options with a task status notification handler.
#pragma warning disable MCPEXP001 // Type is for evaluation purposes only and is subject to change or removal in future updates.
McpClientOptions options = new()
{
    ClientInfo = new()
    {
        Name = "TasksDemoClient",
        Version = "1.0.0"
    },
    Handlers = new McpClientHandlers
    {
        TaskStatusHandler = (task, cancellationToken) =>
        {
            Console.WriteLine($"  [Notification] Task {task.TaskId} status changed to {task.Status}");
            return ValueTask.CompletedTask;
        }
    }
};

await using var client = await McpClient.CreateAsync(clientTransport, options);

var tools = await client.ListToolsAsync();
Console.WriteLine($"Connected to server with tools: {string.Join(", ", tools.Select(t => t.Name))}");
Console.WriteLine();

// -----------------------------------------------------------------------
// 1. Call a synchronous tool (no task augmentation) — returns inline result
// -----------------------------------------------------------------------

Console.WriteLine("=== 1. Synchronous tool call (no task support) ===");
var timeResult = await client.CallToolAsync(toolName: "get_server_time");
var timeText = timeResult.Content.OfType<TextContentBlock>().First();
Console.WriteLine($"Result: {timeText.Text}");
Console.WriteLine();

// -----------------------------------------------------------------------
// 2. Call an async tool WITH task augmentation — result is tracked durably
// -----------------------------------------------------------------------

Console.WriteLine("=== 2. Async tool with task augmentation (generate_report) ===");
var reportResult = await client.CallToolAsync(
    new CallToolRequestParams
    {
        Name = "generate_report",
        Arguments = new Dictionary<string, JsonElement>
        {
            ["topic"] = JsonSerializer.SerializeToElement("renewable energy")
        },
        Task = new McpTaskMetadata
        {
            TimeToLive = TimeSpan.FromMinutes(5)
        }
    });

if (reportResult.Task != null)
{
    Console.WriteLine($"Task created: {reportResult.Task.TaskId}");
    Console.WriteLine($"Initial status: {reportResult.Task.Status}");

    // Poll until the task reaches a terminal state
    Console.WriteLine("Polling for completion...");
    var completedTask = await client.PollTaskUntilCompleteAsync(reportResult.Task.TaskId);
    Console.WriteLine($"Final status: {completedTask.Status}");

    if (completedTask.Status == McpTaskStatus.Completed)
    {
        // Retrieve the durable result
        var resultJson = await client.GetTaskResultAsync(reportResult.Task.TaskId);
        var callToolResult = resultJson.Deserialize<CallToolResult>(McpJsonUtilities.DefaultOptions);
        foreach (var content in callToolResult?.Content ?? [])
        {
            if (content is TextContentBlock text)
            {
                Console.WriteLine($"Result: {text.Text}");
            }
        }
    }
}
else
{
    // Server returned result inline (no task created)
    var text = reportResult.Content.OfType<TextContentBlock>().First();
    Console.WriteLine($"Inline result: {text.Text}");
}
Console.WriteLine();

// -----------------------------------------------------------------------
// 3. Required-task tool (process_data) — always creates a task even if not
//    requested.  We call it with task augmentation and save the task ID so
//    we can retrieve the result later in step 6.
// -----------------------------------------------------------------------

Console.WriteLine("=== 3. Required-task tool (process_data) ===");
var processResult = await client.CallToolAsync(
    new CallToolRequestParams
    {
        Name = "process_data",
        Arguments = new Dictionary<string, JsonElement>
        {
            ["recordCount"] = JsonSerializer.SerializeToElement(5000)
        },
        Task = new McpTaskMetadata
        {
            TimeToLive = TimeSpan.FromMinutes(5)
        }
    });

string? processTaskId = processResult.Task?.TaskId;

if (processTaskId != null)
{
    Console.WriteLine($"Task created: {processTaskId}");
    Console.WriteLine($"Initial status: {processResult.Task!.Status}");

    // Check status manually before polling to completion
    var status = await client.GetTaskAsync(processTaskId);
    Console.WriteLine($"Polled status: {status.Status}");
}
Console.WriteLine();

// -----------------------------------------------------------------------
// 4. List all tasks for the current session
// -----------------------------------------------------------------------

Console.WriteLine("=== 4. Listing all tasks ===");
var allTasks = await client.ListTasksAsync();
foreach (var t in allTasks)
{
    Console.WriteLine($"  Task {t.TaskId}: {t.Status}");
}
Console.WriteLine();

// -----------------------------------------------------------------------
// 5. Start another task and cancel it
// -----------------------------------------------------------------------

Console.WriteLine("=== 5. Cancel a running task ===");
var cancelResult = await client.CallToolAsync(
    new CallToolRequestParams
    {
        Name = "process_data",
        Arguments = new Dictionary<string, JsonElement>
        {
            ["recordCount"] = JsonSerializer.SerializeToElement(10000)
        },
        Task = new McpTaskMetadata
        {
            TimeToLive = TimeSpan.FromMinutes(5)
        }
    });

if (cancelResult.Task != null)
{
    Console.WriteLine($"Task created: {cancelResult.Task.TaskId}");
    Console.WriteLine($"Status before cancel: {cancelResult.Task.Status}");

    var cancelledTask = await client.CancelTaskAsync(cancelResult.Task.TaskId);
    Console.WriteLine($"Status after cancel: {cancelledTask.Status}");
}
Console.WriteLine();

// -----------------------------------------------------------------------
// 6. Wait for the process_data task from step 3 to complete and retrieve result
// -----------------------------------------------------------------------

if (processTaskId != null)
{
    Console.WriteLine("=== 6. Retrieve deferred result from step 3 ===");
    var completedProcess = await client.PollTaskUntilCompleteAsync(processTaskId);
    Console.WriteLine($"Final status: {completedProcess.Status}");

    if (completedProcess.Status == McpTaskStatus.Completed)
    {
        var resultJson = await client.GetTaskResultAsync(processTaskId);
        var callToolResult = resultJson.Deserialize<CallToolResult>(McpJsonUtilities.DefaultOptions);
        foreach (var content in callToolResult?.Content ?? [])
        {
            if (content is TextContentBlock text)
            {
                Console.WriteLine($"Result: {text.Text}");
            }
        }
    }
}
Console.WriteLine();

Console.WriteLine("=== Done ===");
#pragma warning restore MCPEXP001
