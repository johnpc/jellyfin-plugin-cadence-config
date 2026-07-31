using System;
using FluentAssertions;
using Jellyfin.Plugin.CadenceConfig.Audiobooks;
using Xunit;

namespace Jellyfin.Plugin.CadenceConfig.Tests
{
    public class AudiobooksCacheTests
    {
        private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void Get_ReturnsNull_OnMiss()
        {
            // Cold cache → null so the controller triggers a background build + 503 fallback.
            new AudiobooksCache().Get(Guid.NewGuid(), T0).Should().BeNull();
        }

        [Fact]
        public void Set_ThenGet_ReturnsStoredResult_Fresh()
        {
            var cache = new AudiobooksCache();
            var id = Guid.NewGuid();
            var result = new AudiobooksResult();

            cache.Set(id, result, T0);
            var hit = cache.Get(id, T0);

            hit.Should().NotBeNull();
            hit!.Value.Result.Should().BeSameAs(result);
            hit.Value.Stale.Should().BeFalse();
        }

        [Fact]
        public void Get_MarksStale_PastTheFreshnessWindow()
        {
            var cache = new AudiobooksCache();
            var id = Guid.NewGuid();
            cache.Set(id, new AudiobooksResult(), T0);

            // Just inside the window → fresh; just past it → stale (served anyway).
            cache.Get(id, T0 + AudiobooksCache.FreshFor - TimeSpan.FromMinutes(1))!.Value.Stale
                .Should().BeFalse();
            cache.Get(id, T0 + AudiobooksCache.FreshFor + TimeSpan.FromMinutes(1))!.Value.Stale
                .Should().BeTrue();
        }

        [Fact]
        public void Set_IsScopedPerUser()
        {
            var cache = new AudiobooksCache();
            var mine = new AudiobooksResult();
            var userA = Guid.NewGuid();

            cache.Set(userA, mine, T0);

            cache.Get(Guid.NewGuid(), T0).Should().BeNull();
            cache.Get(userA, T0)!.Value.Result.Should().BeSameAs(mine);
        }

        [Fact]
        public void Set_OverwritesAndRefreshesTimestamp()
        {
            var cache = new AudiobooksCache();
            var id = Guid.NewGuid();
            cache.Set(id, new AudiobooksResult(), T0);

            // A refresh past the window re-stamps freshness → no longer stale.
            var later = T0 + AudiobooksCache.FreshFor + TimeSpan.FromHours(1);
            var second = new AudiobooksResult();
            cache.Set(id, second, later);

            var hit = cache.Get(id, later);
            hit!.Value.Result.Should().BeSameAs(second);
            hit.Value.Stale.Should().BeFalse();
        }

        [Fact]
        public void Invalidate_MakesTheNextGetAMiss()
        {
            var cache = new AudiobooksCache();
            var id = Guid.NewGuid();
            cache.Set(id, new AudiobooksResult(), T0);
            cache.Get(id, T0).Should().NotBeNull();

            cache.Invalidate(id);

            cache.Get(id, T0).Should().BeNull(); // forces a fresh rebuild
        }

        [Fact]
        public void Clear_DropsEveryUser()
        {
            var cache = new AudiobooksCache();
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            cache.Set(a, new AudiobooksResult(), T0);
            cache.Set(b, new AudiobooksResult(), T0);

            cache.Clear();

            cache.Get(a, T0).Should().BeNull();
            cache.Get(b, T0).Should().BeNull();
        }
    }
}
