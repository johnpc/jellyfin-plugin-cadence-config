using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Jellyfin.Plugin.CadenceConfig.Storyteller;
using Xunit;

namespace Jellyfin.Plugin.CadenceConfig.Tests
{
    public class TimelineCacheTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "cadence-tl-" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, true);
            }
        }

        [Fact]
        public void FileName_IsStableAndSafe()
        {
            TimelineCache.FileName("u1", "a|b").Should().Be(TimelineCache.FileName("u1", "a|b"));
            TimelineCache.FileName("u1", "a|b").Should().NotBe(TimelineCache.FileName("u1", "a|c"));
            TimelineCache.FileName("u1", "x/y:z").Should().MatchRegex("^u1-[0-9A-F]{16}\\.json$");
        }

        [Fact]
        public async Task Put_ThenGet_FromMemoryAndFromDiskAfterRestart()
        {
            var timeline = TimelineFixture.Build();
            timeline.BookUuid = "u1";
            timeline.Fingerprint = "fp";

            var cache = new TimelineCache(_dir);
            (await cache.GetAsync("u1", "fp", CancellationToken.None)).Should().BeNull();
            await cache.PutAsync(timeline, CancellationToken.None);
            (await cache.GetAsync("u1", "fp", CancellationToken.None)).Should().BeSameAs(timeline);

            var restarted = new TimelineCache(_dir);
            var loaded = await restarted.GetAsync("u1", "fp", CancellationToken.None);
            loaded!.Clips.Should().HaveCount(timeline.Clips.Count);
            loaded.AudioOffsets.Should().Equal(timeline.AudioOffsets);
            (await restarted.GetAsync("u1", "other", CancellationToken.None)).Should().BeNull();
        }

        [Fact]
        public async Task CorruptDiskEntry_ReadsAsMiss()
        {
            Directory.CreateDirectory(_dir);
            await File.WriteAllTextAsync(Path.Combine(_dir, TimelineCache.FileName("u1", "fp")), "{not json");
            (await new TimelineCache(_dir).GetAsync("u1", "fp", CancellationToken.None)).Should().BeNull();
        }
    }
}
