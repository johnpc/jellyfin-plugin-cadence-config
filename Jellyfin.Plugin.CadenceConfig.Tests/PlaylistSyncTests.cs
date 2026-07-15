using System.Collections.Generic;
using FluentAssertions;
using Jellyfin.Plugin.CadenceConfig.Sync;
using Xunit;

namespace Jellyfin.Plugin.CadenceConfig.Tests
{
    public class PlaylistSyncTests
    {
        [Fact]
        public void ComputeAdditions_ReturnsOnlyCandidatesNotAlreadyPresent()
        {
            var existing = new[] { "a", "c" };
            var candidates = new[] { "a", "b", "c", "d" };

            var additions = PlaylistSync.ComputeAdditions(existing, candidates);

            // Additive only: b and d are new; a and c already in the playlist are left alone.
            additions.Should().Equal("b", "d");
        }

        [Fact]
        public void ComputeAdditions_PreservesCandidateOrder()
        {
            var additions = PlaylistSync.ComputeAdditions(
                new List<string>(),
                new[] { "z", "m", "a" });

            additions.Should().Equal("z", "m", "a");
        }

        [Fact]
        public void ComputeAdditions_DedupesRepeatedCandidates()
        {
            var additions = PlaylistSync.ComputeAdditions(
                new List<string>(),
                new[] { "x", "x", "y", "x" });

            additions.Should().Equal("x", "y");
        }

        [Fact]
        public void ComputeAdditions_ReturnsEmptyWhenEverythingAlreadyPresent()
        {
            var additions = PlaylistSync.ComputeAdditions(
                new[] { "a", "b" },
                new[] { "a", "b" });

            additions.Should().BeEmpty();
        }

        [Fact]
        public void ComputeAdditions_IgnoresNullOrEmptyCandidateIds()
        {
            var additions = PlaylistSync.ComputeAdditions(
                new List<string>(),
                new[] { string.Empty, "real", null! });

            additions.Should().Equal("real");
        }

        [Fact]
        public void ComputeAdditions_EmptyCandidates_ReturnsEmpty()
        {
            var additions = PlaylistSync.ComputeAdditions(
                new[] { "a", "b" },
                new List<string>());

            additions.Should().BeEmpty();
        }
    }
}
