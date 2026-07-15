using FluentAssertions;
using Jellyfin.Plugin.CadenceConfig.Deezer;
using Xunit;

namespace Jellyfin.Plugin.CadenceConfig.Tests
{
    public class TrackKeyTests
    {
        [Fact]
        public void Normalize_StripsRemasterParenthetical()
        {
            TrackKey.Normalize("Hey Jude (Remastered 2015)").Should().Be("heyjude");
        }

        [Fact]
        public void Normalize_StripsFeatCredit()
        {
            TrackKey.Normalize("Crazy In Love (feat. Jay-Z)").Should().Be("crazyinlove");
        }

        [Fact]
        public void Normalize_RemovesAccentsCasePunctuation()
        {
            TrackKey.Normalize("  Björk — Jóga!  ").Should().Be("bjorkjoga");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Normalize_EmptyForBlank(string? input)
        {
            TrackKey.Normalize(input).Should().BeEmpty();
        }

        [Fact]
        public void Equality_MatchesAcrossCatalogNoise()
        {
            var deezer = new TrackKey("The Beatles", "Hey Jude (Remastered 2015)");
            var jellyfin = new TrackKey("the beatles", "Hey Jude");
            (deezer == jellyfin).Should().BeTrue();
            (deezer != jellyfin).Should().BeFalse();
            deezer.GetHashCode().Should().Be(jellyfin.GetHashCode());
            deezer.Equals((object)jellyfin).Should().BeTrue();
        }

        [Fact]
        public void Equality_DiffersByTitle()
        {
            var a = new TrackKey("Radiohead", "Creep");
            var b = new TrackKey("Radiohead", "Karma Police");
            (a == b).Should().BeFalse();
            a.Equals("not a key").Should().BeFalse();
        }

        [Fact]
        public void IsValid_RequiresBothParts()
        {
            new TrackKey("Radiohead", "Creep").IsValid.Should().BeTrue();
            new TrackKey("Radiohead", "").IsValid.Should().BeFalse();
            new TrackKey("", "Creep").IsValid.Should().BeFalse();
        }
    }
}
