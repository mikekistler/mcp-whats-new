using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Register IHttpContextAccessor for accessing HTTP context in tools
builder.Services.AddHttpContextAccessor();

// Add the MCP services: the transport to use (http) and the tools to register.
builder.Services
    .AddMcpServer(options =>
    {
        options.ProtocolVersion = "2025-11-25";
    })
    .WithHttpTransport()
    .WithTools<ElicitationTools>();

var app = builder.Build();
app.MapMcp();

// Endpoint to directly receive the elicitation information
app.MapGet("/elicitation-info", HandleElicitationInfo);

app.Run();

static async Task HandleElicitationInfo(HttpContext context)
{
    // Get the elicitation ID from query string
    var elicitationId = context.Request.Query["id"].ToString();

    if (string.IsNullOrEmpty(elicitationId))
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync("Missing elicitation ID");
        return;
    }

    // Check if this is a GET (display form) or has query parameters (form submission)
    if (context.Request.Query.Count == 1) // Only "id" parameter
    {
        // Just displaying the form - peek at the queue to verify the ID exists
        // but don't remove it yet
        var requestExists = ElicitationTools.PeekRequest(elicitationId);

        if (requestExists == null)
        {
            context.Response.StatusCode = 404;
            await context.Response.WriteAsync("Elicitation request not found");
            return;
        }

        // Display an HTML form to collect user input
        var formHtml = await File.ReadAllTextAsync("Pages/ElicitationForm.html");
        formHtml = formHtml.Replace("{{ELICITATION_ID}}", elicitationId);

        context.Response.ContentType = "text/html";
        await context.Response.WriteAsync(formHtml);
    }
    else
    {
        // Form was submitted - now we need to find and remove the request
        ElicitationRequest? matchingRequest = null;
        var tempQueue = new List<ElicitationRequest>();

        while (ElicitationTools.TryDequeueRequest(out var request))
        {
            if (request!.ElicitationId == elicitationId)
            {
                matchingRequest = request;
                break;
            }
            tempQueue.Add(request);
        }

        // Re-enqueue any non-matching requests
        foreach (var req in tempQueue)
        {
            ElicitationTools.EnqueueRequest(req);
        }

        if (matchingRequest == null)
        {
            context.Response.StatusCode = 404;
            await context.Response.WriteAsync("Elicitation request not found");
            return;
        }

        // Form was submitted - extract the user data
        var userData = new Dictionary<string, JsonElement>();
        foreach (var param in context.Request.Query)
        {
            if (param.Key != "id")
            {
                userData[param.Key] = JsonDocument.Parse($"\"{param.Value}\"").RootElement;
            }
        }

        // Store the data and signal completion
        matchingRequest.UserData = userData;
        matchingRequest.CompletionSource.SetResult(userData);

        // Return success page
        var successHtml = await File.ReadAllTextAsync("Pages/ElicitationSuccess.html");

        context.Response.ContentType = "text/html";
        await context.Response.WriteAsync(successHtml);
    }
}
