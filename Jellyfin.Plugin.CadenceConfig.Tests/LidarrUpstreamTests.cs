using FluentAssertions;
using Jellyfin.Plugin.CadenceConfig.Lidarr;
using Xunit;

namespace Jellyfin.Plugin.CadenceConfig.Tests
{
    public class LidarrUpstreamTests
    {
        [Fact]
        public void Build_MapsSubPathToV1Api()
        {
            LidarrUpstream.Build("http://localhost:8686", "artist")
                .Should().Be("http://localhost:8686/api/v1/artist");
        }

        [Fact]
        public void Build_PreservesQueryAndTrimsSlashes()
        {
            LidarrUpstream.Build("http://localhost:8686/", "/queue?pageSize=100")
                .Should().Be("http://localhost:8686/api/v1/queue?pageSize=100");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Build_NullWhenBaseUrlMissing(string? baseUrl)
        {
            LidarrUpstream.Build(baseUrl, "artist").Should().BeNull();
        }

        [Theory]
        [InlineData("http://localhost:8686", true)]
        [InlineData("https://lidarr.example.com", true)]
        [InlineData("ftp://localhost", false)]
        [InlineData("not-a-url", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void IsValidBaseUrl_ChecksHttpScheme(string? baseUrl, bool expected)
        {
            LidarrUpstream.IsValidBaseUrl(baseUrl).Should().Be(expected);
        }

        [Theory]
        [InlineData("queue", "?pageSize=100", "queue?pageSize=100")]
        [InlineData("artist", "", "artist")]
        [InlineData("artist", null, "artist")]
        [InlineData(null, "?x=1", "?x=1")]
        public void JoinQuery_AppendsTheQueryString(string? sub, string? query, string expected)
        {
            LidarrUpstream.JoinQuery(sub, query).Should().Be(expected);
        }
    }
}
