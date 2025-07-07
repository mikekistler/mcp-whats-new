# mcp-whats-new

This branch describes the changes in [Version 0.3.0 of the MCP C# SDK].

[Version 0.3.0 of the MCP C# SDK]: https://www.nuget.org/packages/ModelContextProtocol

## New Features

The v0.3.0 release of the MCP C# SDK adds support for the [2025-06-18 version of the MCP Specification].
New features include:

- [New Authentication Protocol](#new-authentication-protocol)
- [Structured Tool Output](#structured-tool-output)
- [Elicitation: Interactive User Engagement](#elicitation-interactive-user-engagement)
- [Resource links in tool call results](#resource-links-in-tool-call-results)
- [Negotiated Protocol Version Header](#negotiated-protocol-version-header)

[2025-06-18 version of the MCP Specification]: https://modelcontextprotocol.io/specification/2025-06-18

## New Authentication Protocol

The 2025-06-18 MCP spec supports a new authentication protocol that separates the role of Authorization Server from Resource Server. MCP servers are now classified as OAuth Resource Servers, which allows for better integration with existing authentication systems and provides enhanced security features. It also provides for protected resource metadata to discover the corresponding Authorization server.

## Structured Tool Output

<!-- Spec PR: https://github.com/modelcontextprotocol/modelcontextprotocol/pull/371 -->
<!-- SDK PR: https://github.com/modelcontextprotocol/csharp-sdk/pull/480 -->

Adds support for strict validation of structured tool results.

This feature allows simple, lightweight support for validation of tool result data whose structure is described by a JSON schema.

Tools can now declare an outputSchema property, which is a JSON schema that describes the structure of the data they return. This allows clients to validate the data returned by the tool against the schema, ensuring that it conforms to the expected structure.

The output schema can also inform the LLM about the expected structure of the tool's output, allowing it to better understand and process the data.

[McpServerToolAttribute] has been updated to include the [UseStructuredContent] boolean property, which specifies whether the tool should report an output schema for structured content. This property defaults to false, meaning that tools will not report an output schema unless explicitly set to true.

[McpServerToolAttribute]: https://modelcontextprotocol.github.io/csharp-sdk/api/ModelContextProtocol.Server.McpServerToolAttribute.html
[UseStructuredContent]: https://modelcontextprotocol.github.io/csharp-sdk/api/ModelContextProtocol.Server.McpServerToolAttribute.html#ModelContextProtocol_Server_McpServerToolAttribute_UseStructuredContent

## Elicitation: Interactive User Engagement

<!-- SDK PR: https://github.com/modelcontextprotocol/csharp-sdk/pull/467 -->

The new **elicitation** feature enables servers to request additional information from users during interactions. This creates more dynamic and interactive AI experiences.
Servers can request multiple inputs from users at a time, allowing for more complex interactions and data collection,
but each input must be a "primitive" type (string, number, boolean, etc.) and cannot be nested or complex objects.

Servers request structured data from users with the [ElictAsync] extension method on IMcpServer.

[ElictAsync]: https://modelcontextprotocol.github.io/csharp-sdk/api/ModelContextProtocol.Server.McpServerExtensions.html#ModelContextProtocol_Server_McpServerExtensions_ElicitAsync_ModelContextProtocol_Server_IMcpServer_ModelContextProtocol_Protocol_ElicitRequestParams_System_Threading_CancellationToken_

```csharp
[McpServerTool]
public async Task<string> GuessTheNumber(
    IMcpServer server, // Get the McpServer from DI container
    CancellationToken token
)
{
    // First ask the user if they want to play
    var playSchema = new RequestSchema
    {
        Properties =
        {
            ["Answer"] = new BooleanSchema()
        }
    };

    var playResponse = await server.ElicitAsync(new ElicitRequestParams
    {
        Message = "Do you want to play a game?",
        RequestedSchema = playSchema
    }, token);

    // Check if user wants to play
    if (playResponse.Action != "accept" || playResponse.Content?["Answer"].ValueKind != JsonValueKind.True)
    {
        return "Maybe next time!";
    }
```

The MCP client in the C# SDK can be used to accept and process elicitation requests from an MCP server.
The MCP host must create the MCP client with the "Elicitation" capability, which specifies a callback to handle elicitation requests.

```csharp
McpClientOptions options = new()
{
    ClientInfo = new()
    {
        Name = "ElicitationClient",
        Version = "1.0.0"
    },
    Capabilities = new()
    {
        Elicitation = new()
        {
            ElicitationHandler = HandleElicitationAsync
         }
     }
};

await using var mcpClient = await McpClientFactory.CreateAsync(clientTransport, options);
```

The ElicitationHandler must request input from the user and return the data in a format that matches the requested schema.
This will be highly dependent on the client application and how it interacts with the user.
Below is an example of how a console application might handle elicitation requests.

```csharp
// Implement a method that matches the delegate's signature
async ValueTask<ElicitResult> HandleElicitationAsync(ElicitRequestParams? requestParams, CancellationToken token)
{
    // Bail out if the requestParams is null or if the requested schema has no properties
    if (requestParams?.RequestedSchema?.Properties == null)
    {
        return new ElicitResult();
    }

    // Process the elicitation request
    if (requestParams?.Message is not null)
    {
        Console.WriteLine(requestParams.Message);
    }

    var content = new Dictionary<string, JsonElement>();

    // Loop through requestParams.requestSchema.Properties dictionary requesting values for each property
    foreach (var property in requestParams.RequestedSchema.Properties)
    {
        if (property.Value is ElicitRequestParams.BooleanSchema booleanSchema)
        {
            Console.Write($"{booleanSchema.Description}: ");
            var clientInput = Console.ReadLine();
            bool parsedBool;
            if (bool.TryParse(clientInput, out parsedBool))
            {
                content[property.Key] = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(parsedBool));
            }
        }
        else if (property.Value is ElicitRequestParams.NumberSchema numberSchema)
        {
            Console.Write($"{numberSchema.Description}: ");
            var clientInput = Console.ReadLine();
            double parsedNumber;
            if (double.TryParse(clientInput, out parsedNumber))
            {
                content[property.Key] = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(parsedNumber));
            }
        }
        else if (property.Value is ElicitRequestParams.StringSchema stringSchema)
        {
            Console.Write($"{stringSchema.Description}: ");
            var clientInput = Console.ReadLine();
            content[property.Key] = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(clientInput));
        }
    }

    // Return the user's input
    return new ElicitResult
    {
        Action = "accept",
        Content = content
    };
}
```

## Resource links in tool call results

<!-- Spec PR: https://github.com/modelcontextprotocol/modelcontextprotocol/pull/603 -->
<!-- SDK PR: https://github.com/modelcontextprotocol/csharp-sdk/pull/467 -->

Tools can now include **resource links** in their results, allowing clients to easily access related resources.
One interesting use case is to return a link to a resource that was created by the tool.

```csharp
[McpServerTool]
public async Task<CallToolResult> MakeAResource()
{
    int id = new Random().Next(1, 101); // 1 to 100 inclusive

    var resource = ResourceGenerator.CreateResource(id);

    var result = new CallToolResult();

    result.Content.Add(new ResourceLinkBlock()
    {
        Uri = resource.Uri,
        Name = resource.Name
    });

    return result;
}
```

## Negotiated Protocol Version Header

<!-- SDK PR: https://github.com/modelcontextprotocol/csharp-sdk/pull/500 -->

MCP has supported [protocol version negotiation] since the 2024-11-05 version of the MCP Specification.
Version negotiation is done through the "initialize" message and response which is always the first message sent in a session.

[protocol version negotiation]: https://modelcontextprotocol.io/specification/2025-06-18/basic/lifecycle#version-negotiation

Starting with the 2025-06-18 version of the MCP Specification, clients using the streaming HTTP protocol must include the
"MCP-Protocol-Version" header in all messages for the session after the "initialize" message.

The MCP C# SDK includes this header automatically in all messages sent by the client after the "Initialize" message.
