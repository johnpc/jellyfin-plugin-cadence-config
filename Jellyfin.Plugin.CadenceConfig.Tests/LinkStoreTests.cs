using System;
using FluentAssertions;
using Jellyfin.Plugin.CadenceConfig.Configuration;
using Jellyfin.Plugin.CadenceConfig.Storyteller;
using Xunit;

namespace Jellyfin.Plugin.CadenceConfig.Tests
{
    public class LinkStoreTests
    {
        private static readonly Guid Alice = Guid.NewGuid();
        private static readonly Guid Bob = Guid.NewGuid();

        [Fact]
        public void StateFor_FollowsConfigThenLink()
        {
            var config = new PluginConfiguration();
            LinkStore.StateFor(config, Alice).Should().Be(LinkState.Unconfigured);

            config.StorytellerUrl = "http://st";
            LinkStore.StateFor(config, Alice).Should().Be(LinkState.Unlinked);

            config.StorytellerLinks = LinkStore.Upsert(null, new StorytellerLink { JellyfinUserId = Alice, Username = "alice", Token = "t" });
            LinkStore.StateFor(config, Alice).Should().Be(LinkState.Linked);
            LinkStore.StateFor(config, Bob).Should().Be(LinkState.Unlinked);
        }

        [Fact]
        public void Upsert_ReplacesTheSameUser_KeepsOthers()
        {
            var links = LinkStore.Upsert(null, new StorytellerLink { JellyfinUserId = Alice, Username = "a1", Token = "t1" });
            links = LinkStore.Upsert(links, new StorytellerLink { JellyfinUserId = Bob, Username = "b", Token = "tb" });
            links = LinkStore.Upsert(links, new StorytellerLink { JellyfinUserId = Alice, Username = "a2", Token = "t2" });

            links.Should().HaveCount(2);
            var config = new PluginConfiguration { StorytellerLinks = links };
            LinkStore.Find(config, Alice)!.Username.Should().Be("a2");
            LinkStore.Find(config, Bob)!.Token.Should().Be("tb");
        }

        [Fact]
        public void Remove_DropsOnlyThatUser_AndEmptyTokensNeverCount()
        {
            var links = new[]
            {
                new StorytellerLink { JellyfinUserId = Alice, Token = "t" },
                new StorytellerLink { JellyfinUserId = Bob, Token = string.Empty },
            };
            LinkStore.Remove(links, Alice).Should().ContainSingle(l => l.JellyfinUserId == Bob);
            LinkStore.Find(new PluginConfiguration { StorytellerLinks = links }, Bob).Should().BeNull();
            LinkStore.Find(new PluginConfiguration { StorytellerLinks = null! }, Bob).Should().BeNull();
        }
    }
}
