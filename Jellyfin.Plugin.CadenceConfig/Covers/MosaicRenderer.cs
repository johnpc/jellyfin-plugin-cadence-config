using System.Collections.Generic;
using SkiaSharp;

namespace Jellyfin.Plugin.CadenceConfig.Covers
{
    /// <summary>
    /// Renders a square playlist cover from 1–4 source album images using SkiaSharp (the same imaging
    /// library the Jellyfin server ships). Four images tile as a 2×2 grid; fewer than four fall back
    /// to a single full-bleed image (the first). Each cell is centre-cropped to a square so mixed
    /// aspect ratios don't distort. Output is a PNG byte array. No I/O — takes decoded bytes, returns
    /// bytes — so it's isolated from the file/library layer.
    /// </summary>
    public static class MosaicRenderer
    {
        /// <summary>The rendered cover's edge length in pixels.</summary>
        public const int Size = 640;

        /// <summary>
        /// Render a cover PNG from the given source image bytes. Returns null when no source decodes
        /// (nothing to draw). One usable image → full-bleed; two or more → the first four as a 2×2 grid
        /// (a 2- or 3-image list still lays out on the grid, leaving blank cells filled by the accent).
        /// </summary>
        /// <param name="sources">Encoded source images (JPEG/PNG/WebP), in tile order.</param>
        /// <returns>PNG bytes, or null if none of the sources decode.</returns>
        public static byte[]? Render(IReadOnlyList<byte[]> sources)
        {
            var bitmaps = Decode(sources);
            if (bitmaps.Count == 0)
            {
                return null;
            }

            try
            {
                using var surface = SKSurface.Create(new SKImageInfo(Size, Size, SKColorType.Rgba8888, SKAlphaType.Premul));
                var canvas = surface.Canvas;
                canvas.Clear(new SKColor(0x18, 0x18, 0x18));

                if (bitmaps.Count == 1)
                {
                    DrawCropped(canvas, bitmaps[0], new SKRect(0, 0, Size, Size));
                }
                else
                {
                    var half = Size / 2f;
                    for (var i = 0; i < bitmaps.Count && i < 4; i++)
                    {
                        var x = (i % 2) * half;
                        var y = (i / 2) * half;
                        DrawCropped(canvas, bitmaps[i], new SKRect(x, y, x + half, y + half));
                    }
                }

                canvas.Flush();
                using var image = surface.Snapshot();
                using var data = image.Encode(SKEncodedImageFormat.Png, 90);
                return data.ToArray();
            }
            finally
            {
                foreach (var bmp in bitmaps)
                {
                    bmp.Dispose();
                }
            }
        }

        private static List<SKBitmap> Decode(IReadOnlyList<byte[]> sources)
        {
            var bitmaps = new List<SKBitmap>(sources.Count);
            foreach (var bytes in sources)
            {
                if (bytes.Length == 0)
                {
                    continue;
                }

                // Go via SKCodec.Create, which RETURNS NULL for undecodable bytes rather than
                // throwing (SKBitmap.Decode throws on a null codec) — so one corrupt/unsupported
                // source is skipped, not fatal, without a broad catch.
                using var data = SKData.CreateCopy(bytes);
                using var codec = SKCodec.Create(data);

                // SKCodec.Create's return is annotated non-null, but at RUNTIME it returns null
                // for undecodable bytes — so the null check is real, not dead code (CA1508's
                // annotation-based analysis can't see that).
#pragma warning disable CA1508
                if (codec is null)
#pragma warning restore CA1508
                {
                    continue;
                }

                var bmp = SKBitmap.Decode(codec);
                if (bmp is not null)
                {
                    bitmaps.Add(bmp);
                }
            }

            return bitmaps;
        }

        /// <summary>Draw <paramref name="bmp"/> into <paramref name="dest"/>, centre-cropped to fill it
        /// (cover, not stretch), so the source's aspect ratio is preserved.</summary>
        private static void DrawCropped(SKCanvas canvas, SKBitmap bmp, SKRect dest)
        {
            // Pick the largest square of the source that fills dest (cover crop), centred.
            var side = System.Math.Min(bmp.Width, bmp.Height);
            var srcX = (bmp.Width - side) / 2f;
            var srcY = (bmp.Height - side) / 2f;
            var src = new SKRect(srcX, srcY, srcX + side, srcY + side);
            using var paint = new SKPaint { FilterQuality = SKFilterQuality.Medium, IsAntialias = true };
            canvas.DrawBitmap(bmp, src, dest, paint);
        }
    }
}
