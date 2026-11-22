using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Jellyfin.Plugin.CadenceConfig.Storyteller;
using Xunit;

namespace Jellyfin.Plugin.CadenceConfig.Tests
{
    public class ChunkGapsTests
    {
        private static Timeline Raw(params (string File, double Offset)[] files)
        {
            // Three files of 10 s each unless offsets say otherwise; two clips per file.
            var clips = new List<ReadAlongClip>();
            foreach (var (_, offset) in files)
            {
                clips.Add(new ReadAlongClip { Start = offset + 1, End = offset + 4, Chapter = 0, Fragment = $"s{offset + 1}" });
                clips.Add(new ReadAlongClip { Start = offset + 5, End = offset + 9, Chapter = 0, Fragment = $"s{offset + 5}" });
            }

            return new Timeline
            {
                AudioFiles = files.Select(f => f.File).ToList(),
                AudioOffsets = files.Select(f => f.Offset).ToList(),
                Total = files[^1].Offset + 10,
                Clips = clips,
            };
        }

        [Theory]
        [InlineData("OEBPS/Audio/00001-00022.mp4", 22)]
        [InlineData("a/b-7.m4a", 7)]
        [InlineData("OEBPS/Audio/intro.mp4", null)]
        public void ChunkNumber_ReadsTheTrailingNumber(string file, int? expected)
        {
            ChunkGaps.ChunkNumber(file).Should().Be(expected);
        }

        [Fact]
        public void Reconcile_WithinTolerance_ReturnsTheSameTimeline()
        {
            var raw = Raw(("x-1.mp4", 0), ("x-2.mp4", 10));
            ChunkGaps.Reconcile(raw, 22).Should().BeSameAs(raw);
        }

        [Fact]
        public void Reconcile_LongerThanTheAudiobook_StaysUnchanged_SoCheckReportsMismatch()
        {
            var raw = Raw(("x-1.mp4", 0), ("x-2.mp4", 10));
            var t = ChunkGaps.Reconcile(raw, 12);
            t.Should().BeSameAs(raw);
            OverlayTimeline.Check(t, 12).Should().Be(ReadAlongStatus.Mismatch);
        }

        [Fact]
        public void Reconcile_MissingMiddleChunk_InsertsTheLeftoverThere_AndShiftsLaterClips()
        {
            // Chunks 1, 3: chunk 2 (20 s of silence) was never embedded. Audiobook = 10 + 20 + 10.
            var raw = Raw(("x-1.mp4", 0), ("x-3.mp4", 10));
            var t = ChunkGaps.Reconcile(raw, 40);

            t.Total.Should().Be(40);
            t.AudioOffsets.Should().Equal(0, 30);
            t.Clips.Select(c => c.Start).Should().Equal(1, 5, 31, 35);
            t.Clips.Select(c => c.End).Should().Equal(4, 9, 34, 39);
            t.Clips.Select(c => c.Fragment).Should().Equal("s1", "s5", "s11", "s15");
            OverlayTimeline.Check(t, 40).Should().Be(ReadAlongStatus.Ready);
        }

        [Fact]
        public void Reconcile_TwoHoles_ShareTheLeftoverEvenly()
        {
            var raw = Raw(("x-1.mp4", 0), ("x-3.mp4", 10), ("x-5.mp4", 20));
            var t = ChunkGaps.Reconcile(raw, 50);
            t.AudioOffsets.Should().Equal(0, 20, 40);
            t.Clips.Select(c => c.Start).Should().Equal(1, 5, 21, 25, 41, 45);
        }

        [Fact]
        public void Reconcile_NoNumberedHole_TreatsTheLeftoverAsTrailingAudio()
        {
            var raw = Raw(("x-1.mp4", 0), ("x-2.mp4", 10));
            var t = ChunkGaps.Reconcile(raw, 45);
            t.Total.Should().Be(45);
            t.AudioOffsets.Should().Equal(0, 10);
            t.Clips.Select(c => c.Start).Should().Equal(1, 5, 11, 15);
            OverlayTimeline.Check(t, 45).Should().Be(ReadAlongStatus.Ready);
        }

        [Fact]
        public void Reconcile_UnnumberedFiles_CannotPlaceTheGap_SoNothingShifts()
        {
            var raw = Raw(("intro.mp4", 0), ("body.mp4", 10));
            var t = ChunkGaps.Reconcile(raw, 40);
            t.AudioOffsets.Should().Equal(0, 10);
            t.Total.Should().Be(40);
        }
    }
}
