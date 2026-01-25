using System.Collections.Concurrent;
using Microsoft.Extensions.AI;

/// <summary>
/// Manages user approvals for sampling requests and tool calls, with caching support.
/// </summary>
public class ApprovalManager
{
    private readonly ConcurrentDictionary<string, bool> _samplingApprovals = new();
    private readonly ConcurrentDictionary<string, bool> _toolApprovals = new();

    /// <summary>
    /// Requests user approval for a sampling call. Results are cached by a key derived from the messages.
    /// </summary>
    public bool RequestSamplingApproval(IEnumerable<ChatMessage> messages)
    {
        // Create a cache key based on message content
        var cacheKey = GenerateSamplingCacheKey(messages);

        if (_samplingApprovals.TryGetValue(cacheKey, out var cachedApproval))
        {
            Console.WriteLine($"[Cached] Using cached sampling approval: {(cachedApproval ? "approved" : "rejected")}");
            return cachedApproval;
        }

        // Display the sampling request
        Console.WriteLine();
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║  MCP SERVER REQUESTING SAMPLING                              ║");
        Console.WriteLine("╠══════════════════════════════════════════════════════════════╣");
        Console.WriteLine($"║  Messages: {messages.Count()}");
        foreach (var msg in messages)
        {
            var preview = msg.Text?.Length > 50 ? msg.Text[..50] + "..." : msg.Text;
            Console.WriteLine($"║  [{msg.Role}]: {preview}");
        }
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.Write("Approve this sampling request? (y/n/always/never): ");

        var approval = Console.ReadLine()?.Trim().ToLowerInvariant();
        var isApproved = approval == "y" || approval == "yes" || approval == "always";

        // Cache the result
        if (approval == "always" || approval == "never")
        {
            _samplingApprovals[cacheKey] = isApproved;
            Console.WriteLine($"[Cached] Sampling approval cached as: {(isApproved ? "always approve" : "never approve")}");
        }

        return isApproved;
    }

    /// <summary>
    /// Requests user approval for a tool call. Results are cached by tool name.
    /// </summary>
    public bool RequestToolApproval(FunctionCallContent toolCall)
    {
        var toolName = toolCall.Name ?? "unknown";

        if (_toolApprovals.TryGetValue(toolName, out var cachedApproval))
        {
            Console.WriteLine($"[Cached] Using cached approval for tool '{toolName}': {(cachedApproval ? "approved" : "rejected")}");
            return cachedApproval;
        }

        // Display the tool call details
        Console.WriteLine();
        Console.WriteLine($"  Tool: {toolName}");
        Console.WriteLine($"  Call ID: {toolCall.CallId}");
        if (toolCall.Arguments != null)
        {
            Console.WriteLine("  Arguments:");
            foreach (var arg in toolCall.Arguments)
            {
                Console.WriteLine($"    {arg.Key}: {arg.Value}");
            }
        }
        Console.Write($"  Approve tool call '{toolName}'? (y/n/always/never): ");

        var approval = Console.ReadLine()?.Trim().ToLowerInvariant();
        var isApproved = approval == "y" || approval == "yes" || approval == "always";

        // Cache the result for this tool
        if (approval == "always" || approval == "never")
        {
            _toolApprovals[toolName] = isApproved;
            Console.WriteLine($"[Cached] Tool '{toolName}' approval cached as: {(isApproved ? "always approve" : "never approve")}");
        }

        return isApproved;
    }

    /// <summary>
    /// Clears all cached approvals.
    /// </summary>
    public void ClearCache()
    {
        _samplingApprovals.Clear();
        _toolApprovals.Clear();
        Console.WriteLine("[Cache cleared]");
    }

    private static string GenerateSamplingCacheKey(IEnumerable<ChatMessage> messages)
    {
        // Create a simple hash based on the first message's content
        var firstMessage = messages.FirstOrDefault();
        if (firstMessage?.Text != null)
        {
            return $"sampling:{firstMessage.Role}:{firstMessage.Text.GetHashCode()}";
        }
        return $"sampling:{messages.Count()}";
    }
}
