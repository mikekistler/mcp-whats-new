# mcp-whats-new

This branch describes the changes in [Version 0.3.0 of the MCP C# SDK].

[Version 0.3.0 of the MCP C# SDK]: https://www.nuget.org/packages/ModelContextProtocol

## New Features

The v0.3.0 release of the MCP C# SDK adds support for the [2025-06-18 version of the MCP Specification].
New features include:

- [New Authentication Protocol](#new-authentication-protocol)
- [Structured Tool Output](#structured-tool-output)
- [Elicitation:: Interactive User Engagement](#elicitation-interactive-user-engagement)
- [Resource links in tool call results](#resource-links-in-tool-call-results)
- [Negotiated Protocol Version](#negotiated-protocol-version)

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

## Elicitation:: Interactive User Engagement
## Resource links in tool call results
## Negotiated Protocol Version
