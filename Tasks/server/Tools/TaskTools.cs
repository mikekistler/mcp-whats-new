using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

/// <summary>
/// MCP tools demonstrating all three task support modes:
///   Optional  — generate_report (default for async tools)
///   Required  — process_data   (always runs as a task)
///   Forbidden — get_server_time (default for sync tools)
/// </summary>
#pragma warning disable MCPEXP001 // Type is for evaluation purposes only and is subject to change or removal in future updates.
internal class TaskTools
{
    // -----------------------------------------------------------------------
    // 1. Automatic task support (Optional) — the default for async tools.
    //    When a client sends a task-augmented request, the SDK automatically
    //    wraps the result in a task. Without task augmentation it returns
    //    the result inline as usual.
    // -----------------------------------------------------------------------

    [McpServerTool]
    [Description("Generates a report for the given topic. Task support is Optional (default for async).")]
    public static async Task<string> GenerateReport(
        [Description("The topic to report on")] string topic,
        CancellationToken cancellationToken)
    {
        // Simulate work
        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        return $"Report for '{topic}': This is a comprehensive analysis of {topic} with key findings and recommendations.";
    }

    // -----------------------------------------------------------------------
    // 2. Required task support — the SDK always creates a task even if the
    //    client does not request one. Good for operations that take a long
    //    time and whose results should always be durable.
    // -----------------------------------------------------------------------

    [McpServerTool(TaskSupport = ToolTaskSupport.Required)]
    [Description("Processes a batch of data records. Task support is Required — always runs as a task.")]
    public static async Task<string> ProcessData(
        [Description("Number of records to process")] int recordCount,
        CancellationToken cancellationToken)
    {
        // Simulate a longer-running data processing operation
        await Task.Delay(TimeSpan.FromSeconds(8), cancellationToken);
        return $"Successfully processed {recordCount} records. Found 3 anomalies and generated summary statistics.";
    }

    // -----------------------------------------------------------------------
    // 3. Synchronous tool — task support is Forbidden by default.
    //    The result is returned inline immediately.
    // -----------------------------------------------------------------------

    [McpServerTool]
    [Description("Returns the current server time. Task support is Forbidden (default for sync).")]
    public static string GetServerTime()
    {
        return $"Server time: {DateTimeOffset.UtcNow:O}";
    }
}
#pragma warning restore MCPEXP001
