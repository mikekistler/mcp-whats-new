
using System.ClientModel;
using Microsoft.Extensions.AI;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using OpenAI;

var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
if (string.IsNullOrEmpty(token))
{
    Console.WriteLine("Error: GITHUB_TOKEN environment variable is not set.");
    return;
}
var baseUrl = "https://models.github.ai/inference";
var modelId = "gpt-4o-mini";

// Create a chat client with function invocation.
IChatClient chatClient = new ChatClientBuilder(
        new OpenAIClient(new ApiKeyCredential(token), new OpenAIClientOptions { Endpoint = new Uri(baseUrl) })
            .GetChatClient(modelId)
            .AsIChatClient())
    .UseFunctionInvocation()
    .Build();

// Create the sampling handler and wrap it with approval logic.
var innerSamplingHandler = chatClient.CreateSamplingHandler();
var approvalManager = new ApprovalManager();

Func<CreateMessageRequestParams?, IProgress<ProgressNotificationValue>, CancellationToken, ValueTask<CreateMessageResult>> samplingHandler =
    async (request, progress, cancellationToken) =>
    {
        // === BEFORE: Ask user to approve the sampling request ===
        if (request?.Messages != null)
        {
            var chatMessages = request.Messages.Select(m => new ChatMessage(
                m.Role == Role.User ? ChatRole.User : ChatRole.Assistant,
                m.Content.OfType<TextContentBlock>().Select(c => c.Text).FirstOrDefault() ?? ""));

            if (!approvalManager.RequestSamplingApproval(chatMessages))
            {
                throw new OperationCanceledException("User rejected the sampling request.");
            }
        }

        var result = await innerSamplingHandler(request, progress, cancellationToken);

        // === AFTER: Check for tool calls and prompt for approval ===
        var toolCalls = result.Content.OfType<ToolUseContentBlock>().ToList();
        if (toolCalls.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║  SAMPLING RESPONSE CONTAINS TOOL CALLS                       ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");

            foreach (var toolCall in toolCalls)
            {
                if (!approvalManager.RequestToolApproval(toolCall.Name, toolCall.Id, toolCall.Input))
                {
                    throw new OperationCanceledException($"User rejected tool call: {toolCall.Name}");
                }
            }
        }

        Console.WriteLine($"[Sampling Complete] Response role: {result.Role}, Model: {result.Model}");

        return result;
    };

// Create the MCP client
// Configure it to connect to the SamplingWithTools MCP server.
var mcpClient = await McpClient.CreateAsync(
    new HttpClientTransport(new()
    {
        Endpoint = new Uri("http://localhost:6184"),
        Name = "SamplingWithTools MCP Server",
    }),
    clientOptions: new()
    {
        Capabilities = new ClientCapabilities
        {
            Sampling = new SamplingCapability
            {
                Tools = new SamplingToolsCapability {}
            }
        },
        Handlers = new()
        {
            SamplingHandler = samplingHandler,
        }
    });

// Retrieve the list of available tools from the server.
IList<McpClientTool> tools = await mcpClient.ListToolsAsync();

const string defaultPrompt = "Play a single game of craps. Continue until the game is won or lost. Report the results of each roll and the final outcome.";

// Conversational loop that can utilize the tools via prompts.
List<ChatMessage> messages = [];
while (true)
{
    Console.Write("Prompt (or just enter to play a game of craps): ");

    var input = Console.ReadLine();
    messages.Add(new(ChatRole.User, string.IsNullOrEmpty(input) ? defaultPrompt : input));

    List<ChatResponseUpdate> updates = [];
    await foreach (ChatResponseUpdate update in chatClient
        .GetStreamingResponseAsync(messages, new() { Tools = [.. tools] }))
    {
        Console.Write(update);
        updates.Add(update);
    }
    Console.WriteLine();

    messages.AddMessages(updates);
}
