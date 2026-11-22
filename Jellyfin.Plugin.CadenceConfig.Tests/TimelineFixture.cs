using System.Collections.Generic;
using Jellyfin.Plugin.CadenceConfig.Storyteller;

namespace Jellyfin.Plugin.CadenceConfig.Tests
{
    /// <summary>A two-chapter, two-chunk aligned book shared by the timeline and converter tests.</summary>
    internal static class TimelineFixture
    {
        public static ReadiumManifest Manifest() => new()
        {
            Metadata = new ReadiumMetadata { Title = "Harbor Winds" },
            ReadingOrder = new[]
            {
                new ReadiumLink { Href = "OEBPS/Text/ch1.xhtml", Type = "application/xhtml+xml", Title = "One" },
                new ReadiumLink { Href = "OEBPS/Text/ch2.xhtml", Type = "application/xhtml+xml", Title = "Two" },
                new ReadiumLink { Href = "OEBPS/Text/notes.xhtml", Type = "application/xhtml+xml", Title = "Notes" },
            },
            Resources = new[]
            {
                new ReadiumLink { Href = "OEBPS/Audio/00001-00001.mp4", Type = "audio/mp4" },
                new ReadiumLink { Href = "OEBPS/Audio/00001-00002.mp4", Type = "audio/mp4" },
            },
        };

        private static GuidedNode Node(string text, string audio, params GuidedNode[] children) => new()
        {
            Textref = text,
            Audioref = audio,
            Children = children.Length == 0 ? null : children,
        };

        public static Dictionary<string, GuidedNavigationDocument> Guides() => new()
        {
            ["OEBPS/Text/ch1.xhtml"] = new GuidedNavigationDocument
            {
                Guided = new[]
                {
                    Node("ch1.xhtml#h1", "../Audio/00001-00001.mp4#t=0,2", Node("ch1.xhtml#s1", "../Audio/00001-00001.mp4#t=2,6"), Node("ch1.xhtml#s2", "../Audio/00001-00001.mp4#t=6,10")),
                    Node("ch1.xhtml#s3", "../Audio/00001-00001.mp4#t=10,14"),
                    new GuidedNode { Textref = "ch1.xhtml#noaudio" },
                },
            },
            // Chapter two straddles the chunk boundary: its first sentence ends chunk 1, the rest are in chunk 2.
            ["OEBPS/Text/ch2.xhtml"] = new GuidedNavigationDocument
            {
                Guided = new[]
                {
                    Node("ch2.xhtml#s1", "../Audio/00001-00001.mp4#t=14,20"),
                    Node("ch2.xhtml#s2", "../Audio/00001-00002.mp4#t=0,5"),
                    Node("ch2.xhtml#s3", "../Audio/00001-00002.mp4#t=5,10"),
                },
            },
        };

        /// <summary>Chunk 1 is 20 s, chunk 2 is 10 s → total 30 s.</summary>
        public static Dictionary<string, double> Durations() => new()
        {
            ["OEBPS/Audio/00001-00001.mp4"] = 20,
            ["OEBPS/Audio/00001-00002.mp4"] = 10,
        };

        public static Timeline Build() => OverlayTimeline.Build(Manifest(), Guides(), Durations());
    }
}
