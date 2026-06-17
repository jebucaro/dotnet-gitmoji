namespace DotnetGitmoji.Services;

public interface ICommitMessageService
{
    Task<CommitMessageContent> ReadMessageAsync(string commitMsgFilePath);
    Task WriteMessageAsync(string commitMsgFilePath, string newMessage);
    Task WriteMessageAsync(string commitMsgFilePath, string subject, string? body);
}