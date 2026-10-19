namespace Sprout.Core;

public static class Meetings
{
    private static readonly string[] Ordinals = ["", "1st", "2nd", "3rd", "4th", "Last"];

    // The nth weekday of a month; nth 5 means the last one.
    public static DateOnly NthWeekday(int year, int month, int nth, DayOfWeek weekday)
    {
        if (nth >= 5)
        {
            var last = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
            return last.AddDays(-(((int)last.DayOfWeek - (int)weekday + 7) % 7));
        }

        var first = new DateOnly(year, month, 1);
        return first.AddDays((((int)weekday - (int)first.DayOfWeek + 7) % 7) + (nth - 1) * 7);
    }

    // First meeting date strictly after `after`. For every-N-weeks rules, `previous` keeps the rhythm.
    public static DateOnly NextAfter(MeetingRule rule, DateOnly after, DateOnly? previous = null)
    {
        if (rule.Kind == MeetingRuleKind.EveryWeeks)
        {
            var step = 7 * Math.Max(1, rule.EveryWeeks);

            if (previous is not null)
            {
                var next = previous.Value.AddDays(step);
                while (next <= after)
                {
                    next = next.AddDays(step);
                }
                return next;
            }

            var day = after.AddDays(1);
            while (day.DayOfWeek != rule.Weekday)
            {
                day = day.AddDays(1);
            }
            return day;
        }

        for (var k = 0; k < 15; k++)
        {
            var month = new DateOnly(after.Year, after.Month, 1).AddMonths(k);
            var candidate = NthWeekday(month.Year, month.Month, rule.Nth, rule.Weekday);
            if (candidate > after)
            {
                return candidate;
            }
        }

        return after.AddDays(30);
    }

    public static string ScheduleText(MeetingRule rule)
    {
        if (rule.Kind == MeetingRuleKind.EveryWeeks)
        {
            return rule.EveryWeeks <= 1
                ? $"Every {rule.Weekday}"
                : $"Every {rule.EveryWeeks} weeks on {rule.Weekday}";
        }

        return $"{Ordinals[Math.Clamp(rule.Nth, 1, 5)]} {rule.Weekday} of every month";
    }

    // Counts back n workdays (Mon-Fri) from the meeting, never before today.
    public static DateOnly WorkdaysBefore(DateOnly meeting, int workdays, DateOnly today)
    {
        var day = meeting;
        var counted = 0;

        while (counted < workdays && day > today)
        {
            day = day.AddDays(-1);
            if (Capacity.IsWorkday(day))
            {
                counted++;
            }
        }

        return day;
    }

    public static TaskItem MakePrepTask(Meeting meeting, PrepItem item, DateOnly today)
    {
        var task = new TaskItem
        {
            Title = $"{meeting.Name}: {item.Title}",
            Size = item.Size,
            DueDate = WorkdaysBefore(meeting.NextDate, item.WorkdaysBefore, today),
            Impact = meeting.Impact,
            Status = WorkStatus.Backlog,
            MeetingId = meeting.Id,
            PrepItemId = item.Id
        };
        task.Log($"Created automatically for the {meeting.NextDate:MMM d} {meeting.Name}");
        return task;
    }

    public record RollResult(List<Meeting> ChangedMeetings, List<TaskItem> ChangedTasks, List<TaskItem> NewTasks);

    // After a meeting date passes: its agenda and notes move to history, the next date is worked out,
    // leftover prep is closed and fresh prep tasks are created for the next one.
    public static RollResult RollPast(IEnumerable<Meeting> meetings, IEnumerable<TaskItem> tasks, DateOnly today)
    {
        var changedMeetings = new List<Meeting>();
        var changedTasks = new List<TaskItem>();
        var newTasks = new List<TaskItem>();
        var taskList = tasks.ToList();

        foreach (var original in meetings)
        {
            if (original.NextDate >= today)
            {
                continue;
            }

            var meeting = original.Clone();

            while (meeting.NextDate < today)
            {
                meeting.History.Add(new MeetingRecord { Date = meeting.NextDate, Agenda = meeting.Agenda, Notes = meeting.Notes });
                meeting.Agenda = "";
                meeting.Notes = "";
                meeting.NextDate = NextAfter(meeting.Rule, meeting.NextDate,
                    meeting.Rule.Kind == MeetingRuleKind.EveryWeeks ? meeting.NextDate : null);
            }

            foreach (var task in taskList.Where(t => t.MeetingId == meeting.Id && t.IsOpen))
            {
                var closed = task.Clone();
                closed.Status = WorkStatus.Closed;
                closed.Log("Closed: the meeting date passed");
                changedTasks.Add(closed);
            }

            newTasks.AddRange(meeting.Prep.Select(p => MakePrepTask(meeting, p, today)));
            changedMeetings.Add(meeting);
        }

        return new RollResult(changedMeetings, changedTasks, newTasks);
    }

    public static string DueShort(DateOnly date, DateOnly today)
    {
        var days = date.DayNumber - today.DayNumber;
        return days switch
        {
            < 0 => "overdue",
            0 => "today",
            1 => "tomorrow",
            < 7 => date.DayOfWeek.ToString(),
            _ => date.ToString("MMM d")
        };
    }

    public static DateOnly? LastHeld(Meeting meeting) =>
        meeting.History.Count == 0 ? null : meeting.History.Max(h => h.Date);

    // This meeting's prep: everything linked to it that isn't closed, plus anything finished since it last met.
    public static List<TaskItem> CycleTasks(Meeting meeting, IEnumerable<TaskItem> tasks)
    {
        var last = LastHeld(meeting);

        return tasks
            .Where(t => t.MeetingId == meeting.Id && t.Status != WorkStatus.Closed)
            .Where(t => t.Status != WorkStatus.Done
                || last is null
                || (t.CompletedAt is not null && DateOnly.FromDateTime(t.CompletedAt.Value) > last))
            .OrderBy(t => t.DueDate ?? DateOnly.MaxValue)
            .ToList();
    }

    public static string WhenText(DateOnly date, DateOnly today)
    {
        var days = date.DayNumber - today.DayNumber;
        return days switch
        {
            < 0 => "Was " + date.ToString("MMM d"),
            0 => "Today",
            1 => "Tomorrow",
            _ => $"In {days} days"
        };
    }

    // Strips the "Meeting name: " prefix that prep task titles start with.
    public static string PrepTitle(Meeting meeting, TaskItem task)
    {
        var prefix = meeting.Name + ": ";
        return task.Title.StartsWith(prefix) ? task.Title[prefix.Length..] : task.Title;
    }

    public record PrepResult(List<TaskItem> ChangedTasks, List<TaskItem> NewTasks);

    // Applies the prep rows from the meeting dialog. The meeting keeps the recurring rows as its template;
    // open prep tasks follow their row (or close if the row was removed) and new rows become tasks right away.
    public static PrepResult ApplyPrep(Meeting meeting, IEnumerable<PrepDraft> rows, IEnumerable<TaskItem> tasks, DateOnly today)
    {
        var kept = rows.Where(r => !string.IsNullOrWhiteSpace(r.Title)).ToList();

        foreach (var row in kept)
        {
            row.Title = row.Title.Trim();
        }

        meeting.Prep = kept
            .Where(r => r.Recurring)
            .Select(r => new PrepItem { Id = r.Id ?? Guid.NewGuid(), Title = r.Title, Size = r.Size, WorkdaysBefore = r.WorkdaysBefore })
            .ToList();

        // New recurring rows got their id above; match the template back so their tasks link to it.
        var newRows = kept.Where(r => r.Id is null).ToList();
        var byExisting = kept.Where(r => r.Id is not null).ToDictionary(r => r.Id!.Value);

        var changed = new List<TaskItem>();

        foreach (var task in tasks.Where(t => t.MeetingId == meeting.Id && t.PrepItemId is not null && t.IsOpen))
        {
            var copy = task.Clone();

            if (!byExisting.TryGetValue(task.PrepItemId!.Value, out var row))
            {
                copy.Status = WorkStatus.Closed;
                copy.Log("Removed from the meeting’s prep");
                changed.Add(copy);
                continue;
            }

            copy.Title = $"{meeting.Name}: {row.Title}";
            copy.Size = row.Size;
            copy.DueDate = WorkdaysBefore(meeting.NextDate, row.WorkdaysBefore, today);
            copy.PrepItemId = row.Recurring ? task.PrepItemId : null;

            if (copy.Title != task.Title || copy.Size != task.Size || copy.DueDate != task.DueDate || copy.PrepItemId != task.PrepItemId)
            {
                copy.Log("Updated from the meeting’s prep");
                changed.Add(copy);
            }
        }

        var created = new List<TaskItem>();
        var templateIndex = 0;

        foreach (var row in kept)
        {
            PrepItem item;

            if (row.Recurring)
            {
                item = meeting.Prep[templateIndex++];
            }
            else
            {
                item = new PrepItem { Title = row.Title, Size = row.Size, WorkdaysBefore = row.WorkdaysBefore };
            }

            if (!newRows.Contains(row))
            {
                continue;
            }

            var task = MakePrepTask(meeting, item, today);
            if (!row.Recurring)
            {
                task.PrepItemId = null;
            }
            created.Add(task);
        }

        return new PrepResult(changed, created);
    }
}
