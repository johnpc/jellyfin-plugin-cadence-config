using FluentAssertions;
using Jellyfin.Plugin.CadenceConfig.Configuration;
using Jellyfin.Plugin.CadenceConfig.Lidarr;
using Xunit;

namespace Jellyfin.Plugin.CadenceConfig.Tests
{
    public class LidarrProxyPlanTests
    {
        private static PluginConfiguration Configured() => new PluginConfiguration
        {
            LidarrUrl = "http://localhost:8686",
            LidarrApiKey = "secret-key",
        };

        [Fact]
        public void Create_ForwardsAllowlistedRequestToMappedUpstream()
        {
            var plan = LidarrProxyPlan.Create("POST", "artist", Configured());

            plan.Forward.Should().BeTrue();
            plan.TargetUrl.Should().Be("http://localhost:8686/api/v1/artist");
            plan.ApiKey.Should().Be("secret-key");
            plan.StatusCode.Should().Be(0);
        }

        [Fact]
        public void Create_PreservesQueryStringInTarget()
        {
            var plan = LidarrProxyPlan.Create("GET", "queue?pageSize=100", Configured());
            plan.TargetUrl.Should().Be("http://localhost:8686/api/v1/queue?pageSize=100");
        }

        [Theory]
        [InlineData("DELETE", "artist")]
        [InlineData("POST", "command")]
        [InlineData("GET", "config")]
        public void Create_Refuses403WhenNotAllowlisted(string method, string sub)
        {
            var plan = LidarrProxyPlan.Create(method, sub, Configured());

            plan.Forward.Should().BeFalse();
            plan.StatusCode.Should().Be(403);
            plan.TargetUrl.Should().BeNull();
            plan.ApiKey.Should().BeNull();
        }

        [Fact]
        public void Create_Refuses503WhenLidarrUrlMissing()
        {
            var config = new PluginConfiguration { LidarrUrl = string.Empty, LidarrApiKey = "k" };
            var plan = LidarrProxyPlan.Create("GET", "artist", config);

            plan.Forward.Should().BeFalse();
            plan.StatusCode.Should().Be(503);
        }

        [Fact]
        public void Create_Refuses503WhenApiKeyMissing()
        {
            var config = new PluginConfiguration { LidarrUrl = "http://localhost:8686", LidarrApiKey = "  " };
            var plan = LidarrProxyPlan.Create("GET", "artist", config);

            plan.Forward.Should().BeFalse();
            plan.StatusCode.Should().Be(503);
        }

        [Fact]
        public void Create_Refuses503WhenLidarrUrlNotHttp()
        {
            var config = new PluginConfiguration { LidarrUrl = "ftp://nope", LidarrApiKey = "k" };
            var plan = LidarrProxyPlan.Create("GET", "artist", config);

            plan.Forward.Should().BeFalse();
            plan.StatusCode.Should().Be(503);
        }
    }
}
