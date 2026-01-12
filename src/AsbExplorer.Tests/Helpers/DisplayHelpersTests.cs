using AsbExplorer.Helpers;

namespace AsbExplorer.Tests.Helpers;

public class DisplayHelpersTests
{
    public class TruncateIdTests
    {
        [Fact]
        public void TruncateId_ShortId_ReturnsUnchanged()
        {
            var result = DisplayHelpers.TruncateId("short", 12);
            Assert.Equal("short", result);
        }

        [Fact]
        public void TruncateId_LongId_TruncatesWithEllipsis()
        {
            var result = DisplayHelpers.TruncateId("this-is-a-very-long-id", 12);
            Assert.Equal("this-is-a-ve...", result);
        }

        [Fact]
        public void TruncateId_ExactLength_ReturnsUnchanged()
        {
            var result = DisplayHelpers.TruncateId("exactly12chr", 12);
            Assert.Equal("exactly12chr", result);
        }
    }

    public class FormatRelativeTimeTests
    {
        [Fact]
        public void FormatRelativeTime_JustNow_ReturnsJustNow()
        {
            var time = DateTimeOffset.UtcNow.AddSeconds(-30);
            var result = DisplayHelpers.FormatRelativeTime(time);
            Assert.Equal("just now", result);
        }

        [Fact]
        public void FormatRelativeTime_MinutesAgo_ReturnsMinutes()
        {
            var time = DateTimeOffset.UtcNow.AddMinutes(-5);
            var result = DisplayHelpers.FormatRelativeTime(time);
            Assert.Equal("5m ago", result);
        }

        [Fact]
        public void FormatRelativeTime_HoursAgo_ReturnsHours()
        {
            var time = DateTimeOffset.UtcNow.AddHours(-3);
            var result = DisplayHelpers.FormatRelativeTime(time);
            Assert.Equal("3h ago", result);
        }

        [Fact]
        public void FormatRelativeTime_DaysAgo_ReturnsDays()
        {
            var time = DateTimeOffset.UtcNow.AddDays(-2);
            var result = DisplayHelpers.FormatRelativeTime(time);
            Assert.Equal("2d ago", result);
        }
    }

    public class FormatSizeTests
    {
        [Fact]
        public void FormatSize_Bytes_ReturnsBytes()
        {
            var result = DisplayHelpers.FormatSize(500);
            Assert.Equal("500B", result);
        }

        [Fact]
        public void FormatSize_Kilobytes_ReturnsKB()
        {
            var result = DisplayHelpers.FormatSize(2048);
            Assert.Equal("2.0KB", result);
        }

        [Fact]
        public void FormatSize_Megabytes_ReturnsMB()
        {
            var result = DisplayHelpers.FormatSize(2 * 1024 * 1024);
            Assert.Equal("2.0MB", result);
        }
    }
}
