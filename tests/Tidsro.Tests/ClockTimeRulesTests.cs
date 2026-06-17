using Tidsro.Models;
using Xunit;

namespace Tidsro.Tests;

public class ClockTimeRulesTests
{
    [Theory]
    [InlineData("09:00", 9, 0)]
    [InlineData("9:00", 9, 0)]      // single-digit hour ok
    [InlineData("00:00", 0, 0)]
    [InlineData("23:59", 23, 59)]
    [InlineData(" 7:5 ", 7, 5)]     // trimmed; minutes "5" == 05
    // bare-digit fast input
    [InlineData("9", 9, 0)]         // 1-digit → hour only, minute = 0
    [InlineData("11", 11, 0)]       // 2-digit → hour only, minute = 0
    [InlineData("14", 14, 0)]       // 2-digit → 14:00
    [InlineData("930", 9, 30)]      // 3-digit → h=9, mm=30
    [InlineData("115", 1, 15)]      // 3-digit → h=1, mm=15
    [InlineData("1100", 11, 0)]     // 4-digit → hh=11, mm=00
    [InlineData("1430", 14, 30)]    // 4-digit → 14:30
    [InlineData("0930", 9, 30)]     // 4-digit leading zero → 09:30
    [InlineData(" 11 ", 11, 0)]     // whitespace trimmed before bare-digit parse
    public void TryParse_accepts_valid_times(string input, int hour, int minute)
    {
        Assert.True(ClockTimeRules.TryParse(input, out var h, out var m, out var err));
        Assert.Null(err);
        Assert.Equal(hour, h);
        Assert.Equal(minute, m);
    }

    [Theory]
    [InlineData("")]         // empty
    [InlineData("   ")]      // blank
    [InlineData("24:00")]    // hour out of range
    [InlineData("12:60")]    // minute out of range
    [InlineData("-1:00")]    // negative
    [InlineData("9:00:00")]  // too many parts
    [InlineData("abc")]      // non-digit bare input
    [InlineData("9:ab")]     // junk minutes
    // bare-digit out-of-range / too long
    [InlineData("2500")]     // hour 25 out of range
    [InlineData("1170")]     // minute 70 out of range
    [InlineData("99")]       // hour 99 out of range
    [InlineData("12345")]    // 5 digits → too long
    public void TryParse_rejects_invalid_times(string input)
    {
        Assert.False(ClockTimeRules.TryParse(input, out _, out _, out var err));
        Assert.NotNull(err);
    }

    [Fact]
    public void ComputeFireAt_uses_today_when_the_time_is_still_ahead()
    {
        var now = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);
        var fire = ClockTimeRules.ComputeFireAt(now, 10, 30);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 10, 30, 0, TimeSpan.Zero), fire);
    }

    [Fact]
    public void ComputeFireAt_rolls_to_tomorrow_when_the_time_has_passed()
    {
        var now = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);
        var fire = ClockTimeRules.ComputeFireAt(now, 8, 0);
        Assert.Equal(new DateTimeOffset(2026, 1, 2, 8, 0, 0, TimeSpan.Zero), fire);
    }

    [Fact]
    public void ComputeFireAt_rolls_to_tomorrow_when_the_time_equals_now()
    {
        var now = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);
        var fire = ClockTimeRules.ComputeFireAt(now, 9, 0);     // "now" is ambiguous → tomorrow
        Assert.Equal(new DateTimeOffset(2026, 1, 2, 9, 0, 0, TimeSpan.Zero), fire);
    }
}
