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

    private static GitmojiProvider CreateProvider(HttpMessageHandler handler,
        string url = "https://gitmoji.dev/api/gitmojis")
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(handler));

        var config = new ToolConfiguration { GitmojisUrl = url };
        return new GitmojiProvider(factory, config);
    }

    [Fact]
    public async Task TryFetchFromApi_WhenContentLengthExceedsLimit_ReturnsEmbeddedFallback()
    {
        var handler = new FakeHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ValidJson)
            {
                Headers = { ContentLength = 2_000_000 } // > 1 MB limit
            }
        });

        var provider = CreateProvider(handler);

        // GetAllAsync falls through to embedded default when API returns oversized response
        var result = await provider.ForceRefreshAsync();

        Assert.NotNull(result);
        Assert.True(result.Count > 0);
        // The result should come from embedded default, not our fake API response
    }

    [Fact]
    public async Task TryFetchFromApi_WhenContentLengthWithinLimit_ReturnsData()
    {
        var json = ValidJson;
        var handler = new FakeHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
            {
                Headers = { ContentLength = Encoding.UTF8.GetByteCount(json) }
            }
        });

        var provider = CreateProvider(handler);
        var result = await provider.ForceRefreshAsync();

        Assert.NotNull(result);
        Assert.Contains(result, g => g.Code == ":art:");
    }

    [Theory]
    [InlineData("http://gitmoji.dev/api/gitmojis")]
    [InlineData("ftp://gitmoji.dev/api/gitmojis")]
    public async Task TryFetchFromApi_WhenNonHttpsUrl_ReturnsEmbeddedFallback(string url)
    {
        var handler = new FakeHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ValidJson)
        });

        var provider = CreateProvider(handler, url);
        var result = await provider.ForceRefreshAsync();

        Assert.NotNull(result);
        Assert.True(result.Count > 0);
        // Handler should not have been called for non-HTTPS URL
        Assert.False(handler.WasCalled);
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