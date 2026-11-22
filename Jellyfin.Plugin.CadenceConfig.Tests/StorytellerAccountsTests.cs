using System;
using System.Collections.Generic;
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
    public class StorytellerAccountsTests
    {
        private sealed class Handler : HttpMessageHandler
        {
            public List<(HttpRequestMessage Request, string Body)> Calls { get; } = new();

            public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.OK);

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Calls.Add((request, request.Content == null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken)));
                return Respond(request);
            }
        }

        private static (StorytellerAccounts Accounts, Handler Handler) Make()
        {
            var handler = new Handler();
            var factory = Substitute.For<IHttpClientFactory>();
            factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(handler, disposeHandler: false));
            return (new StorytellerAccounts(factory, NullLogger<StorytellerAccounts>.Instance), handler);
        }

        private static HttpResponseMessage Json(string body, HttpStatusCode code = HttpStatusCode.OK) =>
            new(code) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

        [Fact]
        public async Task CreateInvite_PostsTheReaderPermissionSetAsTheAdmin_AndReturnsTheKey()
        {
            var (accounts, handler) = Make();
            handler.Respond = _ => Json("{\"email\":\"jf-john@cadence.invalid\",\"key\":\"abc123\"}");

            var result = await accounts.CreateInviteAsync("http://st", "admin-tok", "jf-john@cadence.invalid", CancellationToken.None);

            result.Should().Be(new AccountResult(AccountOutcome.Ok, "abc123"));
            var (request, body) = handler.Calls[0];
            request.RequestUri!.ToString().Should().Be("http://st/api/v2/invites");
            request.Headers.Authorization!.Parameter.Should().Be("admin-tok");
            using var doc = JsonDocument.Parse(body);
            doc.RootElement.GetProperty("email").GetString().Should().Be("jf-john@cadence.invalid");
            doc.RootElement.GetProperty("bookRead").GetBoolean().Should().BeTrue();
            doc.RootElement.GetProperty("bookList").GetBoolean().Should().BeTrue();
            doc.RootElement.GetProperty("bookDownload").GetBoolean().Should().BeTrue();
            doc.RootElement.TryGetProperty("userCreate", out _).Should().BeFalse("admin rights are never granted");
        }

        [Fact]
        public async Task AcceptInvite_PostsTheAccountFields_AndReturnsTheNewToken()
        {
            var (accounts, handler) = Make();
            handler.Respond = _ => Json("{\"access_token\":\"new-tok\",\"token_type\":\"bearer\"}");

            var result = await accounts.AcceptInviteAsync("http://st", "e@cadence.invalid", "abc123", "jf-john", "John", "pw", CancellationToken.None);

            result.Should().Be(new AccountResult(AccountOutcome.Ok, "new-tok"));
            var (request, body) = handler.Calls[0];
            request.RequestUri!.ToString().Should().Be("http://st/api/v2/users");
            request.Headers.Authorization.Should().BeNull("accepting an invite is unauthenticated");
            using var doc = JsonDocument.Parse(body);
            doc.RootElement.GetProperty("inviteKey").GetString().Should().Be("abc123");
            doc.RootElement.GetProperty("username").GetString().Should().Be("jf-john");
            doc.RootElement.GetProperty("name").GetString().Should().Be("John");
            doc.RootElement.GetProperty("password").GetString().Should().Be("pw");
            doc.RootElement.GetProperty("email").GetString().Should().Be("e@cadence.invalid");
        }

        [Theory]
        [InlineData(HttpStatusCode.Conflict)]
        [InlineData(HttpStatusCode.Unauthorized)]
        [InlineData(HttpStatusCode.InternalServerError)]
        public async Task ARefusal_IsRejected_NotUnreachable(HttpStatusCode code)
        {
            var (accounts, handler) = Make();
            handler.Respond = _ => new HttpResponseMessage(code);
            (await accounts.CreateInviteAsync("http://st", "t", "e", CancellationToken.None)).Outcome.Should().Be(AccountOutcome.Rejected);
            (await accounts.AcceptInviteAsync("http://st", "e", "k", "u", "n", "p", CancellationToken.None)).Outcome.Should().Be(AccountOutcome.Rejected);
        }

        [Fact]
        public async Task NetworkFailure_OrAnEmptyAnswer_IsUnreachable()
        {
            var (accounts, handler) = Make();
            handler.Respond = _ => throw new HttpRequestException("refused");
            (await accounts.CreateInviteAsync("http://st", "t", "e", CancellationToken.None)).Outcome.Should().Be(AccountOutcome.Unreachable);
            handler.Respond = _ => Json("{}");
            (await accounts.AcceptInviteAsync("http://st", "e", "k", "u", "n", "p", CancellationToken.None)).Outcome.Should().Be(AccountOutcome.Unreachable);
        }
    }
}
