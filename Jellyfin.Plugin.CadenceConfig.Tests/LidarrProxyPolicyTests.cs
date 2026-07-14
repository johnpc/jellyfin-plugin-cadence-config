using FluentAssertions;
using Jellyfin.Plugin.CadenceConfig.Lidarr;
using Xunit;

namespace Jellyfin.Plugin.CadenceConfig.Tests
{
    public class LidarrProxyPolicyTests
    {
        [Theory]
        [InlineData("search?term=radiohead", "GET")]
        [InlineData("qualityprofile", "GET")]
        [InlineData("metadataprofile", "GET")]
        [InlineData("rootfolder", "GET")]
        [InlineData("queue?pageSize=100", "GET")]
        [InlineData("artist", "GET")]
        [InlineData("artist", "POST")]
        [InlineData("album", "POST")]
        [InlineData("artist/123", "GET")]
        public void IsAllowed_TrueForAllowlistedResourceAndMethod(string subPath, string method)
        {
            LidarrProxyPolicy.IsAllowed(subPath, method).Should().BeTrue();
        }

        [Theory]
        [InlineData("artist", "DELETE")]
        [InlineData("artist", "PUT")]
        [InlineData("command", "POST")]
        [InlineData("config", "GET")]
        [InlineData("system/shutdown", "POST")]
        [InlineData("rootfolder", "DELETE")]
        public void IsAllowed_FalseForDisallowedResourceOrMethod(string subPath, string method)
        {
            LidarrProxyPolicy.IsAllowed(subPath, method).Should().BeFalse();
        }

        [Theory]
        [InlineData(null, "GET")]
        [InlineData("", "GET")]
        [InlineData("artist", null)]
        [InlineData("artist", "")]
        public void IsAllowed_FalseForEmptyInputs(string? subPath, string? method)
        {
            LidarrProxyPolicy.IsAllowed(subPath, method).Should().BeFalse();
        }

        [Fact]
        public void IsAllowed_IsCaseInsensitive()
        {
            LidarrProxyPolicy.IsAllowed("Artist", "get").Should().BeTrue();
            LidarrProxyPolicy.IsAllowed("ARTIST", "Post").Should().BeTrue();
        }

        [Theory]
        [InlineData("artist/123?x=1", "artist")]
        [InlineData("/artist", "artist")]
        [InlineData("queue?pageSize=100", "queue")]
        [InlineData("search#frag", "search")]
        [InlineData("", "")]
        public void FirstSegment_ExtractsTheResource(string subPath, string expected)
        {
            LidarrProxyPolicy.FirstSegment(subPath).Should().Be(expected);
        }
    }
}
