using FluentAssertions;
using Jellyfin.Plugin.CadenceConfig.Covers;
using SkiaSharp;
using Xunit;

namespace Jellyfin.Plugin.CadenceConfig.Tests
{
    public class NameCoverRendererTests
    {
        [Fact]
        public void RendersASquarePng_ForANamedPlaylist()
        {
            var png = NameCoverRenderer.Render("Yacht Rock");
            png.Should().NotBeNull();
            using var bmp = SKBitmap.Decode(png!);
            bmp.Width.Should().Be(NameCoverRenderer.Size);
            bmp.Height.Should().Be(NameCoverRenderer.Size);
        }

        [Fact]
        public void ReturnsNull_ForABlankName()
        {
            NameCoverRenderer.Render(null).Should().BeNull();
            NameCoverRenderer.Render("   ").Should().BeNull();
        }

        [Fact]
        public void IsDeterministic_SameNameSameBytes()
        {
            var a = NameCoverRenderer.Render("Chill Mix");
            var b = NameCoverRenderer.Render("Chill Mix");
            a.Should().Equal(b);
        }

        [Fact]
        public void DifferentNamesDiffer()
        {
            var a = NameCoverRenderer.Render("Chill Mix");
            var b = NameCoverRenderer.Render("Party Mix");
            a.Should().NotEqual(b);
        }

        [Fact]
        public void HandlesALongNameWithoutThrowing()
        {
            var png = NameCoverRenderer.Render("The Very Long Playlist Name That Should Wrap Across Several Lines Nicely");
            png.Should().NotBeNull();
        }
    }
}
