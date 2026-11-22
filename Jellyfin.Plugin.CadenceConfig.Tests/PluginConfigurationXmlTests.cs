using System.IO;
using System.Xml;
using System.Xml.Serialization;
using FluentAssertions;
using Jellyfin.Plugin.CadenceConfig.Configuration;
using Xunit;

namespace Jellyfin.Plugin.CadenceConfig.Tests
{
    public class PluginConfigurationXmlTests
    {
        // Jellyfin loads plugin config with System.Xml.Serialization.XmlSerializer (MyXmlSerializer).
        // A saved XML from an older/newer plugin version may carry elements this build no longer
        // declares; they must be ignored, not crash loading or drop the known values.
        private const string SavedXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <PluginConfiguration xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
              <MarlinUrl>https://marlin.example.com</MarlinUrl>
              <HomeShelvesStaleMinutes>30</HomeShelvesStaleMinutes>
              <AudiobooksEnabled>true</AudiobooksEnabled>
              <LidarrUrl>http://localhost:8686</LidarrUrl>
              <SyncIntervalHours>6</SyncIntervalHours>
              <DeezerSubscriptions>
                <DeezerSubscription>
                  <RemovedField>x</RemovedField>
                </DeezerSubscription>
              </DeezerSubscriptions>
            </PluginConfiguration>
            """;

        [Fact]
        public void Deserialize_IgnoresUnknownElementsFromRemovedFeatures()
        {
            var serializer = new XmlSerializer(typeof(PluginConfiguration));
            using var reader = XmlReader.Create(new StringReader(SavedXml));

            var config = (PluginConfiguration)serializer.Deserialize(reader)!;

            config.MarlinUrl.Should().Be("https://marlin.example.com");
            config.LidarrUrl.Should().Be("http://localhost:8686");
            config.SyncIntervalHours.Should().Be(6);
            config.DeezerSubscriptions.Should().HaveCount(1);
            config.StorytellerAdminToken.Should().BeEmpty("absent in old XML → default");
            config.StorytellerLinks.Should().BeEmpty();
        }

        [Fact]
        public void StorytellerAdminAndLinkKind_RoundTripThroughXml()
        {
            var original = new PluginConfiguration
            {
                StorytellerUrl = "http://st",
                StorytellerAdminUsername = "umbrel",
                StorytellerAdminToken = "admin-tok",
                StorytellerLinks = new[]
                {
                    new Storyteller.StorytellerLink { JellyfinUserId = System.Guid.Parse("11111111-2222-3333-4444-555555555555"), Username = "jf-john", Token = "t", Kind = Storyteller.LinkKind.Provisioned, Password = "pw" },
                    new Storyteller.StorytellerLink { JellyfinUserId = System.Guid.Parse("11111111-2222-3333-4444-555555555556"), Username = "kim", Token = "t2" },
                },
            };
            var serializer = new XmlSerializer(typeof(PluginConfiguration));
            var sw = new StringWriter();
            serializer.Serialize(sw, original);
            using var reader = XmlReader.Create(new StringReader(sw.ToString()));

            var config = (PluginConfiguration)serializer.Deserialize(reader)!;

            config.StorytellerAdminUsername.Should().Be("umbrel");
            config.StorytellerAdminToken.Should().Be("admin-tok");
            config.StorytellerLinks.Should().HaveCount(2);
            config.StorytellerLinks[0].Kind.Should().Be(Storyteller.LinkKind.Provisioned);
            config.StorytellerLinks[0].Password.Should().Be("pw");
            config.StorytellerLinks[1].Kind.Should().Be(Storyteller.LinkKind.Mapped, "the default for links saved before Kind existed");
            config.StorytellerLinks[1].Password.Should().BeEmpty();
        }
    }
}
