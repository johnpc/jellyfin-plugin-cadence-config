using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Jellyfin.Plugin.CadenceConfig.Storyteller;
using Xunit;

namespace Jellyfin.Plugin.CadenceConfig.Tests
{
    public class LibraryStatusTests
    {
        private static StorytellerBook Book(string? alignedAt) => new() { Uuid = "u", Title = "Dune", AlignedAt = alignedAt };

        private static Timeline Timeline(double total, int clips) => new()
        {
            Total = total,
            Clips = Enumerable.Range(0, clips).Select(i => new ReadAlongClip { Start = i, End = i + 1, Fragment = $"s{i}" }).ToList(),
        };

        [Fact]
        public void For_NoMatch_IsUnmatched()
        {
            LibraryStatus.For(null, null, 100).Should().Be(ReadAlongStatus.Unmatched);
        }

        [Fact]
        public void For_MatchedButNotAligned_IsAligning_EvenWithAStaleCachedTimeline()
        {
            LibraryStatus.For(Book(null), Timeline(100, 3), 100).Should().Be(ReadAlongStatus.Aligning);
        }

        [Fact]
        public void For_AlignedWithoutACachedTimeline_IsReady_WithoutBuildingOne()
        {
            LibraryStatus.For(Book("2026-11-22"), null, 100).Should().Be(ReadAlongStatus.Ready);
        }

        [Fact]
        public void For_AlignedWithACachedTimeline_UsesItsRealVerdict()
        {
            LibraryStatus.For(Book("2026-11-22"), Timeline(100, 3), 100).Should().Be(ReadAlongStatus.Ready);
            LibraryStatus.For(Book("2026-11-22"), Timeline(100, 0), 100).Should().Be(ReadAlongStatus.Empty);
            LibraryStatus.For(Book("2026-11-22"), Timeline(80, 3), 100).Should().Be(ReadAlongStatus.Mismatch);
        }

        [Fact]
        public void Checked_WiresStatuses_AndFlagsAligning()
        {
            var r = LibraryStatus.Checked(new List<(string, ReadAlongStatus)>
            {
                ("a", ReadAlongStatus.Ready), ("b", ReadAlongStatus.Aligning), ("c", ReadAlongStatus.Unmatched),
                ("d", ReadAlongStatus.Empty), ("e", ReadAlongStatus.Mismatch),
            });

            r.Status.Should().Be("ok");
            r.HasAligning.Should().BeTrue();
            r.Books.Select(b => (b.ItemId, b.Status)).Should().Equal(
                ("a", "ready"), ("b", "aligning"), ("c", "unmatched"), ("d", "empty"), ("e", "mismatch"));
            LibraryStatus.Checked(new[] { ("a", ReadAlongStatus.Ready) }).HasAligning.Should().BeFalse();
        }

        [Theory]
        [InlineData(LibraryState.Unlinked, "unlinked")]
        [InlineData(LibraryState.Unconfigured, "unconfigured")]
        public void Unavailable_CarriesTheReason_AndNoBooks(LibraryState state, string wire)
        {
            var r = LibraryStatus.Unavailable(state);
            r.Status.Should().Be(wire);
            r.Books.Should().BeEmpty();
            r.HasAligning.Should().BeFalse();
        }

        [Fact]
        public void ReadAlongService_Reconcile_FallsBackToTheLeftoverSplit_WithoutUsableMarkers()
        {
            var raw = new Timeline
            {
                AudioFiles = new[] { "x-1.mp4", "x-3.mp4" },
                AudioOffsets = new[] { 0.0, 10.0 },
                Total = 20,
                Clips = new List<ReadAlongClip> { new() { Start = 11, End = 12, Fragment = "s" } },
            };

            var t = ReadAlongService.Reconcile(raw, 40, Array.Empty<double>());

            t.Total.Should().Be(40);
            t.Clips.Single().Start.Should().Be(31);
        }
    }
}
