using System;
using System.Threading.Tasks;
using FluentAssertions;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.CadenceConfig.Audiobooks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.CadenceConfig.Tests
{
    public class AudiobooksRefresherTests
    {
        // A fake service so we don't need real Jellyfin library/DTO managers; counts builds.
        private sealed class FakeService : IAudiobooksService
        {
            public int Builds;
            public AudiobooksResult Result { get; } = new();

            public AudiobooksResult Build(User user)
            {
                Builds++;
                return Result;
            }
        }

        private static User NewUser() => new("tester", "default", "default");
        private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        [Fact]
        public async Task RefreshAsync_BuildsAndCachesTheUsersLibrary()
        {
            var svc = new FakeService();
            var cache = new AudiobooksCache();
            var refresher = new AudiobooksRefresher(svc, cache, NullLogger<AudiobooksRefresher>.Instance);
            var user = NewUser();

            await refresher.RefreshAsync(user, () => T0);

            svc.Builds.Should().Be(1);
            var hit = cache.Get(user.Id, T0);
            hit.Should().NotBeNull();
            hit!.Value.Result.Should().BeSameAs(svc.Result);
            hit.Value.Stale.Should().BeFalse();
        }

        [Fact]
        public async Task RefreshAsync_DedupesConcurrentRebuildsForTheSameUser()
        {
            // A slow build lets a second call land while the first is in flight; the
            // second must be a no-op (dedup) — only ONE build runs.
            var gate = new TaskCompletionSource();
            var svc = new BlockingService(gate.Task);
            var cache = new AudiobooksCache();
            var refresher = new AudiobooksRefresher(svc, cache, NullLogger<AudiobooksRefresher>.Instance);
            var user = NewUser();

            var first = refresher.RefreshAsync(user, () => T0);
            var second = refresher.RefreshAsync(user, () => T0); // deduped — returns immediately
            await second;
            gate.SetResult();
            await first;

            svc.Builds.Should().Be(1);
        }

        private sealed class BlockingService : IAudiobooksService
        {
            private readonly Task _gate;
            public int Builds;

            public BlockingService(Task gate) => _gate = gate;

            public AudiobooksResult Build(User user)
            {
                Builds++;
                _gate.GetAwaiter().GetResult();
                return new AudiobooksResult();
            }
        }
    }
}
