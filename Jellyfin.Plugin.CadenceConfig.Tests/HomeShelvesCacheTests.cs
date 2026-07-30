using System;
using FluentAssertions;
using Jellyfin.Plugin.CadenceConfig.Home;
using Xunit;

namespace Jellyfin.Plugin.CadenceConfig.Tests
{
    public class HomeShelvesCacheTests
    {
        [Fact]
        public void Get_ReturnsNull_OnMiss()
        {
            // A cold cache (task hasn't run for this user) must return null so the controller
            // computes on demand rather than serving empty shelves.
            new HomeShelvesCache().Get(Guid.NewGuid()).Should().BeNull();
        }

        [Fact]
        public void Set_ThenGet_ReturnsStoredResult()
        {
            var cache = new HomeShelvesCache();
            var id = Guid.NewGuid();
            var result = new HomeShelvesResult();

            cache.Set(id, result);

            cache.Get(id).Should().BeSameAs(result);
        }

        [Fact]
        public void Set_IsScopedPerUser()
        {
            var cache = new HomeShelvesCache();
            var mine = new HomeShelvesResult();
            var userA = Guid.NewGuid();

            cache.Set(userA, mine);

            // A different user's id is still a miss — shelves never leak across users.
            cache.Get(Guid.NewGuid()).Should().BeNull();
            cache.Get(userA).Should().BeSameAs(mine);
        }

        [Fact]
        public void Set_OverwritesOnRecompute()
        {
            var cache = new HomeShelvesCache();
            var id = Guid.NewGuid();
            var first = new HomeShelvesResult();
            var second = new HomeShelvesResult();

            cache.Set(id, first);
            cache.Set(id, second);

            cache.Get(id).Should().BeSameAs(second);
        }
    }
}
