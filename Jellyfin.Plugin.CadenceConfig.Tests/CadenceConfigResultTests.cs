using System.Text.Json;
using FluentAssertions;
using Jellyfin.Plugin.CadenceConfig.Config;
using Jellyfin.Plugin.CadenceConfig.Configuration;
using Xunit;

namespace Jellyfin.Plugin.CadenceConfig.Tests
{
    public class CadenceConfigResultTests
    {
        [Fact]
        public void FromConfiguration_CopiesNonSecretUrls()
        {
            var config = new PluginConfiguration
            {
                MarlinUrl = "https://marlin.example.com",
                SignupUrl = "https://signup.example.com",
                CastReceiverAppId = "A1B2C3D4",
            };

            var result = CadenceConfigResult.FromConfiguration(config);

            result.MarlinUrl.Should().Be("https://marlin.example.com");
            result.SignupUrl.Should().Be("https://signup.example.com");
            result.CastReceiverAppId.Should().Be("A1B2C3D4");
        }

        [Fact]
        public void FromConfiguration_NeverExposesTheLidarrApiKey()
        {
            var config = new PluginConfiguration
            {
                LidarrUrl = "http://localhost:8686",
                LidarrApiKey = "super-secret-key",
            };

            var result = CadenceConfigResult.FromConfiguration(config);

            // The DTO has no field for the key at all; the only Lidarr signal is the boolean.
            result.LidarrProxy.Should().BeTrue();
        }

        [Fact]
        public void FromConfiguration_LidarrProxyFalse_WhenUrlMissing()
        {
            var config = new PluginConfiguration { LidarrUrl = string.Empty, LidarrApiKey = "key" };
            CadenceConfigResult.FromConfiguration(config).LidarrProxy.Should().BeFalse();
        }

        [Fact]
        public void FromConfiguration_LidarrProxyFalse_WhenKeyMissing()
        {
            var config = new PluginConfiguration { LidarrUrl = "http://localhost:8686", LidarrApiKey = "  " };
            CadenceConfigResult.FromConfiguration(config).LidarrProxy.Should().BeFalse();
        }

        [Fact]
        public void FromConfiguration_DefaultsAreEmpty()
        {
            var result = CadenceConfigResult.FromConfiguration(new PluginConfiguration());

            result.MarlinUrl.Should().BeEmpty();
            result.SignupUrl.Should().BeEmpty();
            result.CastReceiverAppId.Should().BeEmpty();
            result.LidarrProxy.Should().BeFalse();
        }

        [Fact]
        public void FromConfiguration_DeezerImportAlwaysAvailable()
        {
            // Deezer's public API needs no config, so the endpoint exists whenever the plugin does.
            CadenceConfigResult.FromConfiguration(new PluginConfiguration()).DeezerImport.Should().BeTrue();
        }

        [Fact]
        public void FromConfiguration_ReportsRetiredHomeAndAudiobooksEndpointsOff()
        {
            // GET /Cadence/Home and GET /Cadence/Audiobooks were removed. Clients in the wild only
            // take those plugin paths on an explicit true, so both flags must read false.
            var result = CadenceConfigResult.FromConfiguration(new PluginConfiguration
            {
                LidarrUrl = "http://localhost:8686",
                LidarrApiKey = "key",
                MusicGrabberUrl = "https://musicgrabber.example.com",
                MusicGrabberApiKey = "key",
            });

            result.HomeShelves.Should().BeFalse();
            result.Audiobooks.Should().BeFalse();
        }

        [Fact]
        public void ConfigResponse_SerializesRetiredFlagsAsExplicitFalse()
        {
            // Old iOS decodes a missing key as false and the old web app treats anything but true
            // as off — emitting an explicit false satisfies both, on the real JSON wire shape.
            var json = JsonSerializer.Serialize(CadenceConfigResult.FromConfiguration(new PluginConfiguration()));

            using var doc = JsonDocument.Parse(json);
            doc.RootElement.GetProperty("HomeShelves").GetBoolean().Should().BeFalse();
            doc.RootElement.GetProperty("Audiobooks").GetBoolean().Should().BeFalse();
            doc.RootElement.GetProperty("DeezerImport").GetBoolean().Should().BeTrue();
        }

        [Fact]
        public void FromConfiguration_HandsClientsTheMusicGrabberCredentials()
        {
            // Deliberate: the grabber key is CLIENT-grade (the web app ships it in its public
            // bundle), so serving it to authenticated users is strictly tighter than the status quo
            // and spares every device hand-configuring Settings. The Lidarr key stays server-side.
            var config = new PluginConfiguration
            {
                MusicGrabberUrl = "https://musicgrabber.example.com",
                MusicGrabberApiKey = "client-grade-key",
            };

            var result = CadenceConfigResult.FromConfiguration(config);

            result.MusicGrabberUrl.Should().Be("https://musicgrabber.example.com");
            result.MusicGrabberApiKey.Should().Be("client-grade-key");
        }

        [Fact]
        public void FromConfiguration_MusicGrabberDefaultsEmpty()
        {
            var result = CadenceConfigResult.FromConfiguration(new PluginConfiguration());

            result.MusicGrabberUrl.Should().BeEmpty();
            result.MusicGrabberApiKey.Should().BeEmpty();
        }
    }
}
