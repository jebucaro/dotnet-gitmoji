using DotnetGitmoji.Models;

namespace DotnetGitmoji.Services;

public interface IGitmojiProvider
{
    Task<IReadOnlyList<Gitmoji>> GetAllAsync();
    Task<IReadOnlyList<Gitmoji>> SearchAsync(string keyword);
    Task<IReadOnlyList<Gitmoji>> ForceRefreshAsync();
}