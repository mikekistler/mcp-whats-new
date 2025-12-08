# mcp-whats-new

New features in MCP C# SDK for the [2025-11-25 version of the MCP Specification]:

- Enhance authorization server discovery with support for OpenID Connect Discovery 1.0.
    - Spec change: [PR #797](https://github.com/modelcontextprotocol/modelcontextprotocol/pull/797)
    - SDK change: [PR #377](https://github.com/modelcontextprotocol/csharp-sdk/pull/377)
- Icons for tools, resources, resource templates, and prompts
    - Spec change: [SEP-973](https://modelcontextprotocol/modelcontextprotocol/issues/973)
    - SDK change: [PR #802](https://github.com/modelcontextprotocol/csharp-sdk/pull/802)
- Incremental scope consent via WWW-Authenticate
    - Spec change: [SEP-835](https://modelcontextprotocol/modelcontextprotocol/issues/835)
    - SDK change: In progress
- Added support for URL mode elicitation
    - Spec change: [SEP-1036](https://modelcontextprotocol/modelcontextprotocol/issues/1036)
    - SDK change: [PR #1021](https://github.com/modelcontextprotocol/csharp-sdk/pull/1021)
- Add tool calling support to sampling
    - Spec channge: [SEP-1577](https://modelcontextprotocol/modelcontextprotocol/issues/1577)
    - SDK change: [PR #976](https://github.com/modelcontextprotocol/csharp-sdk/pull/976)
- Add support for OAuth Client ID Metadata Documents
    - Spec change: [SEP-991](https://modelcontextprotocol/modelcontextprotocol/issues/991)
    - SDK change: [PR #1023](https://github.com/modelcontextprotocol/csharp-sdk/pull/1023)
- Support for long-running requests over HTTP with polling
    - Spec change: [SEP-1699](https://modelcontextprotocol/modelcontextprotocol/issues/1699)
    - SDK change: In progress
- An experimental tasks feature for durable requests with polling and deferred result retrieval
    - Spec change: [SEP-1686](https://modelcontextprotocol/modelcontextprotocol/issues/1686)
    - SDK change: In progress

See the [Changelog] for the full list of changes.

[2025-11-25 version of the MCP Specification]: https://modelcontextprotocol.io/specification/2025-11-25
[Changelog]: https://modelcontextprotocol.io/specification/2025-11-25/changelog