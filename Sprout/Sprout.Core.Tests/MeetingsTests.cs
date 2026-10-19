using Sprout.Core;

namespace Sprout.Core.Tests;

public class MeetingsTests
{
    [Fact]
    public void Fourth_thursday_of_october_2026()
    {
        Assert.Equal(new DateOnly(2026, 10, 22), Meetings.NthWeekday(2026, 10, 4, DayOfWeek.Thursday));
    }

    [Fact]
    public void Last_friday_of_october_2026()
    {
        Assert.Equal(new DateOnly(2026, 10, 30), Meetings.NthWeekday(2026, 10, 5, DayOfWeek.Friday));
    }

    [Fact]
    public void Monthly_next_moves_to_next_month_after_this_ones_date()
    {
        var rule = new MeetingRule { Kind = MeetingRuleKind.Monthly, Nth = 1, Weekday = DayOfWeek.Tuesday };

        Assert.Equal(new DateOnly(2026, 11, 3), Meetings.NextAfter(rule, new DateOnly(2026, 10, 6)));
    }

    [Fact]
    public void Every_two_weeks_keeps_its_rhythm()
    {
        var rule = new MeetingRule { Kind = MeetingRuleKind.EveryWeeks, EveryWeeks = 2, Weekday = DayOfWeek.Wednesday };
        var previous = new DateOnly(2026, 10, 7);

        Assert.Equal(new DateOnly(2026, 10, 21), Meetings.NextAfter(rule, new DateOnly(2026, 10, 15), previous));
    }

    [Fact]
    public void Schedule_text_reads_naturally()
    {
        Assert.Equal("4th Thursday of every month", Meetings.ScheduleText(new MeetingRule { Nth = 4, Weekday = DayOfWeek.Thursday }));
        Assert.Equal("Last Friday of every month", Meetings.ScheduleText(new MeetingRule { Nth = 5, Weekday = DayOfWeek.Friday }));
        Assert.Equal("Every 2 weeks on Monday", Meetings.ScheduleText(new MeetingRule { Kind = MeetingRuleKind.EveryWeeks, EveryWeeks = 2, Weekday = DayOfWeek.Monday }));
    }

    [Fact]
    public void Prep_counts_back_workdays_over_a_weekend()
    {
        var monday = new DateOnly(2026, 10, 19);

        Assert.Equal(new DateOnly(2026, 10, 15), Meetings.WorkdaysBefore(monday, 2, new DateOnly(2026, 10, 1)));
    }

    [Fact]
    public void Rolling_a_past_meeting_moves_notes_to_history_and_makes_new_prep()
    {
        var meeting = new Meeting
        {
            Name = "AIM Meeting",
            Rule = new MeetingRule { Nth = 1, Weekday = DayOfWeek.Tuesday },
            NextDate = new DateOnly(2026, 10, 6),
            Agenda = "Topics",
            Notes = "Went well",
            Prep = [new PrepItem { Title = "Prepare topics", Size = TaskSize.M, WorkdaysBefore = 1 }]
        };
        var leftover = new TaskItem { Status = WorkStatus.Backlog, MeetingId = meeting.Id };

        var result = Meetings.RollPast([meeting], [leftover], new DateOnly(2026, 10, 15));

        var rolled = result.ChangedMeetings[0];
        Assert.Equal(new DateOnly(2026, 11, 3), rolled.NextDate);
        Assert.Equal("Went well", rolled.History[0].Notes);
        Assert.Equal("", rolled.Notes);
        Assert.Equal(WorkStatus.Closed, result.ChangedTasks[0].Status);
        Assert.Equal("AIM Meeting: Prepare topics", result.NewTasks[0].Title);
        Assert.Equal(new DateOnly(2026, 11, 2), result.NewTasks[0].DueDate!.Value);
    }
}

public class MeetingPrepTests
{
    // Wednesday, Oct 14 2026; the meeting is Tuesday, Oct 20.
    private static readonly DateOnly Today = new(2026, 10, 14);

    private static Meeting NewMeeting() => new() { Name = "Quality Review", NextDate = new DateOnly(2026, 10, 20) };

    [Fact]
    public void New_rows_become_tasks_and_only_recurring_ones_stay_on_the_meeting()
    {
        var meeting = NewMeeting();
        var rows = new List<PrepDraft>
        {
            new() { Title = "Pull the numbers", WorkdaysBefore = 2 },
            new() { Title = "Book the room", Recurring = false, WorkdaysBefore = 0 },
            new() { Title = "   " }
        };

        var result = Meetings.ApplyPrep(meeting, rows, [], Today);

        Assert.Single(meeting.Prep);
        Assert.Equal(2, result.NewTasks.Count);
        Assert.Equal("Quality Review: Pull the numbers", result.NewTasks[0].Title);
        Assert.Equal(new DateOnly(2026, 10, 16), result.NewTasks[0].DueDate);
        Assert.Equal(meeting.Prep[0].Id, result.NewTasks[0].PrepItemId);
        Assert.Null(result.NewTasks[1].PrepItemId);
        Assert.Equal(meeting.Id, result.NewTasks[1].MeetingId);
    }

    [Fact]
    public void Removed_rows_close_their_task_and_edited_rows_update_it()
    {
        var meeting = NewMeeting();
        var keep = new PrepItem { Title = "Slides", WorkdaysBefore = 1 };
        var drop = new PrepItem { Title = "Agenda" };
        meeting.Prep = [keep, drop];
        var keepTask = Meetings.MakePrepTask(meeting, keep, Today);
        var dropTask = Meetings.MakePrepTask(meeting, drop, Today);

        var rows = new List<PrepDraft> { new() { Id = keep.Id, Title = "Final slides", Size = TaskSize.M, WorkdaysBefore = 3 } };
        var result = Meetings.ApplyPrep(meeting, rows, [keepTask, dropTask], Today);

        Assert.Empty(result.NewTasks);
        var closed = result.ChangedTasks.Single(t => t.Id == dropTask.Id);
        Assert.Equal(WorkStatus.Closed, closed.Status);
        var updated = result.ChangedTasks.Single(t => t.Id == keepTask.Id);
        Assert.Equal("Quality Review: Final slides", updated.Title);
        Assert.Equal(TaskSize.M, updated.Size);
        Assert.Equal(new DateOnly(2026, 10, 15), updated.DueDate);
    }

    [Fact]
    public void Cycle_keeps_open_prep_and_prep_done_since_the_last_meeting()
    {
        var meeting = NewMeeting();
        meeting.History.Add(new MeetingRecord { Date = new DateOnly(2026, 9, 15) });
        var open = new TaskItem { Title = "Open", MeetingId = meeting.Id, Status = WorkStatus.Backlog };
        var doneNow = new TaskItem { Title = "Done now", MeetingId = meeting.Id, Status = WorkStatus.Done, CompletedAt = new DateTime(2026, 10, 13) };
        var doneBefore = new TaskItem { Title = "Done before", MeetingId = meeting.Id, Status = WorkStatus.Done, CompletedAt = new DateTime(2026, 9, 14) };
        var closed = new TaskItem { Title = "Closed", MeetingId = meeting.Id, Status = WorkStatus.Closed };

        var cycle = Meetings.CycleTasks(meeting, [open, doneNow, doneBefore, closed]);

        Assert.Equal(new[] { "Done now", "Open" }, cycle.Select(t => t.Title).Order().ToArray());
    }

    [Fact]
    public void When_text_counts_days()
    {
        Assert.Equal("Today", Meetings.WhenText(Today, Today));
        Assert.Equal("Tomorrow", Meetings.WhenText(Today.AddDays(1), Today));
        Assert.Equal("In 6 days", Meetings.WhenText(Today.AddDays(6), Today));
    }
}
