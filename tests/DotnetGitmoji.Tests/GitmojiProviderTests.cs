using System.Net;
using System.Text;
using System.Text.Json;
using DotnetGitmoji.Models;
using DotnetGitmoji.Services;
using NSubstitute;

namespace DotnetGitmoji.Tests;

public class GitmojiProviderTests
{
    private static readonly GitmojiResponse ValidResponse = new(new[]
    {
        new Gitmoji("🎨", "entity", ":art:", "Improve structure", "art", null)
    });

    private static string ValidJson => JsonSerializer.Serialize(ValidResponse);

    private static GitmojiProvider CreateProvider(
        HttpMessageHandler handler,
        string url = "https://gitmoji.dev/api/gitmojis",
        IGitmojiFuzzyMatcher? fuzzyMatcher = null)
    {
        IHttpClientFactory? factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(handler));

        ToolConfiguration config = new() { GitmojisUrl = url };
        return new GitmojiProvider(factory, config, fuzzyMatcher ?? new GitmojiFuzzyMatcher());
    }

    [Fact]
    public async Task TryFetchFromApi_WhenContentLengthExceedsLimit_ReturnsEmbeddedFallback()
    {
        FakeHandler handler = new(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ValidJson)
            {
                Headers = { ContentLength = 2_000_000 } // > 1 MB limit
            }
        });

        GitmojiProvider provider = CreateProvider(handler);

        // GetAllAsync falls through to embedded default when API returns oversized response
        IReadOnlyList<Gitmoji> result = await provider.ForceRefreshAsync();

        Assert.NotNull(result);
        Assert.True(result.Count > 0);
        // The result should come from embedded default, not our fake API response
    }

    [Fact]
    public async Task TryFetchFromApi_WhenContentLengthWithinLimit_ReturnsData()
    {
        string json = ValidJson;
        FakeHandler handler = new(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
            {
                Headers = { ContentLength = Encoding.UTF8.GetByteCount(json) }
            }
        });

        GitmojiProvider provider = CreateProvider(handler);
        IReadOnlyList<Gitmoji> result = await provider.ForceRefreshAsync();

        Assert.NotNull(result);
        Assert.Contains(result, g => g.Code == ":art:");
    }

    [Theory]
    [InlineData("http://gitmoji.dev/api/gitmojis")]
    [InlineData("ftp://gitmoji.dev/api/gitmojis")]
    public async Task TryFetchFromApi_WhenNonHttpsUrl_ReturnsEmbeddedFallback(string url)
    {
        FakeHandler handler = new(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ValidJson)
        });

        GitmojiProvider provider = CreateProvider(handler, url);
        IReadOnlyList<Gitmoji> result = await provider.ForceRefreshAsync();

        Assert.NotNull(result);
        Assert.True(result.Count > 0);
        // Handler should not have been called for non-HTTPS URL
        Assert.False(handler.WasCalled);
    }

    [Fact]
    public async Task SearchAsync_DelegatesRankingToFuzzyMatcher()
    {
        FakeHandler handler = new(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ValidJson)
        });

        IGitmojiFuzzyMatcher? fuzzyMatcher = Substitute.For<IGitmojiFuzzyMatcher>();
        Gitmoji[] expected = new[] { new Gitmoji("🐛", "entity", ":bug:", "Fix a bug", "bug", null) };
        fuzzyMatcher.RankGitmojis(Arg.Any<IReadOnlyList<Gitmoji>>(), "bug").Returns(expected);

        GitmojiProvider provider = CreateProvider(handler, fuzzyMatcher: fuzzyMatcher);
        IReadOnlyList<Gitmoji> result = await provider.SearchAsync("bug");

        Assert.Equal(expected, result);
        fuzzyMatcher.Received(1).RankGitmojis(Arg.Any<IReadOnlyList<Gitmoji>>(), "bug");
    }

    [Fact]
    public async Task ForceRefreshAsync_WhenApiReturns500_FallsBackToEmbeddedDefault()
    {
        FakeHandler handler = new(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        GitmojiProvider provider = CreateProvider(handler);

        IReadOnlyList<Gitmoji> result = await provider.ForceRefreshAsync();

        Assert.NotNull(result);
        Assert.True(result.Count > 0);
    }

    [Fact]
    public async Task GetAllAsync_WhenValidCacheExists_ReturnsFromCacheWithoutCallingApi()
    {
        string cachePath = DotnetGitmojiPaths.GitmojiCachePath;
        bool hadCache = File.Exists(cachePath);
        byte[]? backup = hadCache
            ? await File.ReadAllBytesAsync(cachePath, TestContext.Current.CancellationToken)
            : null;

        try
        {
            Gitmoji[] cacheGitmojis = Enumerable.Range(0, 50)
                .Select(i => new Gitmoji($"🎨", "entity", $":art{i}:", $"desc{i}", $"art{i}", null))
                .ToArray();
            string cacheJson = JsonSerializer.Serialize(new GitmojiResponse(cacheGitmojis));
            Directory.CreateDirectory(DotnetGitmojiPaths.UserDataDirectory);
            await File.WriteAllTextAsync(cachePath, cacheJson, TestContext.Current.CancellationToken);

            FakeHandler handler = new(new HttpResponseMessage(HttpStatusCode.InternalServerError));
            GitmojiProvider provider = CreateProvider(handler);

            IReadOnlyList<Gitmoji> result = await provider.GetAllAsync();

            Assert.NotNull(result);
            Assert.True(result.Count >= 50);
            Assert.False(handler.WasCalled);
        }
        finally
        {
            if (backup is not null)
            {
                await File.WriteAllBytesAsync(cachePath, backup, TestContext.Current.CancellationToken);
            }
            else if (!hadCache && File.Exists(cachePath))
            {
                File.Delete(cachePath);
            }
        }
    }

    [Fact]
    public async Task GetAllAsync_WhenCacheMissingAndApiUnavailable_ReturnsEmbeddedFallback()
    {
        string cachePath = DotnetGitmojiPaths.GitmojiCachePath;
        bool hadCache = File.Exists(cachePath);
        byte[]? backup = hadCache
            ? await File.ReadAllBytesAsync(cachePath, TestContext.Current.CancellationToken)
            : null;

        if (hadCache)
        {
            File.Delete(cachePath);
        }

        try
        {
            FakeHandler handler = new(new HttpResponseMessage(HttpStatusCode.OK));
            GitmojiProvider provider = CreateProvider(handler, "http://not-https.example.com/gitmojis");

            IReadOnlyList<Gitmoji> result = await provider.GetAllAsync();

            Assert.NotNull(result);
            Assert.True(result.Count > 0);
        }
        finally
        {
            if (backup is not null)
            {
                await File.WriteAllBytesAsync(cachePath, backup, TestContext.Current.CancellationToken);
            }
        }
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;
        public bool WasCalled { get; private set; }

        public FakeHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.FromResult(_response);
        }
    }
}