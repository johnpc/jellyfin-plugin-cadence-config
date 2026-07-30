using System.Collections.Generic;
using FluentAssertions;
using Jellyfin.Plugin.CadenceConfig.Grab;
using Xunit;

namespace Jellyfin.Plugin.CadenceConfig.Tests
{
    public class GrabPickTests
    {
        private static GrabResult Result(string title, int score, string? artist = null) =>
            new() { Title = title, Artist = artist, QualityScore = score, VideoId = title };

        [Fact]
        public void PicksHighestQualityAmongTitleMatches()
        {
            var results = new List<GrabResult>
            {
                Result("Africa", 100),
                Result("Toto - Africa (Official Audio)", 300), // higher score, still matches "africa"
                Result("Africa (Live)", 150),
            };

            var best = GrabPick.Best(results, "Africa", "Toto");

            best.Should().NotBeNull();
            best!.QualityScore.Should().Be(300);
        }

        [Fact]
        public void RejectsResultsThatDontShareTheTitle()
        {
            var results = new List<GrabResult> { Result("Completely Different Song", 999) };
            GrabPick.Best(results, "Africa", "Toto").Should().BeNull();
        }

        [Fact]
        public void IgnoresParentheticalNoiseInTheWantedTitle()
        {
            // Wanted "Hey Jude (Remastered 2015)" should still match a plain "Hey Jude".
            var results = new List<GrabResult> { Result("Hey Jude", 200) };
            GrabPick.Best(results, "Hey Jude (Remastered 2015)", "The Beatles").Should().NotBeNull();
        }

        [Fact]
        public void RequiresAtLeastHalfTheTitleWords()
        {
            // "One" alone (1 of 3 words) isn't enough for "One More Time".
            var results = new List<GrabResult> { Result("One", 500) };
            GrabPick.Best(results, "One More Time", "Daft Punk").Should().BeNull();
        }

        [Fact]
        public void ReturnsNullForNoResults()
        {
            GrabPick.Best(new List<GrabResult>(), "Africa", "Toto").Should().BeNull();
        }
    }
}
