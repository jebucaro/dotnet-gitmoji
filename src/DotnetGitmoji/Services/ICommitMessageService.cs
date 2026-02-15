namespace DotnetGitmoji.Services;

public interface ICommitMessageService
{
    Task<string> ReadMessageAsync(string commitMsgFilePath);
    Task WriteMessageAsync(string commitMsgFilePath, string newMessage);
}