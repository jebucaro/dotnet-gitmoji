namespace DotnetGitmoji.Services;

public sealed class CommitMessageService : ICommitMessageService
{
    public async Task<CommitMessageContent> ReadMessageAsync(string commitMsgFilePath)
    {
        string fullPath = ValidatePath(commitMsgFilePath);
        string[] lines = await File.ReadAllLinesAsync(fullPath);

        string subject = lines.FirstOrDefault(l => !l.StartsWith('#')) ?? string.Empty;

        int subjectIndex = Array.FindIndex(lines, l => !l.StartsWith('#'));
        string? body = ParseBody(lines, subjectIndex);

        return new CommitMessageContent(subject, body);
    }

    public async Task WriteMessageAsync(string commitMsgFilePath, string newMessage)
    {
        string fullPath = ValidatePath(commitMsgFilePath);
        string[] lines = await File.ReadAllLinesAsync(fullPath);
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
        string fullPath = ValidatePath(commitMsgFilePath);
        string[] lines = await File.ReadAllLinesAsync(fullPath);

        string[] commentLines = lines.Where(l => l.StartsWith('#')).ToArray();
        List<string> parts = new() { subject };

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
        string fullPath = Path.GetFullPath(commitMsgFilePath);
        if (!fullPath.Contains(Path.DirectorySeparatorChar + ".git" + Path.DirectorySeparatorChar))
        {
            throw new ArgumentException("Commit message file must be within .git directory.");
        }

        return fullPath;
    }

    private static string? ParseBody(string[] lines, int subjectIndex)
    {
        if (subjectIndex < 0)
        {
            return null;
        }

        List<string> bodyLines = new();
        bool pastBlankLine = false;

        for (int i = subjectIndex + 1; i < lines.Length; i++)
        {
            if (lines[i].StartsWith('#'))
            {
                continue;
            }

            if (!pastBlankLine)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                {
                    pastBlankLine = true;
                }

                continue;
            }

            bodyLines.Add(lines[i]);
        }

        string body = string.Join(Environment.NewLine, bodyLines).Trim();
        return body.Length > 0 ? body : null;
    }
}