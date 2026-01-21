using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

public class ElicitationFormModel : PageModel
{
    public string ElicitationId { get; set; } = string.Empty;

    public IActionResult OnGet(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return BadRequest("Missing elicitation ID");
        }

        // Peek at the queue to verify the ID exists but don't remove it yet
        var requestExists = ElicitationTools.PeekRequest(id);

        if (requestExists == null)
        {
            return NotFound("Elicitation request not found");
        }

        ElicitationId = id;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(
        string id,
        [FromForm] string name,
        [FromForm] string ssn,
        [FromForm] string secret)
    {
        if (string.IsNullOrEmpty(id))
        {
            return BadRequest("Missing elicitation ID");
        }

        // Find and remove the matching request
        if (!ElicitationTools.TryRemoveRequest(id, out var matchingRequest))
        {
            return NotFound("Elicitation request not found");
        }

        // Create user data dictionary from form parameters
        var userData = new Dictionary<string, string>()
        {
            { "name", name },
            { "ssn", ssn },
            { "secret", secret }
        };

        // Store the data and signal completion
        matchingRequest!.UserData = userData;
        matchingRequest!.CompletionSource.SetResult(userData);

        // Redirect to success page
        return RedirectToPage("/ElicitationSuccess");
    }
}
