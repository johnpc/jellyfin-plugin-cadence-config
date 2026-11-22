using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Jellyfin.Plugin.CadenceConfig.Configuration;
using Jellyfin.Plugin.CadenceConfig.Storyteller;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Jellyfin.Plugin.CadenceConfig.Tests
{
    public class UserProvisionerTests
    {
        private static readonly Guid User = Guid.Parse("11111111-2222-3333-4444-555555555555");

        private sealed class MemoryLinks : ILinkWriter
        {
            public PluginConfiguration Config { get; } = new() { StorytellerUrl = "http://st", StorytellerAdminToken = "admin-tok" };

            public int Saves { get; private set; }

            public PluginConfiguration Current() => Config;

            public void Upsert(StorytellerLink link)
            {
                Config.StorytellerLinks = LinkStore.Upsert(Config.StorytellerLinks, link);
                Saves++;
            }

            public void Remove(Guid jellyfinUserId)
            {
                Config.StorytellerLinks = LinkStore.Remove(Config.StorytellerLinks, jellyfinUserId);
                Saves++;
            }

            public void Save() => Saves++;
        }

        private sealed class Handler : HttpMessageHandler
        {
            public List<string> Paths { get; } = new();

            public Queue<HttpResponseMessage> Responses { get; } = new();

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Paths.Add(request.RequestUri!.AbsolutePath);
                return Task.FromResult(Responses.Count > 0 ? Responses.Dequeue() : new HttpResponseMessage(HttpStatusCode.InternalServerError));
            }
        }

        private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

        private static (UserProvisioner Provisioner, Handler Handler, MemoryLinks Links) Make()
        {
            var handler = new Handler();
            var factory = Substitute.For<IHttpClientFactory>();
            factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(handler, disposeHandler: false));
            var links = new MemoryLinks();
            var provisioner = new UserProvisioner(new StorytellerAccounts(factory, NullLogger<StorytellerAccounts>.Instance), links, () => "generated-pw", NullLogger<UserProvisioner>.Instance);
            return (provisioner, handler, links);
        }

        [Fact]
        public async Task EnsureLink_CreatesInviteThenAccount_AndPersistsAProvisionedLink()
        {
            var (provisioner, handler, links) = Make();
            handler.Responses.Enqueue(Json("{\"email\":\"jf-john@cadence.invalid\",\"key\":\"k1\"}"));
            handler.Responses.Enqueue(Json("{\"access_token\":\"tok-john\"}"));

            var link = await provisioner.EnsureLinkAsync(User, "John", CancellationToken.None);

            link.Should().NotBeNull();
            link!.Username.Should().Be("jf-john");
            link.Password.Should().Be("generated-pw");
            link.Token.Should().Be("tok-john");
            link.Kind.Should().Be(LinkKind.Provisioned);
            link.JellyfinUserId.Should().Be(User);
            handler.Paths.Should().Equal("/api/v2/invites", "/api/v2/users");
            links.Saves.Should().Be(1);
            LinkStore.Find(links.Config, User).Should().BeSameAs(link);
        }

        [Fact]
        public async Task EnsureLink_ReturnsTheExistingLink_WithoutCallingStoryteller()
        {
            var (provisioner, handler, links) = Make();
            var existing = AdminLinks.Mapped(User, "john", "tok");
            links.Upsert(existing);

            (await provisioner.EnsureLinkAsync(User, "John", CancellationToken.None)).Should().BeSameAs(existing);
            handler.Paths.Should().BeEmpty();
        }

        [Fact]
        public async Task EnsureLink_WithoutAnAdminLogin_DoesNothing()
        {
            var (provisioner, handler, links) = Make();
            links.Config.StorytellerAdminToken = string.Empty;

            (await provisioner.EnsureLinkAsync(User, "John", CancellationToken.None)).Should().BeNull();
            handler.Paths.Should().BeEmpty();
            links.Saves.Should().Be(0);
        }

        [Fact]
        public async Task EnsureLink_OnANameCollision_TriesTheNextSuffix()
        {
            var (provisioner, handler, _) = Make();
            handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.Conflict)); // invite for jf-john refused (email taken)
            handler.Responses.Enqueue(Json("{\"key\":\"k2\"}"));
            handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.InternalServerError)); // jf-john-2 username taken
            handler.Responses.Enqueue(Json("{\"key\":\"k3\"}"));
            handler.Responses.Enqueue(Json("{\"access_token\":\"tok3\"}"));

            var link = await provisioner.EnsureLinkAsync(User, "john", CancellationToken.None);

            link!.Username.Should().Be("jf-john-3");
            link.Token.Should().Be("tok3");
            handler.Paths.Should().HaveCount(5);
        }

        [Fact]
        public async Task EnsureLink_WhenStorytellerIsUnreachable_GivesUpWithoutRetrying()
        {
            var (provisioner, handler, links) = Make();
            var factory = Substitute.For<IHttpClientFactory>();
            factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(new ThrowingHandler(), disposeHandler: false));
            var unreachable = new UserProvisioner(new StorytellerAccounts(factory, NullLogger<StorytellerAccounts>.Instance), links, () => "pw", NullLogger<UserProvisioner>.Instance);

            (await unreachable.EnsureLinkAsync(User, "john", CancellationToken.None)).Should().BeNull();
            links.Saves.Should().Be(0);
            _ = provisioner;
            _ = handler;
        }

        [Fact]
        public async Task EnsureLink_GivesUpAfterTheMaximumNumberOfNames()
        {
            var (provisioner, handler, links) = Make();
            // Every invite refused: 5 attempts, then null — no account half-made, nothing saved.
            var link = await provisioner.EnsureLinkAsync(User, "john", CancellationToken.None);
            link.Should().BeNull();
            handler.Paths.Should().HaveCount(ProvisionNames.MaxAttempts);
            links.Saves.Should().Be(0);
        }

        [Fact]
        public async Task EnsureLink_ConcurrentCallsForOneUser_CreateOneAccount()
        {
            var (provisioner, handler, links) = Make();
            handler.Responses.Enqueue(Json("{\"key\":\"k1\"}"));
            handler.Responses.Enqueue(Json("{\"access_token\":\"tok\"}"));

            var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => provisioner.EnsureLinkAsync(User, "john", CancellationToken.None)));

            results.Should().OnlyContain(l => l != null && l.Token == "tok");
            handler.Paths.Should().HaveCount(2);
            links.Saves.Should().Be(1);
        }

        [Fact]
        public void RandomPassword_Is24UrlSafeCharacters_AndNotConstant()
        {
            var a = UserProvisioner.RandomPassword();
            var b = UserProvisioner.RandomPassword();
            a.Should().HaveLength(24).And.MatchRegex("^[A-Za-z0-9]+$");
            a.Should().NotBe(b);
        }

        private sealed class ThrowingHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => throw new HttpRequestException("refused");
        }
    }
}
