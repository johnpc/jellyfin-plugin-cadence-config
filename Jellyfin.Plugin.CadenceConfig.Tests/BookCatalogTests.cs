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

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Calls++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("[{\"uuid\":\"u1\",\"title\":\"Dune\",\"authors\":[{\"name\":\"Frank Herbert\"}]}]", System.Text.Encoding.UTF8, "application/json"),
                });
            }
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
