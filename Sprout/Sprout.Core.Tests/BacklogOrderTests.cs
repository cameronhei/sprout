using Sprout.Core;

namespace Sprout.Core.Tests;

public class BacklogOrderTests
{
    private static readonly DateOnly Thursday = new(2026, 10, 15);

    private static TaskItem Backlog(string title, ImpactLevel impact) => new()
    {
        Title = title,
        Impact = impact,
        Size = TaskSize.S,
        Status = WorkStatus.Backlog
    };

    [Theory]
    [InlineData(0, 3, 15)]
    [InlineData(1, 3, 3)]
    [InlineData(2, 3, -10)]
    [InlineData(0, 1, 0)]
    public void Boost_runs_from_plus_15_to_minus_10(int position, int count, int expected)
    {
        Assert.Equal(expected, BacklogOrder.Boost(position, count));
    }

    [Fact]
    public void Without_a_saved_order_Sprout_orders_by_score()
    {
        var low = Backlog("low", ImpactLevel.Low);
        var high = Backlog("high", ImpactLevel.High);

        Assert.Equal([high.Id, low.Id], BacklogOrder.Resolve([low, high], null, Thursday));
    }

    [Fact]
    public void New_tasks_slot_in_above_the_first_lower_scored_one()
    {
        var low = Backlog("low", ImpactLevel.Low);
        var medium = Backlog("medium", ImpactLevel.Medium);
        var high = Backlog("high", ImpactLevel.High);

        // Peter put low above high; medium is new.
        var order = BacklogOrder.Resolve([low, medium, high], [low.Id, high.Id], Thursday);

        Assert.Equal([medium.Id, low.Id, high.Id], order);
    }

    [Fact]
    public void Dragging_moves_a_task_to_the_target_position()
    {
        Guid a = Guid.NewGuid(), b = Guid.NewGuid(), c = Guid.NewGuid();

        Assert.Equal([c, a, b], BacklogOrder.Move([a, b, c], c, a));
    }

    [Fact]
    public void Top_of_your_list_beats_a_slightly_higher_score()
    {
        var medium = Backlog("medium", ImpactLevel.Medium);   // 20 + 15 = 35
        var high = Backlog("high", ImpactLevel.High);         // 30 - 10 = 20

        var ranked = Recommender.Rank([medium, high], Thursday, 5.5, [medium.Id, high.Id]);

        Assert.Same(medium, ranked[0]);
    }
}
