namespace DotnetGitmoji.Services;

public sealed class CommitMessageService : ICommitMessageService
{
    public async Task<string> ReadMessageAsync(string commitMsgFilePath)
    {
        var fullPath = Path.GetFullPath(commitMsgFilePath);
        if (!fullPath.Contains(Path.DirectorySeparatorChar + ".git" + Path.DirectorySeparatorChar))
            throw new ArgumentException("Commit message file must be within .git directory.");

        var lines = await File.ReadAllLinesAsync(fullPath);
        var firstContentLine = lines.FirstOrDefault(l => !l.StartsWith('#'));
        return firstContentLine ?? string.Empty;
    }

    public async Task WriteMessageAsync(string commitMsgFilePath, string newMessage)
    {
        var fullPath = Path.GetFullPath(commitMsgFilePath);
        if (!fullPath.Contains(Path.DirectorySeparatorChar + ".git" + Path.DirectorySeparatorChar))
            throw new ArgumentException("Commit message file must be within .git directory.");
        var lines = await File.ReadAllLinesAsync(fullPath);
        if (lines.Length == 0)
        {
            await File.WriteAllTextAsync(fullPath, newMessage);
            return;
        }

        lines[0] = newMessage;
        var contents = string.Join(Environment.NewLine, lines);
        await File.WriteAllTextAsync(fullPath, contents);
    }
}