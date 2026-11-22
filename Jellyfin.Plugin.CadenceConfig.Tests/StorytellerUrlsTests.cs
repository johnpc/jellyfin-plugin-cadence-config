using FluentAssertions;
using Jellyfin.Plugin.CadenceConfig.Storyteller;
using Xunit;

namespace Jellyfin.Plugin.CadenceConfig.Tests
{
    public class StorytellerUrlsTests
    {
        [Theory]
        [InlineData("http://192.168.7.211:8001/", "http://192.168.7.211:8001")]
        [InlineData("  https://st.example.com ", "https://st.example.com")]
        [InlineData("ftp://nope", null)]
        [InlineData("not a url", null)]
        [InlineData("", null)]
        [InlineData(null, null)]
        public void Base_NormalizesOrRejects(string? input, string? expected)
        {
            StorytellerUrls.Base(input).Should().Be(expected);
        }

        [Fact]
        public void Builders_ProduceTheV2Routes()
        {
            const string b = "http://st";
            StorytellerUrls.Token(b).Should().Be("http://st/api/v2/token");
            StorytellerUrls.Books(b).Should().Be("http://st/api/v2/books");
            StorytellerUrls.Positions(b, "u1").Should().Be("http://st/api/v2/books/u1/positions");
            StorytellerUrls.Read(b, "u1", "OEBPS/Text/ch 1.xhtml").Should().Be("http://st/api/v2/books/u1/read/OEBPS/Text/ch%201.xhtml");
            StorytellerUrls.GuidedNavigation(b, "u1", "OEBPS/ch1.xhtml").Should().Be("http://st/api/v2/books/u1/read/~readium/guided-navigation.json?ref=OEBPS%2Fch1.xhtml".Replace("%2F", "/"));
        }

        [Theory]
        [InlineData("OEBPS/Text/ch1.xhtml", "../Audio/a.mp4#t=1,2", "OEBPS/Audio/a.mp4")]
        [InlineData("OEBPS/Text/ch1.xhtml", "./img/x.png", "OEBPS/Text/img/x.png")]
        [InlineData("OEBPS/Text/ch1.xhtml", "Audio/a.mp4", "Audio/a.mp4")]
        [InlineData("ch1.xhtml", "../../a.mp4", "a.mp4")]
        [InlineData("OEBPS/ch1.xhtml", "#frag", "OEBPS/ch1.xhtml")]
        [InlineData("OEBPS/ch1.xhtml", "/root/a.mp4", "root/a.mp4")]
        public void ResolveAgainst_ResolvesRelativeReferences(string chapter, string reference, string expected)
        {
            StorytellerUrls.ResolveAgainst(chapter, reference).Should().Be(expected);
        }
    }
}
