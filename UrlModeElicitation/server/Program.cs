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

// Endpoint to display the elicitation form
app.MapGet("/elicitation-info", DisplayElicitationForm);

// Endpoint to receive the elicitation information
app.MapPost("/elicitation-info", HandleElicitationFormSubmission);

app.Run();

static async Task<IResult> DisplayElicitationForm(string id, HttpContext context)
{
    if (string.IsNullOrEmpty(id))
    {
        return TypedResults.BadRequest("Missing elicitation ID");
    }

    // Peek at the queue to verify the ID exists but don't remove it yet
    var requestExists = ElicitationTools.PeekRequest(id);

    if (requestExists == null)
    {
        return TypedResults.NotFound("Elicitation request not found");
    }

    // Display an HTML form to collect user input
    var formHtml = await File.ReadAllTextAsync("Pages/ElicitationForm.html");
    formHtml = formHtml.Replace("{{ELICITATION_ID}}", id);

    return TypedResults.Content(formHtml, "text/html");
}

static async Task<IResult> HandleElicitationFormSubmission(string id, HttpContext context)
{
    if (string.IsNullOrEmpty(id))
    {
        return TypedResults.BadRequest("Missing elicitation ID");
    }

    // Find and remove the matching request
    if (!ElicitationTools.TryRemoveRequest(id, out var matchingRequest))
    {
        return TypedResults.NotFound("Elicitation request not found");
    }

    // Extract the user data from the form submission
    var userData = new Dictionary<string, JsonElement>();
    var form = await context.Request.ReadFormAsync();

    foreach (var field in form)
    {
        if (field.Key != "id")
        {
            userData[field.Key] = JsonDocument.Parse($"\"{field.Value}\"").RootElement;
        }
    }

    // Store the data and signal completion
    matchingRequest.UserData = userData;
    matchingRequest.CompletionSource.SetResult(userData);

    // Return success page
    var successHtml = await File.ReadAllTextAsync("Pages/ElicitationSuccess.html");

    return TypedResults.Content(successHtml, "text/html");
}
