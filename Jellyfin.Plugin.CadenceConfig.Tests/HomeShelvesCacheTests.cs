using System;
using FluentAssertions;
using Jellyfin.Plugin.CadenceConfig.Home;
using Xunit;

namespace Jellyfin.Plugin.CadenceConfig.Tests
{
    public class HomeShelvesCacheTests
    {
        private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void Get_ReturnsNull_OnMiss()
        {
            // Cold cache → null so the controller triggers a background build + 503 fallback.
            new HomeShelvesCache().Get(Guid.NewGuid(), T0).Should().BeNull();
        }

        [Fact]
        public void Set_ThenGet_ReturnsStoredResult_Fresh()
        {
            var cache = new HomeShelvesCache();
            var id = Guid.NewGuid();
            var result = new HomeShelvesResult();

            cache.Set(id, result, T0);
            var hit = cache.Get(id, T0);

            hit.Should().NotBeNull();
            hit!.Value.Result.Should().BeSameAs(result);
            hit.Value.Stale.Should().BeFalse();
        }

        [Fact]
        public void Get_MarksStale_PastTheFreshnessWindow()
        {
            var cache = new HomeShelvesCache();
            var id = Guid.NewGuid();
            cache.Set(id, new HomeShelvesResult(), T0);

            // Just inside the window → fresh; just past it → stale (served anyway).
            cache.Get(id, T0 + HomeShelvesCache.FreshFor - TimeSpan.FromMinutes(1))!.Value.Stale
                .Should().BeFalse();
            cache.Get(id, T0 + HomeShelvesCache.FreshFor + TimeSpan.FromMinutes(1))!.Value.Stale
                .Should().BeTrue();
        }

        [Fact]
        public void Set_IsScopedPerUser()
        {
            var cache = new HomeShelvesCache();
            var mine = new HomeShelvesResult();
            var userA = Guid.NewGuid();

            cache.Set(userA, mine, T0);

            cache.Get(Guid.NewGuid(), T0).Should().BeNull();
            cache.Get(userA, T0)!.Value.Result.Should().BeSameAs(mine);
        }

        [Fact]
        public void Set_OverwritesAndRefreshesTimestamp()
        {
            var cache = new HomeShelvesCache();
            var id = Guid.NewGuid();
            cache.Set(id, new HomeShelvesResult(), T0);

            // A refresh past the window re-stamps freshness → no longer stale.
            var later = T0 + HomeShelvesCache.FreshFor + TimeSpan.FromHours(1);
            var second = new HomeShelvesResult();
            cache.Set(id, second, later);

            var hit = cache.Get(id, later);
            hit!.Value.Result.Should().BeSameAs(second);
            hit.Value.Stale.Should().BeFalse();
        }
    }
}
