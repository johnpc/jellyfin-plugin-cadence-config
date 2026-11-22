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
