using Microsoft.Extensions.AI;

/// <summary>
/// Middleware that prompts for user approval before sampling and for each tool call in the response.
/// </summary>
public class SamplingApprovalMiddleware : DelegatingChatClient
{
    private readonly ApprovalManager _approvalManager;

    public SamplingApprovalMiddleware(IChatClient innerClient) : base(innerClient)
    {
        _approvalManager = new ApprovalManager();
    }

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // === BEFORE: Ask user to approve the sampling call ===
        if (!_approvalManager.RequestSamplingApproval(messages))
        {
            throw new OperationCanceledException("User rejected the sampling request.");
        }

        var result = await base.GetResponseAsync(messages, options, cancellationToken);

        // === AFTER: Check for tool calls and prompt for approval ===
        var toolCalls = result.Messages
            .SelectMany(m => m.Contents)
            .OfType<FunctionCallContent>()
            .ToList();

        if (toolCalls.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║  SAMPLING RESPONSE CONTAINS TOOL CALLS                       ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");

            foreach (var toolCall in toolCalls)
            {
                if (!_approvalManager.RequestToolApproval(toolCall))
                {
                    throw new OperationCanceledException($"User rejected tool call: {toolCall.Name}");
                }
            }
        }

        Console.WriteLine($"[Sampling Complete] Response role: {result.Messages.LastOrDefault()?.Role}, Model: {result.ModelId}");

        return result;
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // === BEFORE: Ask user to approve the sampling call ===
        if (!_approvalManager.RequestSamplingApproval(messages))
        {
            throw new OperationCanceledException("User rejected the sampling request.");
        }

        List<ChatResponseUpdate> updates = [];
        await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken))
        {
            updates.Add(update);
            yield return update;
        }

        // === AFTER: Check for tool calls and prompt for approval ===
        var toolCalls = updates
            .SelectMany(u => u.Contents)
            .OfType<FunctionCallContent>()
            .ToList();

        if (toolCalls.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║  SAMPLING RESPONSE CONTAINS TOOL CALLS                       ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");

            foreach (var toolCall in toolCalls)
            {
                if (!_approvalManager.RequestToolApproval(toolCall))
                {
                    throw new OperationCanceledException($"User rejected tool call: {toolCall.Name}");
                }
            }
        }

        Console.WriteLine($"[Sampling Complete] Streaming response finished");
    }
}
