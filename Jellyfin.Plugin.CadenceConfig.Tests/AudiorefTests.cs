using FluentAssertions;
using Jellyfin.Plugin.CadenceConfig.Storyteller;
using Xunit;

namespace Jellyfin.Plugin.CadenceConfig.Tests
{
    public class AudiorefTests
    {
        [Fact]
        public void Parse_ReadsPathAndClip()
        {
            var clip = Audioref.Parse("../audio/00001-00003.mp4#t=12.34,15.67");
            clip.Should().BeEquivalentTo(new AudioClip("../audio/00001-00003.mp4", 12.34, 15.67));
        }

        [Theory]
        [InlineData("t=5", 5, null)]
        [InlineData("t=,9", 0, 9d)]
        [InlineData("t=0:01:05.5,0:01:07", 65.5, 67d)]
        public void ParseTimeFragment_HandlesOpenEndsAndNpt(string fragment, double start, double? end)
        {
            Audioref.ParseTimeFragment(fragment).Should().Be((start, end));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("audio.mp4")]
        [InlineData("audio.mp4#id=x")]
        [InlineData("audio.mp4#t=abc")]
        [InlineData("audio.mp4#t=1,abc")]
        public void Parse_RejectsMalformed(string? audioref)
        {
            Audioref.Parse(audioref).Should().BeNull();
        }
    }
}
