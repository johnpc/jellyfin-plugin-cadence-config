using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Jellyfin.Plugin.CadenceConfig.Deezer;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Jellyfin.Plugin.CadenceConfig.Tests
{
    public class DeezerClientTests
    {
        /// <summary>A stub handler that returns canned JSON per requested URL.</summary>
        private sealed class StubHandler : HttpMessageHandler
        {
            private readonly IReadOnlyDictionary<string, string> _byUrl;

            public StubHandler(IReadOnlyDictionary<string, string> byUrl) => _byUrl = byUrl;

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                var url = request.RequestUri!.ToString();
                if (_byUrl.TryGetValue(url, out var body))
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
                    });
                }

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }
        }

        private static DeezerClient ClientFor(IReadOnlyDictionary<string, string> byUrl)
        {
            var factory = Substitute.For<IHttpClientFactory>();
            factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(new StubHandler(byUrl)));
            return new DeezerClient(factory, NullLogger<DeezerClient>.Instance);
        }

        [Fact]
        public async Task FetchPlaylistAsync_FollowsPagingAndReturnsAllTracks()
        {
            const string page2 = "https://api.deezer.com/playlist/42/tracks?index=100";
            var byUrl = new Dictionary<string, string>
            {
                ["https://api.deezer.com/playlist/42"] =
                    $"{{\"title\":\"Road Trip\",\"nb_tracks\":3,\"tracks\":{{\"data\":[" +
                    "{\"title\":\"A\",\"artist\":{\"name\":\"X\"}}," +
                    "{\"title\":\"B\",\"artist\":{\"name\":\"Y\"}}]," +
                    $"\"next\":\"{page2}\"}}}}",
                [page2] = "{\"data\":[{\"title\":\"C\",\"artist\":{\"name\":\"Z\"}}]}",
            };

            var result = await ClientFor(byUrl).FetchPlaylistAsync(
                "https://www.deezer.com/playlist/42",
                CancellationToken.None);

            result.Should().NotBeNull();
            result!.Title.Should().Be("Road Trip");
            result.Tracks.Should().HaveCount(3); // page 1 (2) + page 2 (1)
            result.Tracks[2].Title.Should().Be("C");
        }

        [Fact]
        public async Task FetchPlaylistAsync_NullForBadUrl()
        {
            var result = await ClientFor(new Dictionary<string, string>())
                .FetchPlaylistAsync("https://open.spotify.com/playlist/x", CancellationToken.None);
            result.Should().BeNull();
        }

        [Fact]
        public async Task FetchPlaylistAsync_NullOnApiError()
        {
            var byUrl = new Dictionary<string, string>
            {
                ["https://api.deezer.com/playlist/99"] =
                    "{\"error\":{\"type\":\"DataException\",\"message\":\"no data\"}}",
            };
            var result = await ClientFor(byUrl).FetchPlaylistAsync("99", CancellationToken.None);
            result.Should().BeNull();
        }

        [Fact]
        public async Task FetchPlaylistAsync_SinglePageNoNext()
        {
            var byUrl = new Dictionary<string, string>
            {
                ["https://api.deezer.com/playlist/7"] =
                    "{\"title\":\"Solo\",\"nb_tracks\":1,\"tracks\":{\"data\":" +
                    "[{\"title\":\"Only\",\"artist\":{\"name\":\"Q\"}}]}}",
            };
            var result = await ClientFor(byUrl).FetchPlaylistAsync("7", CancellationToken.None);
            result!.Tracks.Should().ContainSingle();
            result.Title.Should().Be("Solo");
        }
    }
}
