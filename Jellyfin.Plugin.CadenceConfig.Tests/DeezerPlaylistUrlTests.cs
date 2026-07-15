using FluentAssertions;
using Jellyfin.Plugin.CadenceConfig.Deezer;
using Xunit;

namespace Jellyfin.Plugin.CadenceConfig.Tests
{
    public class DeezerPlaylistUrlTests
    {
        [Theory]
        [InlineData("https://www.deezer.com/playlist/908622995", "908622995")]
        [InlineData("https://www.deezer.com/en/playlist/908622995?utm=x", "908622995")]
        [InlineData("https://deezer.com/us/playlist/12345", "12345")]
        [InlineData("908622995", "908622995")]
        [InlineData("  908622995  ", "908622995")]
        public void ParseId_ExtractsTheId(string input, string expected)
        {
            DeezerPlaylistUrl.ParseId(input).Should().Be(expected);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("https://www.deezer.com/album/12345")]
        [InlineData("https://open.spotify.com/playlist/abc")]
        [InlineData("not a url")]
        public void ParseId_NullForNonPlaylist(string? input)
        {
            DeezerPlaylistUrl.ParseId(input).Should().BeNull();
        }
    }
}
