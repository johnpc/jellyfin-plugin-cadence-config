using System;
using FluentAssertions;
using Jellyfin.Plugin.CadenceConfig.Configuration;
using Jellyfin.Plugin.CadenceConfig.Storyteller;
using Xunit;

namespace Jellyfin.Plugin.CadenceConfig.Tests
{
    public class AdminLinksTests
    {
        private static readonly Guid A = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
        private static readonly Guid B = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

        [Fact]
        public void Rows_ListEveryJellyfinUser_WithTheirLinkButNeverTheToken()
        {
            var config = new PluginConfiguration
            {
                StorytellerLinks = new[]
                {
                    AdminLinks.Mapped(A, "john", "secret-token"),
                    new StorytellerLink { JellyfinUserId = B, Username = "jf-kim", Token = "t2", Kind = LinkKind.Provisioned, Password = "pw" },
                },
            };

            var rows = AdminLinks.Rows(new[] { (A, "John"), (B, "Kim"), (Guid.NewGuid(), "Guest") }, config);

            rows.Should().HaveCount(3);
            rows[0].Should().BeEquivalentTo(new AdminUserRow { JellyfinUserId = A, JellyfinUsername = "John", Linked = true, StorytellerUsername = "john", Kind = "mapped" });
            rows[1].Kind.Should().Be("provisioned");
            rows[2].Linked.Should().BeFalse();
            rows[2].StorytellerUsername.Should().BeNull();
            rows[2].Kind.Should().BeNull();
            System.Text.Json.JsonSerializer.Serialize(rows).Should().NotContain("secret-token").And.NotContain("\"pw\"");
        }

        [Fact]
        public void Credentials_AreRevealedOnlyForAccountsThePluginCreated()
        {
            AdminLinks.Credentials(null).Should().BeNull();
            AdminLinks.Credentials(AdminLinks.Mapped(A, "john", "tok")).Should().BeNull("a mapped account's password was never ours");
            AdminLinks.Credentials(new StorytellerLink { Kind = LinkKind.Provisioned, Username = "jf-kim", Password = string.Empty }).Should().BeNull();
            var creds = AdminLinks.Credentials(new StorytellerLink { Kind = LinkKind.Provisioned, Username = "jf-kim", Password = "pw" });
            creds.Should().BeEquivalentTo(new ProvisionedCredentials { Username = "jf-kim", Password = "pw" });
        }

        [Fact]
        public void Mapped_KeepsNoPassword()
        {
            var link = AdminLinks.Mapped(A, "john", "tok");
            link.Kind.Should().Be(LinkKind.Mapped);
            link.Password.Should().BeEmpty();
            link.Token.Should().Be("tok");
            AdminLinks.Wire(LinkKind.Mapped).Should().Be("mapped");
            AdminLinks.Wire(LinkKind.Provisioned).Should().Be("provisioned");
        }
    }
}
