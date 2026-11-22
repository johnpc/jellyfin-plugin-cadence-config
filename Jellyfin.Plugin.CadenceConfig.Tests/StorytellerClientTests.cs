using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Jellyfin.Plugin.CadenceConfig.Storyteller;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Jellyfin.Plugin.CadenceConfig.Tests
{
    public class StorytellerClientTests
    {
        private const string Base = "http://st";

        private sealed class Handler : HttpMessageHandler
        {
            public List<HttpRequestMessage> Requests { get; } = new();

            public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.NotFound);

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                return Task.FromResult(Respond(request));
            }
        }

        private static (StorytellerClient Client, Handler Handler) Make()
        {
            var handler = new Handler();
            var factory = Substitute.For<IHttpClientFactory>();
            factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(handler, disposeHandler: false));
            return (new StorytellerClient(factory, NullLogger<StorytellerClient>.Instance), handler);
        }

        private static HttpResponseMessage Json(string body, HttpStatusCode code = HttpStatusCode.OK) =>
            new(code) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

        [Fact]
        public async Task Login_PostsTheFormAndReadsTheToken()
        {
            var (client, handler) = Make();
            string? form = null;
            handler.Respond = r =>
            {
                form = r.Content!.ReadAsStringAsync().Result;
                return Json("{\"access_token\":\"tok\",\"token_type\":\"bearer\"}");
            };

            var result = await client.LoginAsync(Base, "john", "pw", CancellationToken.None);

            result.Should().Be(new LoginResult(LoginOutcome.Ok, "tok"));
            handler.Requests[0].RequestUri!.ToString().Should().Be("http://st/api/v2/token");
            form.Should().Be("usernameOrEmail=john&password=pw");
        }

        [Fact]
        public async Task Login_MapsUnauthorizedAndFailures()
        {
            var (client, handler) = Make();
            handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.Unauthorized);
            (await client.LoginAsync(Base, "a", "b", CancellationToken.None)).Outcome.Should().Be(LoginOutcome.BadCredentials);

            handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError);
            (await client.LoginAsync(Base, "a", "b", CancellationToken.None)).Outcome.Should().Be(LoginOutcome.Unreachable);

            handler.Respond = _ => Json("{}");
            (await client.LoginAsync(Base, "a", "b", CancellationToken.None)).Outcome.Should().Be(LoginOutcome.Unreachable);

            handler.Respond = _ => throw new HttpRequestException("down");
            (await client.LoginAsync(Base, "a", "b", CancellationToken.None)).Outcome.Should().Be(LoginOutcome.Unreachable);
        }

        [Fact]
        public async Task ListBooks_SendsBearer_AndParses()
        {
            var (client, handler) = Make();
            handler.Respond = _ => Json("[{\"uuid\":\"u1\",\"title\":\"Dune\",\"authors\":[{\"name\":\"Frank Herbert\"}],\"alignedAt\":\"2026-11-21\",\"updatedAt\":\"x\"}]");

            var books = await client.ListBooksAsync(Base, "tok", CancellationToken.None);

            books.Should().ContainSingle().Which.Authors[0].Name.Should().Be("Frank Herbert");
            books![0].Fingerprint.Should().Be("2026-11-21|x");
            handler.Requests[0].Headers.Authorization.Should().Be(new AuthenticationHeaderValue("Bearer", "tok"));
        }

        [Fact]
        public async Task GetJson_ReturnsNullOnErrorsAndBadJson()
        {
            var (client, handler) = Make();
            handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.Forbidden);
            (await client.GetManifestAsync(Base, "t", "u", CancellationToken.None)).Should().BeNull();
            handler.Respond = _ => Json("not json");
            (await client.GetGuidedNavigationAsync(Base, "t", "u", "ch.xhtml", CancellationToken.None)).Should().BeNull();
            handler.Respond = _ => throw new TaskCanceledException();
            (await client.GetPositionAsync(Base, "t", "u", CancellationToken.None)).Should().BeNull();
        }

        [Fact]
        public async Task GetPosition_ParsesLocatorAndTimestamp()
        {
            var (client, handler) = Make();
            handler.Respond = _ => Json("{\"locator\":{\"href\":\"ch.xhtml\",\"type\":\"application/xhtml+xml\",\"locations\":{\"fragments\":[\"s1\"],\"progression\":0.25}},\"timestamp\":1700000000000}");

            var position = await client.GetPositionAsync(Base, "t", "u1", CancellationToken.None);

            position!.Timestamp.Should().Be(1700000000000);
            position.Locator.Locations!.Fragments.Should().Equal("s1");
            handler.Requests[0].RequestUri!.ToString().Should().Be("http://st/api/v2/books/u1/positions");
        }

        [Fact]
        public async Task PutPosition_MapsSavedConflictFailed()
        {
            var (client, handler) = Make();
            var position = new StorytellerPosition { Locator = new ReadiumLocator { Href = "ch.xhtml", Type = "application/xhtml+xml" }, Timestamp = 5 };

            string? sent = null;
            handler.Respond = r =>
            {
                sent = r.Content!.ReadAsStringAsync().Result;
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            };
            (await client.PutPositionAsync(Base, "t", "u", position, CancellationToken.None)).Should().Be(PutPositionOutcome.Saved);
            sent.Should().Contain("\"timestamp\":5").And.Contain("\"href\":\"ch.xhtml\"");

            handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.Conflict);
            (await client.PutPositionAsync(Base, "t", "u", position, CancellationToken.None)).Should().Be(PutPositionOutcome.Conflict);

            handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.BadGateway);
            (await client.PutPositionAsync(Base, "t", "u", position, CancellationToken.None)).Should().Be(PutPositionOutcome.Failed);

            handler.Respond = _ => throw new HttpRequestException("down");
            (await client.PutPositionAsync(Base, "t", "u", position, CancellationToken.None)).Should().Be(PutPositionOutcome.Failed);
        }

        [Fact]
        public async Task ReadRange_ForwardsRange_AndSlicesAFullResponse()
        {
            var (client, handler) = Make();
            var file = new byte[100];
            for (var i = 0; i < file.Length; i++)
            {
                file[i] = (byte)i;
            }

            handler.Respond = r =>
            {
                var range = r.Headers.Range!.Ranges.First();
                var from = (int)range.From!;
                var to = (int)Math.Min(range.To!.Value, file.Length - 1);
                return new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = new ByteArrayContent(file[from..(to + 1)]) };
            };
            (await client.ReadRangeAsync(Base, "t", "u", "a/b.mp4", 10, 5, CancellationToken.None)).Should().Equal(10, 11, 12, 13, 14);
            handler.Requests[0].RequestUri!.ToString().Should().Be("http://st/api/v2/books/u/read/a/b.mp4");

            handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(file) };
            (await client.ReadRangeAsync(Base, "t", "u", "a/b.mp4", 98, 5, CancellationToken.None)).Should().Equal(98, 99);

            handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.RequestedRangeNotSatisfiable);
            (await client.ReadRangeAsync(Base, "t", "u", "a/b.mp4", 500, 5, CancellationToken.None)).Should().BeEmpty();
        }

        [Fact]
        public async Task GetResource_PassesRangeThroughUnbuffered()
        {
            var (client, handler) = Make();
            handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = new ByteArrayContent(new byte[] { 1 }) };
            using var response = await client.GetResourceAsync(Base, "t", "u", "OEBPS/x.xhtml", new RangeHeaderValue(0, 0), CancellationToken.None);
            response.StatusCode.Should().Be(HttpStatusCode.PartialContent);
            handler.Requests[0].Headers.Range!.ToString().Should().Be("bytes=0-0");
        }
    }
}
