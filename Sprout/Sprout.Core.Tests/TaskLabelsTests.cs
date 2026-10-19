using Microsoft.VisualBasic;
using Sprout.Core;

namespace Sprout.Core.Tests;

public class TaskLabelsTests
{
    private static readonly DateOnly Thursday = new(2026, 10, 15);

    [Theory]
    [InlineData(null, "No due date")]
    [InlineData(-2, "Overdue")]
    [InlineData(0, "Due today")]
    [InlineData(1, "Due tomorrow")]
    [InlineData(4, "Due Monday")]
    [InlineData(10, "Due Oct 25")]
    public void Due_label_reads_naturally(int? dueInDays, string expected)
    {
        var task = new TaskItem
        {
            DueDate = dueInDays is null ? null : Thursday.AddDays(dueInDays.Value)
        };

        Assert.Equal(expected, TaskLabels.Due(task, Thursday));

    }

    [Fact]
    public void Size_label_shows_hours()
    {
        Assert.Equal("M · 2.5 h est.", TaskLabels.Size(new TaskItem { Size = TaskSize.M }));
        Assert.Equal("Size not set", TaskLabels.Size(new TaskItem()));
    }
}

