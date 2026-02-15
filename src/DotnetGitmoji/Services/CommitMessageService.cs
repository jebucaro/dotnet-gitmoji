namespace DotnetGitmoji.Services;

public sealed class CommitMessageService : ICommitMessageService
{
    public async Task<string> ReadMessageAsync(string commitMsgFilePath)
    {
        var lines = await File.ReadAllLinesAsync(commitMsgFilePath);
        return lines.Length > 0 ? lines[0] : string.Empty;
    }

    public async Task WriteMessageAsync(string commitMsgFilePath, string newMessage)
    {
        var lines = await File.ReadAllLinesAsync(commitMsgFilePath);
        if (lines.Length == 0)
        {
            await File.WriteAllTextAsync(commitMsgFilePath, newMessage);
            return;
        }

        lines[0] = newMessage;
        var contents = string.Join(Environment.NewLine, lines);
        await File.WriteAllTextAsync(commitMsgFilePath, contents);
    }
}