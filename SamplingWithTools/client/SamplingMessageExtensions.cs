using ModelContextProtocol.Protocol;

/// <summary>
/// Extension methods for SamplingMessage.
/// </summary>
public static class SamplingMessageExtensions
{
    /// <summary>
    /// Returns a concise summary of the sampling message, including tool name for tool use/result messages.
    /// </summary>
    public static string GetPreview(this SamplingMessage message, int maxLength = 60)
    {
        var role = message.Role.ToString();
        var contentPreview = GetContentPreview(message.Content, maxLength);
        return $"[{role}] {contentPreview}";
    }

    private static string GetContentPreview(IList<ContentBlock>? content, int maxLength)
    {
        if (content == null || content.Count == 0)
        {
            return "(empty)";
        }

        var previews = new List<string>();

        foreach (var block in content)
        {
            var preview = block switch
            {
                TextContentBlock textBlock => TruncateText(textBlock.Text, maxLength),
                ToolUseContentBlock toolUseBlock => $"[ToolUse: {toolUseBlock.Name}, Id: {toolUseBlock.Id}]",
                ToolResultContentBlock toolResultBlock => $"[ToolResult: {toolResultBlock.ToolUseId}]",
                ImageContentBlock imageBlock => $"[Image: {FormatSize(imageBlock.Data.Length)}]",
                AudioContentBlock audioBlock => $"[Audio: {FormatSize(audioBlock.Data.Length)}]",
                _ => $"[{block.GetType().Name}]"
            };

            if (!string.IsNullOrEmpty(preview))
            {
                previews.Add(preview);
            }
        }

        return previews.Count > 0 ? string.Join(" | ", previews) : "(unknown content)";
    }

    private static string TruncateText(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "(empty)";
        }

        // Replace newlines with spaces for preview
        text = text.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");

        if (text.Length <= maxLength)
        {
            return text;
        }

        return text[..(maxLength - 3)] + "...";
    }

    private static string FormatSize(int bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }
}
