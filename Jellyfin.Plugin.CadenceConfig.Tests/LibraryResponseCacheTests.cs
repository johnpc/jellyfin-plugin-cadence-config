using System;
using FluentAssertions;
using Jellyfin.Plugin.CadenceConfig.Storyteller;
using Xunit;

namespace Jellyfin.Plugin.CadenceConfig.Tests
{
    public class LibraryResponseCacheTests
    {
        private static readonly Guid Alice = Guid.NewGuid();
        private static readonly Guid Bob = Guid.NewGuid();

        [Fact]
        public void Fresh_ReusesAListingForSixtySeconds_PerCaller()
        {
            var clock = DateTimeOffset.Parse("2026-11-25T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
            var cache = new LibraryResponseCache(() => clock);
            var listing = LibraryStatus.Checked(new[] { ("a", ReadAlongStatus.Ready) });

            cache.Fresh(Alice).Should().BeNull();
            cache.Put(Alice, listing);
            cache.Fresh(Alice).Should().BeSameAs(listing);
            cache.Fresh(Bob).Should().BeNull("listings are per caller");

            clock = clock.AddSeconds(59);
            cache.Fresh(Alice).Should().BeSameAs(listing);
            clock = clock.AddSeconds(2);
            cache.Fresh(Alice).Should().BeNull();
        }

        [Fact]
        public void WasAligning_RemembersTheLastListing_EvenAfterItExpires()
        {
            var clock = DateTimeOffset.UnixEpoch;
            var cache = new LibraryResponseCache(() => clock);
            cache.WasAligning(Alice).Should().BeFalse();

            cache.Put(Alice, LibraryStatus.Checked(new[] { ("a", ReadAlongStatus.Aligning) }));
            clock = clock.AddMinutes(10);
            cache.WasAligning(Alice).Should().BeTrue();

            cache.Put(Alice, LibraryStatus.Checked(new[] { ("a", ReadAlongStatus.Ready) }));
            cache.WasAligning(Alice).Should().BeFalse();
        }

        [Fact]
        public void DefaultConstructor_UsesTheWallClock()
        {
            var cache = new LibraryResponseCache();
            cache.Put(Alice, LibraryStatus.Unavailable(LibraryState.Unlinked));
            cache.Fresh(Alice)!.Status.Should().Be("unlinked");
        }
    }
}
