using System.Collections.Concurrent;
using ModelContextProtocol.Protocol;

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
    public bool RequestSamplingApproval(IList<SamplingMessage> messages)
    {
        // Create a cache key based on message content
        var cacheKey = GenerateSamplingCacheKey(messages);

        if (_samplingApprovals.TryGetValue(cacheKey, out var cachedApproval))
        {
            Console.WriteLine($"[Cached] Using cached sampling approval: {(cachedApproval ? "approved" : "rejected")}");
            return cachedApproval;
        }

        // Display the sampling request
        Console.WriteLine($"  Messages: {messages.Count()}");
        foreach (var msg in messages)
        {
            Console.WriteLine($"  {msg.GetPreview()}");
        }
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
    public bool RequestToolApproval(string toolName, string? toolUseId, System.Text.Json.JsonElement? input)
    {
        toolName ??= "unknown";

        if (_toolApprovals.TryGetValue(toolName, out var cachedApproval))
        {
            Console.WriteLine($"[Cached] Using cached approval for tool '{toolName}': {(cachedApproval ? "approved" : "rejected")}");
            return cachedApproval;
        }

        // Display the tool call details
        Console.WriteLine();
        Console.WriteLine($"  Tool: {toolName}");
        Console.WriteLine($"  Tool Use ID: {toolUseId}");
        if (input.HasValue)
        {
            Console.WriteLine($"  Input: {input.Value}");
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
    /// Requests user approval for a tool result being passed to the LLM. Results are cached by tool name.
    /// </summary>
    public bool RequestToolResultApproval(string? toolName, string? toolUseId, IEnumerable<object>? content)
    {
        toolName ??= "unknown";

        if (_toolApprovals.TryGetValue($"result:{toolName}", out var cachedApproval))
        {
            Console.WriteLine($"[Cached] Using cached approval for tool result '{toolName}': {(cachedApproval ? "approved" : "rejected")}");
            return cachedApproval;
        }

        // Display the tool result details
        Console.WriteLine();
        Console.WriteLine($"  Tool: {toolName}");
        Console.WriteLine($"  Tool Use ID: {toolUseId}");
        if (content != null)
        {
            foreach (var item in content)
            {
                var preview = item?.ToString();
                if (preview?.Length > 60)
                    preview = preview[..60] + "...";
                Console.WriteLine($"  Content: {preview}");
            }
        }
        Console.Write($"Approve tool result '{toolName}'? (y/n/always/never): ");

        var approval = Console.ReadLine()?.Trim().ToLowerInvariant();
        var isApproved = approval == "y" || approval == "yes" || approval == "always";

        // Cache the result for this tool
        if (approval == "always" || approval == "never")
        {
            _toolApprovals[$"result:{toolName}"] = isApproved;
            Console.WriteLine($"[Cached] Tool result '{toolName}' approval cached as: {(isApproved ? "always approve" : "never approve")}");
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

    private static string GenerateSamplingCacheKey(IList<SamplingMessage> messages)
    {
        // Create a simple hash based on the first message's content
        var firstMessage = messages.FirstOrDefault();
        var textContent = firstMessage?.Content?.OfType<TextContentBlock>().FirstOrDefault()?.Text;
        if (textContent != null)
        {
            return $"sampling:{firstMessage!.Role}:{textContent.GetHashCode()}";
        }
        return $"sampling:{messages.Count()}";
    }
}
