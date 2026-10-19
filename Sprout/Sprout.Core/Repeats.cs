namespace Sprout.Core;

public static class Repeats
{
    private static readonly DayOfWeek[] WeekOrder =
    [
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
        DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
    ];

    public static string ShortDay(DayOfWeek day) => day.ToString()[..3];

    public static string Label(RepeatRule? rule)
    {
        if (rule is null)
        {
            return "";
        }

        var every = Math.Max(1, rule.Every);
        var text = every == 1
            ? "Repeats " + rule.Unit switch
            {
                RepeatUnit.Day => "daily",
                RepeatUnit.Week => "weekly",
                _ => "monthly"
            }
            : $"Repeats every {every} {rule.Unit.ToString().ToLower()}s";

        if (rule.Unit == RepeatUnit.Week && rule.Days.Count > 0)
        {
            text += " on " + string.Join(", ", WeekOrder.Where(rule.Days.Contains).Select(ShortDay));
        }

        return text;
    }

    // The next one is due one interval after this one's due date (or after today, if it was overdue or undated).
    public static DateOnly NextDue(RepeatRule rule, DateOnly? due, DateOnly today)
    {
        var from = due is null || due.Value < today ? today : due.Value;
        var every = Math.Max(1, rule.Every);

        switch (rule.Unit)
        {
            case RepeatUnit.Day:
                return from.AddDays(every);

            case RepeatUnit.Month:
                // DateOnly.AddMonths keeps the end of the month: Oct 31 + 1 month = Nov 30.
                return from.AddMonths(every);

            default:
                if (rule.Days.Count == 0)
                {
                    return from.AddDays(7 * every);
                }

                for (var i = 1; i <= 7; i++)
                {
                    var candidate = from.AddDays(i);
                    if (!rule.Days.Contains(candidate.DayOfWeek))
                    {
                        continue;
                    }

                    // Crossing into a new week skips the in-between weeks for "every 2 weeks" and up.
                    var crossedWeek = Array.IndexOf(WeekOrder, candidate.DayOfWeek) <= Array.IndexOf(WeekOrder, from.DayOfWeek);
                    return crossedWeek ? candidate.AddDays(7 * (every - 1)) : candidate;
                }

                return from.AddDays(7 * every);
        }
    }

    public static TaskItem CreateNext(TaskItem finished, DateOnly today)
    {
        var next = finished.Clone();
        next.Id = Guid.NewGuid();
        next.Status = finished.Size is null ? WorkStatus.NeedsReview : WorkStatus.Backlog;
        next.DueDate = NextDue(finished.Repeat!, finished.DueDate, today);
        next.CompletedAt = null;
        next.CreatedAt = DateTime.Now;
        next.Notes = [];
        next.Activity = [];
        next.Log("Created automatically from a repeating task");
        return next;
    }
}
