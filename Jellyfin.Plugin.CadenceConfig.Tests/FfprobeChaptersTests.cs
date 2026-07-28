using FluentAssertions;
using Jellyfin.Plugin.CadenceConfig.Chapters;
using Xunit;

namespace Jellyfin.Plugin.CadenceConfig.Tests
{
    public class FfprobeChaptersTests
    {
        // A trimmed sample of the REAL ffprobe -show_chapters output from the server's
        // "Blade Runner - Book 1.m4b" (25 chapters); start_time is a string of seconds.
        private const string BladeRunnerJson = @"{
            ""chapters"": [
                { ""id"": 0, ""start_time"": ""0.000000"", ""end_time"": ""16.911000"", ""tags"": { ""title"": ""Opening Credits"" } },
                { ""id"": 1, ""start_time"": ""16.911000"", ""end_time"": ""61.674000"", ""tags"": { ""title"": ""Auckland"" } },
                { ""id"": 2, ""start_time"": ""61.674000"", ""end_time"": ""1729.886000"", ""tags"": { ""title"": ""Chapter 01"" } }
            ]
        }";

        [Fact]
        public void ParsesRealFfprobeOutput_InSeconds_WithTitles()
        {
            var result = FfprobeChapters.Parse(BladeRunnerJson);

            result.Should().HaveCount(3);
            result[0].Name.Should().Be("Opening Credits");
            result[0].Start.Should().Be(0);
            result[1].Name.Should().Be("Auckland");
            result[1].Start.Should().BeApproximately(16.911, 0.001);
            result[2].Start.Should().BeApproximately(61.674, 0.001);
        }

        [Fact]
        public void OrdersByStartAndNamesUntitledChapters()
        {
            const string json = @"{ ""chapters"": [
                { ""start_time"": ""120.0"", ""tags"": {} },
                { ""start_time"": ""10.0"" }
            ] }";

            var result = FfprobeChapters.Parse(json);

            result[0].Start.Should().Be(10);
            result[0].Name.Should().Be("Chapter 1");
            result[1].Start.Should().Be(120);
            result[1].Name.Should().Be("Chapter 2");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("not json")]
        [InlineData("{}")]
        [InlineData(@"{ ""chapters"": [] }")]
        public void ReturnsEmptyForMissingOrBadData(string? json)
        {
            FfprobeChapters.Parse(json).Should().BeEmpty();
        }

        [Fact]
        public void SingleZeroChapter_IsTreatedAsNone()
        {
            const string json = @"{ ""chapters"": [ { ""start_time"": ""0.0"", ""tags"": { ""title"": ""x"" } } ] }";
            FfprobeChapters.Parse(json).Should().BeEmpty();
        }

        [Fact]
        public void AllChaptersAtZero_IsTreatedAsNone()
        {
            const string json = @"{ ""chapters"": [
                { ""start_time"": ""0.0"", ""tags"": { ""title"": ""a"" } },
                { ""start_time"": ""0.0"", ""tags"": { ""title"": ""b"" } }
            ] }";
            FfprobeChapters.Parse(json).Should().BeEmpty();
        }

        [Fact]
        public void SingleRealChapter_IsKept()
        {
            const string json = @"{ ""chapters"": [ { ""start_time"": ""5.0"", ""tags"": { ""title"": ""Only"" } } ] }";
            var result = FfprobeChapters.Parse(json);
            result.Should().ContainSingle();
            result[0].Start.Should().Be(5);
            result[0].Name.Should().Be("Only");
        }
    }
}
