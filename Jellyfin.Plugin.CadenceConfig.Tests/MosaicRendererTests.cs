using System.Collections.Generic;
using FluentAssertions;
using Jellyfin.Plugin.CadenceConfig.Covers;
using SkiaSharp;
using Xunit;

namespace Jellyfin.Plugin.CadenceConfig.Tests
{
    public class MosaicRendererTests
    {
        // A solid-colour square PNG, to stand in for an album cover.
        private static byte[] SolidPng(SKColor color, int size = 100)
        {
            using var surface = SKSurface.Create(new SKImageInfo(size, size));
            surface.Canvas.Clear(color);
            surface.Canvas.Flush();
            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }

        private static (int Width, int Height) PngSize(byte[] png)
        {
            using var bmp = SKBitmap.Decode(png);
            return (bmp.Width, bmp.Height);
        }

        [Fact]
        public void RendersASquarePng_FromFourSources()
        {
            var sources = new List<byte[]>
            {
                SolidPng(SKColors.Red),
                SolidPng(SKColors.Green),
                SolidPng(SKColors.Blue),
                SolidPng(SKColors.Yellow),
            };

            var png = MosaicRenderer.Render(sources);

            png.Should().NotBeNull();
            var (w, h) = PngSize(png!);
            w.Should().Be(MosaicRenderer.Size);
            h.Should().Be(MosaicRenderer.Size);
        }

        [Fact]
        public void RendersFromASingleSource_FullBleed()
        {
            var png = MosaicRenderer.Render(new List<byte[]> { SolidPng(SKColors.Purple) });
            png.Should().NotBeNull();
            PngSize(png!).Width.Should().Be(MosaicRenderer.Size);
        }

        [Fact]
        public void ReturnsNull_WhenNoSourceDecodes()
        {
            var png = MosaicRenderer.Render(new List<byte[]> { System.Array.Empty<byte>(), new byte[] { 1, 2, 3 } });
            png.Should().BeNull();
        }

        [Fact]
        public void ProducesAFourColourGrid_FromFourSolids()
        {
            var png = MosaicRenderer.Render(new List<byte[]>
            {
                SolidPng(SKColors.Red),
                SolidPng(SKColors.Green),
                SolidPng(SKColors.Blue),
                SolidPng(SKColors.Yellow),
            });

            using var bmp = SKBitmap.Decode(png!);
            var q = MosaicRenderer.Size / 4; // sample the centre of each quadrant
            bmp.GetPixel(q, q).Should().Be(SKColors.Red); // top-left
            bmp.GetPixel(q * 3, q).Should().Be(SKColors.Green); // top-right
            bmp.GetPixel(q, q * 3).Should().Be(SKColors.Blue); // bottom-left
            bmp.GetPixel(q * 3, q * 3).Should().Be(SKColors.Yellow); // bottom-right
        }
    }
}
