using System.Collections.Generic;
using FluentAssertions;
using Jellyfin.Plugin.CadenceConfig.Deezer;
using Xunit;

namespace Jellyfin.Plugin.CadenceConfig.Tests
{
    public class DeezerMatcherTests
    {
        private static DeezerTrack Track(string artist, string title) =>
            new() { Title = title, Artist = new DeezerArtist { Name = artist } };

        [Fact]
        public void Match_FoundTracksReturnLibraryIds_MissingReturnArtists()
        {
            var library = new Dictionary<TrackKey, string>
            {
                [new TrackKey("The Beatles", "Hey Jude")] = "jf-heyjude",
                [new TrackKey("Radiohead", "Creep")] = "jf-creep",
            };
            var deezer = new[]
            {
                Track("The Beatles", "Hey Jude (Remastered 2015)"), // matches (catalog noise)
                Track("Radiohead", "Creep"), // matches
                Track("Boygenius", "Not Strong Enough"), // missing → artist
            };

            var result = DeezerMatcher.Match(deezer, library);

            result.FoundItemIds.Should().Equal("jf-heyjude", "jf-creep");
            result.MissingArtists.Should().Equal("Boygenius");
            result.FoundCount.Should().Be(2);
            result.MissingArtistCount.Should().Be(1);
        }

        [Fact]
        public void Match_DedupsFoundIdsAndMissingArtists()
        {
            var library = new Dictionary<TrackKey, string>
            {
                [new TrackKey("Daft Punk", "One More Time")] = "jf-omt",
            };
            var deezer = new[]
            {
                Track("Daft Punk", "One More Time"),
                Track("Daft Punk", "One More Time (Radio Edit)"), // same normalized → same id
                Track("Phoebe Bridgers", "Motion Sickness"),
                Track("phoebe bridgers", "Kyoto"), // second miss, SAME artist → dedup'd
            };

            var result = DeezerMatcher.Match(deezer, library);

            result.FoundItemIds.Should().Equal("jf-omt"); // deduped
            result.MissingArtists.Should().Equal("Phoebe Bridgers"); // deduped by normalized name
        }

        [Fact]
        public void Match_SkipsTracksWithNoArtistOrTitle()
        {
            var deezer = new[]
            {
                new DeezerTrack { Title = "No Artist", Artist = null },
                new DeezerTrack { Title = null, Artist = new DeezerArtist { Name = "No Title" } },
            };

            var result = DeezerMatcher.Match(deezer, new Dictionary<TrackKey, string>());

            result.FoundItemIds.Should().BeEmpty();
            result.MissingArtists.Should().BeEmpty();
        }
    }
}
