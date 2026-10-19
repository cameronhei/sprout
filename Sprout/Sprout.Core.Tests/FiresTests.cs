using Sprout.Core;

namespace Sprout.Core.Tests;

public class FiresTests
{
    private static readonly DateOnly Thursday = new(2026, 10, 15);
    private static readonly DateOnly Friday = new(2026, 10, 16);

    private static TaskItem Fire(DateOnly day, int? minutes, string category = "") => new()
    {
        Status = WorkStatus.Fire,
        FireDate = day,
        Minutes = minutes,
        FireCategory = category
    };

    [Theory]
    [InlineData(30, "30 min")]
    [InlineData(60, "1 h")]
    [InlineData(90, "1.5 h")]
    public void Minutes_read_naturally(int minutes, string expected)
    {
        Assert.Equal(expected, Fires.FormatMinutes(minutes));
    }

    [Fact]
    public void Fires_come_out_of_today()
    {
        var settings = new SproutSettings();

        Assert.Equal(4, Capacity.HoursLeftToday(settings, [Fire(Thursday, 90)], Thursday));
    }

    [Fact]
    public void Fires_come_out_of_the_week()
    {
        var settings = new SproutSettings();

        // Thu 5.5 - 2 + Fri 5.5 - 6 (never below zero) = 3.5
        Assert.Equal(3.5, Capacity.HoursLeftThisWeek(settings, [Fire(Thursday, 120), Fire(Friday, 360)], Thursday));
    }

    [Fact]
    public void Categories_sorted_by_time()
    {
        TaskItem[] fires = [Fire(Thursday, 60, "Production"), Fire(Thursday, 120, "Customer"), Fire(Friday, 30)];

        Assert.Equal("Customer 2 h · Production 1 h · Not typed 30 min", Fires.ByCategory(fires));
    }

    [Fact]
    public void In_week_uses_the_week_start()
    {
        TaskItem[] fires = [Fire(new DateOnly(2026, 10, 11), 60), Fire(Thursday, 60)];

        Assert.Single(Fires.InWeek(fires, Thursday, DayOfWeek.Monday));
        Assert.Equal(2, Fires.InWeek(fires, Thursday, DayOfWeek.Sunday).Count);
    }
}
