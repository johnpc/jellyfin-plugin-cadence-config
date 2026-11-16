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
        }
    }
}
