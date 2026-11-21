using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Jellyfin.Plugin.CadenceConfig.Storyteller;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Jellyfin.Plugin.CadenceConfig.Tests
{
    public class BookCatalogTests
    {
        private sealed class CountingHandler : HttpMessageHandler
        {
            public int Calls { get; private set; }

            /// <summary>The alignedAt the next responses report (null = still aligning).</summary>
            public string? AlignedAt { get; set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Calls++;
                var aligned = AlignedAt == null ? "null" : $"\"{AlignedAt}\"";
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent($"[{{\"uuid\":\"u1\",\"title\":\"Dune\",\"alignedAt\":{aligned},\"authors\":[{{\"name\":\"Frank Herbert\"}}]}}]", System.Text.Encoding.UTF8, "application/json"),
                });
            }
        }

        private static BookCatalog Catalog(CountingHandler handler, System.Func<System.DateTimeOffset> now)
        {
            var factory = Substitute.For<IHttpClientFactory>();
            factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(handler, false));
            return new BookCatalog(new StorytellerClient(factory, NullLogger<StorytellerClient>.Instance), now);
        }

        [Fact]
        public async Task Match_RereadsOnTheShortTtl_WhileTheBookIsStillAligning()
        {
            var handler = new CountingHandler();
            var clock = System.DateTimeOffset.Parse("2026-11-21T22:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
            var catalog = Catalog(handler, () => clock);

            (await catalog.MatchAsync("http://st", "tok", "Dune", "Herbert", CancellationToken.None))!.AlignedAt.Should().BeNull();
            handler.Calls.Should().Be(1, "a fresh list is not re-read immediately");

            clock = clock.AddSeconds(31);
            handler.AlignedAt = "2026-11-21T22:00:20Z";
            (await catalog.MatchAsync("http://st", "tok", "Dune", "Herbert", CancellationToken.None))!.AlignedAt.Should().NotBeNull();
            handler.Calls.Should().Be(2, "an unaligned match re-reads after the short TTL, well before the 5-minute one");

            clock = clock.AddSeconds(31);
            await catalog.MatchAsync("http://st", "tok", "Dune", "Herbert", CancellationToken.None);
            handler.Calls.Should().Be(2, "once aligned, the normal TTL applies again");
        }

        [Fact]
        public async Task Books_AreFetchedOncePerToken_AndMatched()
        {
            var handler = new CountingHandler();
            var factory = Substitute.For<IHttpClientFactory>();
            factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(handler, false));
            var catalog = new BookCatalog(new StorytellerClient(factory, NullLogger<StorytellerClient>.Instance));

            (await catalog.MatchAsync("http://st", "tok", "Dune", "Herbert", CancellationToken.None))!.Uuid.Should().Be("u1");
            (await catalog.MatchAsync("http://st", "tok", "Dune", "Nobody", CancellationToken.None)).Should().BeNull();
            (await catalog.BooksAsync("http://st", "tok", CancellationToken.None)).Should().HaveCount(1);
            handler.Calls.Should().Be(1);

            catalog.Forget("tok");
            await catalog.BooksAsync("http://st", "tok", CancellationToken.None);
            handler.Calls.Should().Be(2);
            await catalog.BooksAsync("http://st", "other", CancellationToken.None);
            handler.Calls.Should().Be(3);
        }
    }
}
