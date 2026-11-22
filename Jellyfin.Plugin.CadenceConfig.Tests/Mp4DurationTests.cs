using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Jellyfin.Plugin.CadenceConfig.Storyteller;
using Xunit;

namespace Jellyfin.Plugin.CadenceConfig.Tests
{
    public class Mp4DurationTests
    {
        private static byte[] Atom(string type, byte[] body, bool largesize = false)
        {
            var header = new List<byte>();
            var size = (largesize ? 16 : 8) + body.Length;
            var b4 = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(b4, largesize ? 1u : (uint)size);
            header.AddRange(b4);
            header.AddRange(Encoding.ASCII.GetBytes(type));
            if (largesize)
            {
                var b8 = new byte[8];
                BinaryPrimitives.WriteUInt64BigEndian(b8, (ulong)size);
                header.AddRange(b8);
            }

            return header.Concat(body).ToArray();
        }

        private static byte[] Mvhd0(uint timescale, uint duration)
        {
            var body = new byte[20];
            BinaryPrimitives.WriteUInt32BigEndian(body.AsSpan(12, 4), timescale);
            BinaryPrimitives.WriteUInt32BigEndian(body.AsSpan(16, 4), duration);
            return Atom("mvhd", body);
        }

        private static byte[] Mvhd1(uint timescale, ulong duration)
        {
            var body = new byte[32];
            body[0] = 1;
            BinaryPrimitives.WriteUInt32BigEndian(body.AsSpan(20, 4), timescale);
            BinaryPrimitives.WriteUInt64BigEndian(body.AsSpan(24, 8), duration);
            return Atom("mvhd", body);
        }

        private static Mp4Duration.ReadRange Reader(byte[] file, List<(long, int)>? log = null)
        {
            return (offset, length) =>
            {
                log?.Add((offset, length));
                if (offset >= file.Length)
                {
                    return Task.FromResult(Array.Empty<byte>());
                }

                var end = (int)Math.Min(file.Length, offset + length);
                return Task.FromResult(file[(int)offset..end]);
            };
        }

        [Fact]
        public async Task MoovFirst_ReadsDurationFromMvhdV0()
        {
            var file = Atom("ftyp", new byte[16])
                .Concat(Atom("moov", Atom("trak", new byte[4]).Concat(Mvhd0(1000, 125_500)).ToArray()))
                .Concat(Atom("mdat", new byte[100]))
                .ToArray();

            (await Mp4Duration.ReadAsync(Reader(file), null)).Should().BeApproximately(125.5, 0.001);
        }

        [Fact]
        public async Task MoovLast_SkipsMdatByItsSizeWithoutReadingIt()
        {
            var log = new List<(long, int)>();
            var file = Atom("ftyp", new byte[16])
                .Concat(Atom("mdat", new byte[5000]))
                .Concat(Atom("moov", Mvhd1(44100, 44100UL * 3661)))
                .ToArray();

            (await Mp4Duration.ReadAsync(Reader(file, log), file.Length)).Should().BeApproximately(3661, 0.001);
            log.Sum(e => e.Item2).Should().BeLessThan(200, "only headers and the moov body are read, never the media data");
        }

        [Fact]
        public async Task Largesize_IsHonoured()
        {
            var file = Atom("mdat", new byte[64], largesize: true).Concat(Atom("moov", Mvhd0(10, 55))).ToArray();
            (await Mp4Duration.ReadAsync(Reader(file), null)).Should().Be(5.5);
        }

        [Fact]
        public async Task SizeZeroMoov_UsesFileSizeWhenKnown()
        {
            var moov = Atom("moov", Mvhd0(10, 20));
            BinaryPrimitives.WriteUInt32BigEndian(moov.AsSpan(0, 4), 0);
            var file = Atom("ftyp", new byte[4]).Concat(moov).ToArray();
            (await Mp4Duration.ReadAsync(Reader(file), file.Length)).Should().Be(2);
            (await Mp4Duration.ReadAsync(Reader(file), null)).Should().BeNull();
        }

        [Fact]
        public async Task NoMvhd_OrTruncated_ReturnsNull()
        {
            (await Mp4Duration.ReadAsync(Reader(Atom("moov", Atom("trak", new byte[8]))), null)).Should().BeNull();
            (await Mp4Duration.ReadAsync(Reader(new byte[3]), null)).Should().BeNull();
            (await Mp4Duration.ReadAsync(Reader(Atom("free", new byte[2])), null)).Should().BeNull();
        }

        [Fact]
        public void FindMvhd_RejectsZeroTimescaleAndShortBodies()
        {
            Mp4Duration.FindMvhd(Mvhd0(0, 5)).Should().BeNull();
            Mp4Duration.FindMvhd(Atom("mvhd", new byte[2])).Should().BeNull();
            var v1Short = Atom("mvhd", new byte[8]);
            v1Short[8] = 1;
            Mp4Duration.FindMvhd(v1Short).Should().BeNull();
        }
    }
}
