using FluentAssertions;
using Jellyfin.Plugin.CadenceConfig.Storyteller;
using Xunit;

namespace Jellyfin.Plugin.CadenceConfig.Tests
{
    public class BookTitlesTests
    {
        [Theory]
        [InlineData("/library/Andy Weir - Project Hail Mary", "Project Hail Mary")]
        [InlineData("/library/Philip K. Dick - Blade Runner/", "Blade Runner")]
        [InlineData("/library/Ted Chiang - Exhalation - Stories", "Exhalation - Stories")]
        [InlineData("/library/No Author Prefix", "No Author Prefix")]
        [InlineData(@"C:\library\Matt Haig - The Midnight Library", "The Midnight Library")]
        [InlineData(null, "")]
        [InlineData("  ", "")]
        public void FolderTitle_TakesTheTitleAfterTheAuthor(string? path, string expected)
        {
            BookTitles.FolderTitle(path).Should().Be(expected);
        }

        [Fact]
        public void Of_ListsStorytellerTitleThenFolderTitle()
        {
            var book = new StorytellerBook { Title = "A Novel", Audiobook = new StorytellerAudiobook { Filepath = "/library/Andy Weir - Project Hail Mary" } };
            BookTitles.Of(book).Should().Equal("A Novel", "Project Hail Mary");
        }

        [Fact]
        public void Of_SkipsMissingParts()
        {
            BookTitles.Of(new StorytellerBook { Title = "Dune" }).Should().Equal("Dune");
            BookTitles.Of(new StorytellerBook { Title = " ", Audiobook = new StorytellerAudiobook() }).Should().BeEmpty();
        }
    }
}
