namespace DotnetGitmoji.Services;

public sealed class CommitMessageService : ICommitMessageService
{
    public async Task<CommitMessageContent> ReadMessageAsync(string commitMsgFilePath)
    {
        var fullPath = ValidatePath(commitMsgFilePath);
        var lines = await File.ReadAllLinesAsync(fullPath);

        var subject = lines.FirstOrDefault(l => !l.StartsWith('#')) ?? string.Empty;

        var subjectIndex = Array.FindIndex(lines, l => !l.StartsWith('#'));
        var body = ParseBody(lines, subjectIndex);

        return new CommitMessageContent(subject, body);
    }

    public async Task WriteMessageAsync(string commitMsgFilePath, string newMessage)
    {
        var fullPath = ValidatePath(commitMsgFilePath);
        var lines = await File.ReadAllLinesAsync(fullPath);
        if (lines.Length == 0)
        {
            await File.WriteAllTextAsync(fullPath, newMessage);
            return;
        }

        lines[0] = newMessage;
        await File.WriteAllTextAsync(fullPath, string.Join(Environment.NewLine, lines));
    }

    public async Task WriteMessageAsync(string commitMsgFilePath, string subject, string? body)
    {
        var fullPath = ValidatePath(commitMsgFilePath);
        var lines = await File.ReadAllLinesAsync(fullPath);

        var commentLines = lines.Where(l => l.StartsWith('#')).ToArray();
        var parts = new List<string> { subject };

        if (!string.IsNullOrWhiteSpace(body))
        {
            parts.Add(string.Empty);
            parts.Add(body);
        }

        parts.AddRange(commentLines);
        await File.WriteAllTextAsync(fullPath, string.Join(Environment.NewLine, parts));
    }

    private static string ValidatePath(string commitMsgFilePath)
    {
        var fullPath = Path.GetFullPath(commitMsgFilePath);
        if (!fullPath.Contains(Path.DirectorySeparatorChar + ".git" + Path.DirectorySeparatorChar))
            throw new ArgumentException("Commit message file must be within .git directory.");
        return fullPath;
    }

    private static string? ParseBody(string[] lines, int subjectIndex)
    {
        if (subjectIndex < 0)
            return null;

        var bodyLines = new List<string>();
        var pastBlankLine = false;

        for (var i = subjectIndex + 1; i < lines.Length; i++)
        {
            if (lines[i].StartsWith('#'))
                continue;

            if (!pastBlankLine)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                    pastBlankLine = true;
                continue;
            }

            bodyLines.Add(lines[i]);
        }

        var body = string.Join(Environment.NewLine, bodyLines).Trim();
        return body.Length > 0 ? body : null;
    }
}