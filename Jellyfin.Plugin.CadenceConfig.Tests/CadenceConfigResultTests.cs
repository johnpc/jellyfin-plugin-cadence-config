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
    }
}
