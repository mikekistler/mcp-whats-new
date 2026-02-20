# Stream Resumability MCP Server

See [the main README](../README.md#support-for-long-running-requests-over-http-with-polling) for a description of the Stream Resumability feature.

## Running the server

You can run the server with the following command from the `StreamResumability/server` directory:

```bash
dotnet run
```

The server will start and listen for incoming requests on `http://localhost:6173`.

Then you can run the client to demonstrate the Stream Resumability feature.
