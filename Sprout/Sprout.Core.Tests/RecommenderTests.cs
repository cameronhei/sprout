namespace Sprout.Core.Tests;

public class RecommenderTests
{
    private static readonly DateOnly Today = new(2026, 10, 15);
    private const double FullDay = 5.5;

    private static TaskItem Task(
        string title = "Task",
        TaskSize? size = null,
        ImpactLevel? impact = null,
        int? dueInDays = null,
        bool firm = false,
        WorkStatus status = WorkStatus.Backlog)
    {
        return new TaskItem
        {
            Title = title,
            Size = size,
            Impact = impact,
            DueDate = dueInDays is null ? null : Today.AddDays(dueInDays.Value),
            IsFirm = firm,
            Status = status
        };
    }

    [Fact]
    public void Plain_task_scores_as_medium_impact()
    {
        Assert.Equal(20, Recommender.BaseScore(Task(), Today));
    }

    [Fact]
    public void High_impact_scores_thirty()
    {
        Assert.Equal(30, Recommender.BaseScore(Task(impact: ImpactLevel.High), Today));
    }

    [Theory]
    [InlineData(0, 30)]     // due today: 20 + 10
    [InlineData(2, 26)]     // in 2 days: 20 + 6
    [InlineData(5, 20)]     // in 5 days: no urgency
    [InlineData(9, 20)]     // far away: never negative
    [InlineData(-1, 32)]    // overdue: 20 + 12
    public void Urgency_grows_as_due_date_gets_close(int dueInDays, int expected)
    {
        Assert.Equal(expected, Recommender.BaseScore(Task(dueInDays: dueInDays), Today));
    }

    [Fact]
    public void Firm_bonus_applies_within_three_days()
    {
        Assert.Equal(30, Recommender.BaseScore(Task(dueInDays: 3, firm: true), Today)); // 20 + 4 + 6
        Assert.Equal(22, Recommender.BaseScore(Task(dueInDays: 4, firm: true), Today)); // 20 + 2, no bonus
    }

    [Fact]
    public void Task_too_big_for_today_is_penalised()
    {
        Assert.Equal(-8, Recommender.FitPenalty(Task(size: TaskSize.XL), FullDay));
        Assert.Equal(0, Recommender.FitPenalty(Task(size: TaskSize.M), FullDay));
    }

    [Fact]
    public void No_penalty_when_the_day_is_already_used_up()
    {
        Assert.Equal(0, Recommender.FitPenalty(Task(size: TaskSize.XL), 0));
    }

    [Fact]
    public void Firm_task_due_tomorrow_beats_high_impact_with_no_date()
    {
        var bigButUndated = Task("Big", impact: ImpactLevel.High);   // 30
        var firmTomorrow = Task("Firm", dueInDays: 1, firm: true);   // 20 + 8 + 6 = 34

        var next = Recommender.Rank([bigButUndated, firmTomorrow], Today, FullDay)[0];

        Assert.Same(firmTomorrow, next);
    }

    [Fact]
    public void Done_tasks_are_never_recommended()
    {
        var done = Task(impact: ImpactLevel.High, status: WorkStatus.Done);

        Assert.Empty(Recommender.Rank([done], Today, FullDay));
    }

    [Fact]
    public void Unsized_quick_adds_can_be_recommended()
    {
        var quickAdd = Task(status: WorkStatus.NeedsReview);

        Assert.Single(Recommender.Rank([quickAdd], Today, FullDay));
    }

    [Fact]
    public void Why_mentions_firm_deadline_and_due_date()
    {
        var task = Task(dueInDays: 1, firm: true);
        Assert.Equal("It has a firm deadline and it's due tomorrow.", Recommender.Why(task, Today, FullDay));
    }

    [Fact]
    public void Why_mentions_a_small_window()
    {
        var task = Task(size: TaskSize.XS);
        Assert.Equal("It fits the 2 h you have left today.", Recommender.Why(task, Today, 2));
    }

    [Fact]
    public void Why_has_a_fallback()
    {
        Assert.Equal("It's the best fit for the time you have.", Recommender.Why(Task(), Today, FullDay));
    }
}