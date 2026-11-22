using FluentAssertions;
using Jellyfin.Plugin.CadenceConfig.Storyteller;
using Xunit;

namespace Jellyfin.Plugin.CadenceConfig.Tests
{
    public class ProvisionNamesTests
    {
        [Theory]
        [InlineData("John", "jf-john")]
        [InlineData("cadence-demo", "jf-cadence-demo")]
        [InlineData("Kimmy Mango!!", "jf-kimmy-mango")]
        [InlineData("émile.zola", "jf-mile-zola")]
        [InlineData("___", "jf-user")]
        public void For_SlugsTheJellyfinUsername(string jellyfin, string expected)
        {
            ProvisionNames.For(jellyfin).Should().Be(expected);
        }

        [Fact]
        public void For_AppendsTheAttemptNumber_AndKeepsWithinTheLengthCap()
        {
            ProvisionNames.For("john", 2).Should().Be("jf-john-2");
            var longName = ProvisionNames.For("a-very-long-jellyfin-user-name-indeed", 3);
            longName.Should().HaveLength(ProvisionNames.MaxLength);
            longName.Should().StartWith("jf-a-very-long").And.EndWith("-3");
        }

        [Fact]
        public void EmailFor_UsesTheInvalidTld_SoNothingIsEverDelivered()
        {
            ProvisionNames.EmailFor("jf-john").Should().Be("jf-john@cadence.invalid");
        }

        [Fact]
        public void Slug_CollapsesRunsAndTrimsDashes()
        {
            ProvisionNames.Slug("  Hello -- World  ").Should().Be("hello-world");
            ProvisionNames.Slug(null).Should().BeEmpty();
        }
    }
}
