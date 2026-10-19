namespace Sprout.Core;

public static class Recommender
{
    public const TaskSize DefaultSize = TaskSize.S;
    public const ImpactLevel DefaultImpact = ImpactLevel.Medium;

    public static double EstimatedHours(TaskItem task) => (task.Size ?? DefaultSize).Hours();

    public static int? DaysUntilDue(TaskItem task, DateOnly today)
    {
        if (task.DueDate is null)
        {
            return null;
        }

        return task.DueDate.Value.DayNumber - today.DayNumber;
    }

    public static int BaseScore(TaskItem task, DateOnly today)
    {
        var impact = (int)(task.Impact ?? DefaultImpact) * 10;

        var days = DaysUntilDue(task, today);
        var urgency = days is null ? 0 : Math.Max(0, 10 - 2 * days.Value);

        var firmBonus = task.IsFirm && days is not null && days.Value <= 3 ? 6 : 0;

        return impact + urgency + firmBonus;
    }

    public static int FitPenalty(TaskItem task, double hoursLeftToday)
    {
        if (hoursLeftToday > 0 && EstimatedHours(task) > hoursLeftToday)
        {
            return -8;
        }

        return 0;
    }

    public static string Why(TaskItem task, DateOnly today, double hoursLeftToday, bool topOfYourList = false)
    {
        var reasons = new List<string>();
        var days = DaysUntilDue(task, today);
        var dueSoon = days is not null && days.Value <= 3;

        if (task.IsFirm && dueSoon)
        {
            reasons.Add("it has a firm deadline");
        }

        if (topOfYourList)
        {
            reasons.Add("it's at the top of your list");
        }

        if (dueSoon)
        {
            var due = TaskLabels.Due(task, today);
            reasons.Add("it's " + char.ToLower(due[0]) + due[1..]);
        }

        if (task.Impact == ImpactLevel.High)
        {
            reasons.Add("it's high impact");
        }

        if (hoursLeftToday > 0 && hoursLeftToday < 3 && EstimatedHours(task) <= hoursLeftToday)
        {
            reasons.Add($"it fits the {hoursLeftToday} h you have left today");
        }

        if (reasons.Count == 0)
        {
            reasons.Add("it's the best fit for the time you have");
        }

        var sentence = reasons.Count == 1 ? reasons[0] : string.Join(", ", reasons.Take(reasons.Count - 1)) + " and " + reasons[^1];

        return char.ToUpper(sentence[0]) + sentence[1..] + ".";
    }

    public static int Score(TaskItem task, DateOnly today, double hoursLeftToday, int orderBoost = 0) =>
        BaseScore(task, today) + orderBoost + FitPenalty(task, hoursLeftToday);

    // Boost for each backlog task from Peter's drag order (+15 at the top to -10 at the bottom).
    public static Dictionary<Guid, int> OrderBoosts(IEnumerable<TaskItem> tasks, DateOnly today, IReadOnlyList<Guid>? savedOrder)
    {
        var order = BacklogOrder.Resolve(tasks.Where(t => t.Status == WorkStatus.Backlog), savedOrder, today);
        return order.Select((id, position) => (id, boost: BacklogOrder.Boost(position, order.Count)))
                    .ToDictionary(x => x.id, x => x.boost);
    }

    // Every open task (backlog and needs review), best first. Blocked tasks are included here;
    // callers skip them when picking what to do next.
    public static List<TaskItem> Rank(IEnumerable<TaskItem> tasks, DateOnly today, double hoursLeftToday, IReadOnlyList<Guid>? savedOrder = null)
    {
        var list = tasks.ToList();
        var boosts = OrderBoosts(list, today, savedOrder);

        return list
            .Where(t => t.IsOpen)
            .OrderByDescending(t => Score(t, today, hoursLeftToday, boosts.GetValueOrDefault(t.Id)))
            .ThenBy(t => t.DueDate ?? DateOnly.MaxValue)
            .ThenBy(t => t.CreatedAt)
            .ToList();
    }
}