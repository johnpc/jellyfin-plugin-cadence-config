using System;
using System.Buffers.Binary;
using System.Text;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.CadenceConfig.Storyteller
{
    /// <summary>
    /// Reads an MP4/M4A file's duration from its <c>moov/mvhd</c> atom using only byte-range reads,
    /// so a chunk's length can be learned with a few small HTTP Range requests instead of a download.
    /// Walks the top-level atoms (handling 64-bit <c>largesize</c> and size-0 "to end of file"),
    /// whether <c>moov</c> comes first (faststart) or after <c>mdat</c>.
    /// </summary>
    public static class Mp4Duration
    {
        private const int HeaderBytes = 16;
        private const long MaxMoovBytes = 16L * 1024 * 1024;

        /// <summary>Reads <c>length</c> bytes at <c>offset</c>; may return fewer at end of file.</summary>
        /// <param name="offset">The first byte.</param>
        /// <param name="length">The byte count.</param>
        /// <returns>The bytes.</returns>
        public delegate Task<byte[]> ReadRange(long offset, int length);

        /// <summary>Resolves the duration in seconds, or null when the file has no readable mvhd.</summary>
        /// <param name="read">The range reader.</param>
        /// <param name="fileSize">The file size when known (needed only for a size-0 last atom).</param>
        /// <returns>Seconds, or null.</returns>
        public static async Task<double?> ReadAsync(ReadRange read, long? fileSize)
        {
            long offset = 0;
            for (var hops = 0; hops < 64; hops++)
            {
                var head = await read(offset, HeaderBytes).ConfigureAwait(false);
                if (head.Length < 8)
                {
                    return null;
                }

                var (size, headerLength, type) = ParseHeader(head, offset, fileSize);
                if (size == null || size < headerLength)
                {
                    return null;
                }

                if (type == "moov")
                {
                    var bodyLength = size.Value - headerLength;
                    if (bodyLength <= 0 || bodyLength > MaxMoovBytes)
                    {
                        return null;
                    }

                    var body = await read(offset + headerLength, (int)bodyLength).ConfigureAwait(false);
                    return FindMvhd(body);
                }

                offset += size.Value;
            }

            return null;
        }

        /// <summary>Parses an atom header: (total size, header length, type). Size null = unknown.</summary>
        /// <param name="head">At least 8 bytes at the atom start.</param>
        /// <param name="offset">The atom's offset in the file (for size-0 atoms).</param>
        /// <param name="fileSize">The file size when known.</param>
        /// <returns>The parsed header.</returns>
        public static (long? Size, int HeaderLength, string Type) ParseHeader(byte[] head, long offset, long? fileSize)
        {
            long size = BinaryPrimitives.ReadUInt32BigEndian(head.AsSpan(0, 4));
            var type = Encoding.ASCII.GetString(head, 4, 4);
            if (size == 1)
            {
                if (head.Length < 16)
                {
                    return (null, 16, type);
                }

                return ((long)BinaryPrimitives.ReadUInt64BigEndian(head.AsSpan(8, 8)), 16, type);
            }

            if (size == 0)
            {
                return (fileSize.HasValue ? fileSize.Value - offset : null, 8, type);
            }

            return (size, 8, type);
        }

        /// <summary>Scans a moov body for mvhd and returns duration/timescale.</summary>
        /// <param name="moov">The moov atom body (children only).</param>
        /// <returns>Seconds, or null.</returns>
        public static double? FindMvhd(byte[] moov)
        {
            var at = 0;
            while (at + 8 <= moov.Length)
            {
                var (size, headerLength, type) = ParseHeader(moov[at..Math.Min(moov.Length, at + HeaderBytes)], at, moov.Length);
                if (size == null || size < headerLength)
                {
                    return null;
                }

                if (type == "mvhd")
                {
                    return ParseMvhd(moov.AsSpan(at + headerLength, (int)Math.Min(size.Value - headerLength, moov.Length - at - headerLength)));
                }

                at += (int)size.Value;
            }

            return null;
        }

        private static double? ParseMvhd(ReadOnlySpan<byte> body)
        {
            if (body.Length < 4)
            {
                return null;
            }

            var version = body[0];
            if (version == 1)
            {
                if (body.Length < 32)
                {
                    return null;
                }

                var timescale = BinaryPrimitives.ReadUInt32BigEndian(body.Slice(20, 4));
                var duration = BinaryPrimitives.ReadUInt64BigEndian(body.Slice(24, 8));
                return timescale == 0 ? null : duration / (double)timescale;
            }

            if (body.Length < 20)
            {
                return null;
            }

            var scale = BinaryPrimitives.ReadUInt32BigEndian(body.Slice(12, 4));
            var dur = BinaryPrimitives.ReadUInt32BigEndian(body.Slice(16, 4));
            return scale == 0 ? null : dur / (double)scale;
        }
    }
}
