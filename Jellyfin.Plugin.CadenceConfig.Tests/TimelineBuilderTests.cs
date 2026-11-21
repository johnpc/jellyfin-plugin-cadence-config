using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Jellyfin.Plugin.CadenceConfig.Storyteller;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Jellyfin.Plugin.CadenceConfig.Tests
{
    public class TimelineBuilderTests
    {
        private sealed class Handler : HttpMessageHandler
        {
            public int Calls { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Calls++;
                var url = request.RequestUri!.ToString();
                if (url.EndsWith("/read/manifest.json", System.StringComparison.Ordinal))
                {
                    return Task.FromResult(Json(JsonSerializer.Serialize(TimelineFixture.Manifest())));
                }

                if (url.Contains("guided-navigation.json?ref=", System.StringComparison.Ordinal))
                {
                    var href = System.Uri.UnescapeDataString(url.Split("ref=")[1]);
                    return Task.FromResult(TimelineFixture.Guides().TryGetValue(href, out var guide)
                        ? Json(JsonSerializer.Serialize(guide))
                        : new HttpResponseMessage(HttpStatusCode.NotFound));
                }

                // Audio chunks: every range read fails → the builder falls back to last clip end.
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.RequestedRangeNotSatisfiable));
            }

            private static HttpResponseMessage Json(string body) =>
                new(HttpStatusCode.OK) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
        }

        [Fact]
        public async Task GetAsync_BuildsFromManifestAndGuides_ThenServesFromCache()
        {
            var handler = new Handler();
            var factory = Substitute.For<IHttpClientFactory>();
            factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(handler, false));
            var client = new StorytellerClient(factory, NullLogger<StorytellerClient>.Instance);
            var dir = Path.Combine(Path.GetTempPath(), "cadence-tb-" + System.Guid.NewGuid().ToString("N"));
            var builder = new TimelineBuilder(client, new TimelineCache(dir), NullLogger<TimelineBuilder>.Instance);
            var book = new StorytellerBook { Uuid = "u1", AlignedAt = "a", UpdatedAt = "b" };

            var timeline = await builder.GetAsync("http://st", "tok", book, CancellationToken.None);

            timeline!.BookUuid.Should().Be("u1");
            timeline.Fingerprint.Should().Be("a|b");
            timeline.Clips.Should().HaveCount(7);
            timeline.AudioOffsets.Should().Equal(0, 20);
            var calls = handler.Calls;

            (await builder.GetAsync("http://st", "tok", book, CancellationToken.None)).Should().BeSameAs(timeline);
            handler.Calls.Should().Be(calls, "the second call is a cache hit");
            Directory.Delete(dir, true);
        }

        [Fact]
        public async Task GetAsync_NullWhenTheManifestIsUnavailable()
        {
            var factory = Substitute.For<IHttpClientFactory>();
            factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(new NotFoundHandler(), false));
            var builder = new TimelineBuilder(new StorytellerClient(factory, NullLogger<StorytellerClient>.Instance), new TimelineCache(Path.GetTempPath()), NullLogger<TimelineBuilder>.Instance);
            (await builder.GetAsync("http://st", "tok", new StorytellerBook { Uuid = "none" }, CancellationToken.None)).Should().BeNull();
        }

        [Fact]
        public void AudioFiles_AreTheDistinctResolvedChunksInNaturalOrder()
        {
            TimelineBuilder.AudioFiles(TimelineFixture.Manifest(), TimelineFixture.Guides())
                .Should().Equal("OEBPS/Audio/00001-00001.mp4", "OEBPS/Audio/00001-00002.mp4");
            TimelineBuilder.AudioFiles(TimelineFixture.Manifest(), new Dictionary<string, GuidedNavigationDocument>()).Should().BeEmpty();
        }

        private sealed class NotFoundHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
