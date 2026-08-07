using FluentAssertions;
using Jellyfin.Plugin.CadenceConfig.Audiobooks;
using MediaBrowser.Model.Querying;
using Xunit;

namespace Jellyfin.Plugin.CadenceConfig.Tests
{
    public class AudiobookLibraryFieldsTests
    {
        [Fact]
        public void Fields_IncludeTheDisplayFieldsTheClientReads()
        {
            // The detail page shows description, genres, release year (via DateCreated/ProductionYear),
            // and grouping needs ParentId — a default DTO omits these, so the endpoint MUST request them.
            AudiobookLibraryFields.Fields.Should().Contain(ItemFields.Overview);
            AudiobookLibraryFields.Fields.Should().Contain(ItemFields.Genres);
            AudiobookLibraryFields.Fields.Should().Contain(ItemFields.ParentId);
            AudiobookLibraryFields.Fields.Should().Contain(ItemFields.DateCreated);
        }

        [Fact]
        public void Fields_AreNonEmpty()
        {
            AudiobookLibraryFields.Fields.Should().NotBeEmpty();
        }
    }
}
