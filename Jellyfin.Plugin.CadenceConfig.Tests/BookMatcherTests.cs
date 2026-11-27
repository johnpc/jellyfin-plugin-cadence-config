using System.Collections.Generic;
using FluentAssertions;
using Jellyfin.Plugin.CadenceConfig.Storyteller;
using Xunit;

namespace Jellyfin.Plugin.CadenceConfig.Tests
{
    public class BookMatcherTests
    {
        private static StorytellerBook Book(string uuid, string title, params string[] authors)
        {
            var creators = new List<StorytellerCreator>();
            foreach (var a in authors)
            {
                creators.Add(new StorytellerCreator { Name = a });
            }

            return new StorytellerBook { Uuid = uuid, Title = title, Authors = creators };
        }

        private static StorytellerBook InFolder(string uuid, string title, string folder, params string[] authors)
        {
            var book = Book(uuid, title, authors);
            book.Audiobook = new StorytellerAudiobook { Filepath = "/library/" + folder };
            return book;
        }

        private static readonly List<StorytellerBook> Live = new()
        {
            // Real Storyteller records from 2026-11-25 that the title-only matcher could not reach.
            InFolder("phm", "A Novel", "Andy Weir - Project Hail Mary", "Andy Weir"),
            InFolder("exh", "Stories", "Ted Chiang - Exhalation Stories", "Ted Chiang"),
            InFolder("br", "Do Androids Dream of Electric Sheep?", "Philip K. Dick - Blade Runner", "Philip K. Dick"),
            InFolder("4hw", "The 4-Hour Workweek: Escape 9&#8211;5, Live Anywhere, and Join the New Rich - Expanded and Updated", "Timothy Ferriss - The 4-Hour Workweek", "Timothy Ferriss"),
            InFolder("spin", "Spin", "Robert Charles Wilson - Spin - Book 1", "Robert Charles Wilson"),
            InFolder("ft1", "The Fourth Turning: An American Prophecy", "William Strauss - The Fourth Turning", "William Strauss", "Neil Howe"),
            InFolder("ft2", "The Fourth Turning Is Here", "Neil Howe - The Fourth Turning Is Here What the Seasons of History Tell Us About How and When This Crisis Will", "Neil Howe"),
            InFolder("dcc1", "Dungeon Crawler Carl: A LitRPG/Gamelit Adventure", "Matt Dinniman - Dungeon Crawler Carl", "Matt Dinniman"),
            InFolder("carl", "Carl's Doomsday Scenario", "Matt Dinniman - Carl's Doomsday Scenario", "Matt Dinniman"),
        };

        [Theory]
        [InlineData("Project Hail Mary", "Andy Weir", "phm")]
        [InlineData("Exhalation: Stories", "Ted Chiang", "exh")]
        [InlineData("Blade Runner", "Philip K. Dick (Blade Runner)", "br")]
        [InlineData("The 4-Hour Workweek: Escape 9-5, Live Anywhere, and Join the New Rich", "Timothy Ferriss", "4hw")]
        public void Match_UsesTheAudiobookFolderAndDecodesEntities(string title, string author, string expected)
        {
            BookMatcher.Match(title, author, Live)!.Uuid.Should().Be(expected);
        }

        [Fact]
        public void Match_TriesEachJellyfinTitle_AlbumThenName()
        {
            BookMatcher.Match(new[] { "01 Spin", "Spin" }, "Robert Charles Wilson (Spin)", Live)!.Uuid.Should().Be("spin");
            BookMatcher.Match(new[] { "Dungeon Crawler Carl, Book 2 - Carl's Doomsday Scenario", "Carl's Doomsday Scenario" }, "Matt Dinniman", Live)!.Uuid.Should().Be("carl");
            BookMatcher.Match(new string?[] { null, " ", "Spin" }, "Robert Charles Wilson", Live)!.Uuid.Should().Be("spin");
            BookMatcher.Match(new[] { "Nothing", "Still Nothing" }, "Andy Weir", Live).Should().BeNull();
        }

        [Fact]
        public void Match_PrefersAnExactTitleOverASeriesPrefix()
        {
            // 0.0.0.29 regression found live: the album tag starts with book 1's folder title.
            var titles = new[] { "Dungeon Crawler Carl, Book 2 - Carl's Doomsday Scenario", "Carl's Doomsday Scenario" };
            BookMatcher.Match(titles, "Matt Dinniman", Live)!.Uuid.Should().Be("carl");
            BookMatcher.Match("Dungeon Crawler Carl", "Matt Dinniman", Live)!.Uuid.Should().Be("dcc1");
        }

        [Fact]
        public void Match_ToleranceKeepsTheMostSpecificTitle()
        {
            // 0.0.0.30 live: the sequel matched the original because "the fourth turning" also prefixes it.
            var sequel = new[] { "The Fourth Turning Is Here: What the Seasons of History Tell Us About How and When This Crisis Will End" };
            BookMatcher.Match(sequel, "Neil Howe", Live)!.Uuid.Should().Be("ft2");
            BookMatcher.Match(new[] { "The Fourth Turning: An American Prophecy", "The Fourth Turning" }, "William Strauss, Neil Howe", Live)!.Uuid.Should().Be("ft1");
        }

        [Fact]
        public void Match_FallsBackToSubtitleToleranceWhenNothingIsExact()
        {
            BookMatcher.Match(new[] { "Dungeon Crawler Carl, Book 9", "Nope" }, "Matt Dinniman", Live)!.Uuid.Should().Be("dcc1");
        }

        [Fact]
        public void Match_FolderTitleStillRequiresTheAuthor()
        {
            BookMatcher.Match("Project Hail Mary", "Someone Else", Live).Should().BeNull();
        }

        private static readonly List<StorytellerBook> Library = new()
        {
            Book("1", "A Gentleman in Moscow", "Amor Towles"),
            Book("2", "Dune", "Frank Herbert"),
            Book("3", "Dune", "Someone Else"),
            Book("4", "The Fourth Turning: An American Prophecy", "William Strauss", "Neil Howe"),
            Book("5", "Do Androids Dream of Electric Sheep?", "Philip K. Dick"),
        };

        [Theory]
        [InlineData("A Gentleman in Moscow (Unabridged)", "Amor Towles", "1")]
        [InlineData("a gentleman in moscow", "Towles, Amor", "1")]
        [InlineData("Dune", "Frank Herbert", "2")]
        [InlineData("Dune", "Herbert", "2")]
        [InlineData("Dune", "Someone Else", "3")]
        [InlineData("The Fourth Turning", "Neil Howe", "4")]
        [InlineData("Do Androids Dream of Electric Sheep", "Philip K Dick", "5")]
        public void Match_FindsTheRightBook(string title, string author, string expected)
        {
            BookMatcher.Match(title, author, Library)!.Uuid.Should().Be(expected);
        }

        [Fact]
        public void Match_RefusesWhenAuthorsDisagree_OrTitleUnknown()
        {
            BookMatcher.Match("Dune", "Arthur C. Clarke", Library).Should().BeNull();
            BookMatcher.Match("Nonexistent Book", "Amor Towles", Library).Should().BeNull();
            BookMatcher.Match(string.Empty, "Amor Towles", Library).Should().BeNull();
        }

        [Fact]
        public void Match_WithoutAnAuthor_FallsBackToTitleOnly()
        {
            BookMatcher.Match("A Gentleman in Moscow", null, Library)!.Uuid.Should().Be("1");
            BookMatcher.Match("Dune", string.Empty, Library)!.Uuid.Should().Be("2");
        }

        [Theory]
        [InlineData("The Institute [Audiobook] (Unabridged)", "the institute")]
        [InlineData("Graveyard Shift: A Novel", "graveyard shift")]
        [InlineData("  Multiple   Spaces -- here! ", "multiple spaces here")]
        [InlineData("Escape 9&#8211;5 &amp; More", "escape 9 5 more")]
        [InlineData(null, "")]
        public void Normalize_StripsNoise(string? input, string expected)
        {
            BookMatcher.Normalize(input).Should().Be(expected);
        }

        [Fact]
        public void AuthorTokens_DropsInitialsAndPunctuation()
        {
            BookMatcher.AuthorTokens("Dick, Philip K.").Should().BeEquivalentTo(new[] { "dick", "philip" });
            BookMatcher.AuthorTokens(null).Should().BeEmpty();
        }
    }
}
