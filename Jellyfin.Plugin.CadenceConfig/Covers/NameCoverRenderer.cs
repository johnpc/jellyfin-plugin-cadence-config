using SkiaSharp;

namespace Jellyfin.Plugin.CadenceConfig.Covers
{
    /// <summary>
    /// Renders a fallback playlist cover from just its NAME, for playlists with no track art to build
    /// a mosaic from (empty playlists, or ones whose tracks lack covers). A deterministic diagonal
    /// gradient — its hue derived from the name so the same playlist always gets the same colour —
    /// with the name wrapped and centred, Spotify-style. Pure: name in, PNG bytes out.
    /// </summary>
    public static class NameCoverRenderer
    {
        /// <summary>The rendered cover's edge length in pixels (matches the mosaic).</summary>
        public const int Size = MosaicRenderer.Size;

        /// <summary>
        /// Render a name-based cover PNG. Returns null only for a null/blank name (nothing to draw).
        /// </summary>
        /// <param name="name">The playlist name.</param>
        /// <returns>PNG bytes, or null when the name is blank.</returns>
        public static byte[]? Render(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            using var surface = SKSurface.Create(new SKImageInfo(Size, Size, SKColorType.Rgba8888, SKAlphaType.Premul));
            var canvas = surface.Canvas;
            PaintBackground(canvas, name);
            DrawTitle(canvas, name.Trim());

            canvas.Flush();
            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 90);
            return data.ToArray();
        }

        /// <summary>Fill with a diagonal two-stop gradient whose hue is a stable hash of the name.</summary>
        private static void PaintBackground(SKCanvas canvas, string name)
        {
            var hue = StableHue(name);
            var top = SKColor.FromHsl(hue, 55, 42);
            var bottom = SKColor.FromHsl((hue + 24) % 360, 60, 26);
            using var shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0),
                new SKPoint(Size, Size),
                new[] { top, bottom },
                null,
                SKShaderTileMode.Clamp);
            using var paint = new SKPaint { Shader = shader };
            canvas.DrawRect(new SKRect(0, 0, Size, Size), paint);
        }

        /// <summary>Draw the name centred, wrapped to at most three lines, auto-sized to fit.</summary>
        private static void DrawTitle(SKCanvas canvas, string name)
        {
            const float margin = 56f;
            var maxWidth = Size - (margin * 2);
            using var typeface = SKTypeface.FromFamilyName(
                "sans-serif",
                SKFontStyleWeight.Bold,
                SKFontStyleWidth.Normal,
                SKFontStyleSlant.Upright);
            using var paint = new SKPaint
            {
                Color = SKColors.White,
                IsAntialias = true,
                Typeface = typeface,
                TextAlign = SKTextAlign.Center,
            };

            paint.TextSize = 64f;
            var lines = WrapText.Wrap(name, paint, maxWidth, 3);
            while (paint.TextSize > 28f && TooTall(lines.Count, paint))
            {
                paint.TextSize -= 4f;
                lines = WrapText.Wrap(name, paint, maxWidth, 3);
            }

            var lineHeight = paint.TextSize * 1.2f;
            var totalHeight = lineHeight * lines.Count;
            var y = ((Size - totalHeight) / 2f) - paint.FontMetrics.Ascent;
            foreach (var line in lines)
            {
                canvas.DrawText(line, Size / 2f, y, paint);
                y += lineHeight;
            }
        }

        private static bool TooTall(int lineCount, SKPaint paint) =>
            paint.TextSize * 1.2f * lineCount > Size - 96f;

        /// <summary>A stable 0..359 hue from the name (simple FNV-ish hash), so a playlist's colour
        /// is consistent across runs and servers.</summary>
        private static float StableHue(string name)
        {
            var hash = 2166136261u;
            foreach (var c in name)
            {
                hash = (hash ^ c) * 16777619u;
            }

            return hash % 360u;
        }
    }
}
