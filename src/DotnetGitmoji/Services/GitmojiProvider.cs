using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using DotnetGitmoji.Models;

namespace DotnetGitmoji.Services;

public sealed class GitmojiProvider : IGitmojiProvider
{
    private const string EmbeddedResourceName = "DotnetGitmoji.Resources.gitmojis.default.json";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient;
    private readonly ToolConfiguration _config;
    private readonly string _cacheDirectory;
    private readonly string _cachePath;

    public GitmojiProvider(IHttpClientFactory httpClientFactory, ToolConfiguration config)
    {
        _httpClient = httpClientFactory.CreateClient();
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _cacheDirectory = DotnetGitmojiPaths.UserDataDirectory;
        _cachePath = DotnetGitmojiPaths.GitmojiCachePath;
    }

    public async Task<IReadOnlyList<Gitmoji>> GetAllAsync()
    {
        if (File.Exists(_cachePath))
            try
            {
                var cached = await LoadFromCacheAsync();
                if (cached.Gitmojis?.Length > 0) return cached.Gitmojis;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                // Corrupted cache; fall through to next fallback.
            }

        var fetched = await TryFetchFromApiAsync();
        if (fetched?.Gitmojis?.Length > 0)
        {
            await SaveToCacheAsync(fetched);
            return fetched.Gitmojis;
        }

        return LoadEmbeddedDefault().Gitmojis;
    }

    public async Task<IReadOnlyList<Gitmoji>> ForceRefreshAsync()
    {
        var fetched = await TryFetchFromApiAsync();
        if (fetched?.Gitmojis?.Length > 0)
        {
            await SaveToCacheAsync(fetched);
            return fetched.Gitmojis;
        }

        return LoadEmbeddedDefault().Gitmojis;
    }

    public async Task<IReadOnlyList<Gitmoji>> SearchAsync(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return await GetAllAsync();

        var all = await GetAllAsync();
        var term = keyword.Trim();

        return all
            .Where(g =>
                g.Description.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                g.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                g.Code.Contains(term, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(g =>
                g.Name.Equals(term, StringComparison.OrdinalIgnoreCase) ? 3 // exact name match
                : g.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ? 2 // partial name match
                : g.Code.Contains(term, StringComparison.OrdinalIgnoreCase) ? 1 // code match
                : 0)
            .ToArray();
    }

    private async Task<GitmojiResponse> LoadFromCacheAsync()
    {
        await using var stream = File.OpenRead(_cachePath);
        var response = await JsonSerializer.DeserializeAsync<GitmojiResponse>(stream, JsonOptions);
        return response ?? new GitmojiResponse(Array.Empty<Gitmoji>());
    }

    private async Task SaveToCacheAsync(GitmojiResponse response)
    {
        Directory.CreateDirectory(_cacheDirectory);
        await using var stream = File.Create(_cachePath);
        await JsonSerializer.SerializeAsync(stream, response, JsonOptions);
    }

    private async Task<GitmojiResponse?> TryFetchFromApiAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            return await _httpClient.GetFromJsonAsync<GitmojiResponse>(_config.GitmojisUrl, JsonOptions, cts.Token);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return null;
        }
    }

    private static GitmojiResponse LoadEmbeddedDefault()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(EmbeddedResourceName)
                           ?? throw new InvalidOperationException(
                               "Embedded gitmoji list not found. " +
                               $"Available: [{string.Join(", ", assembly.GetManifestResourceNames())}]");

        return JsonSerializer.Deserialize<GitmojiResponse>(stream, JsonOptions)
               ?? throw new InvalidOperationException("Embedded gitmoji list could not be deserialized.");
    }
}