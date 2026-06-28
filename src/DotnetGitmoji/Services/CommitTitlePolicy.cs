using DotnetGitmoji.Models;

namespace DotnetGitmoji.Services;

public static class CommitTitlePolicy
{
    public static string? ValidateExplicitTitle(string title, ToolConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(config);

        if (config.MaxTitleLength is null || title.Length <= config.MaxTitleLength.Value)
        {
            return null;
        }

        return $"Title exceeds maximum length of {config.MaxTitleLength.Value} characters.";
    }

    public static CommitTitlePromptResult ApplyPromptPolicy(string title, ToolConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(config);

        if (config.MaxTitleLength is null || title.Length <= config.MaxTitleLength.Value)
        {
            return new CommitTitlePromptResult(title, false, false, config.MaxTitleLength);
        }

        if (!config.TrimTitleWhenExceeded)
        {
            return new CommitTitlePromptResult(title, true, false, config.MaxTitleLength);
        }

        int maxTitleLength = config.MaxTitleLength.Value;
        int lastSpace = title.LastIndexOf(' ', maxTitleLength - 1);
        string trimmedTitle = lastSpace > 0 ? title[..lastSpace] : title[..maxTitleLength];

        return new CommitTitlePromptResult(trimmedTitle, true, true, maxTitleLength);
    }
}

public sealed record CommitTitlePromptResult(
    string Title,
    bool ExceededLimit,
    bool WasTrimmed,
    int? MaxLength);