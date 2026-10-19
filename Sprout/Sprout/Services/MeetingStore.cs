using Sprout.Core;

namespace Sprout.Services;

public class MeetingStore(SproutDb db) : RecordStore<Meeting>(db, "meetings")
{
    protected override object KeyOf(Meeting item) => item.Id;

    public Meeting? Find(Guid? id) => id is null ? null : Items.FirstOrDefault(m => m.Id == id);

    public IEnumerable<Meeting> Upcoming => Items.OrderBy(m => m.NextDate);

    // Run at startup: any meeting whose date has passed moves on to its next date.
    public async Task RollPastAsync(TaskStore tasks)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var result = Meetings.RollPast(Items, tasks.Tasks, today);

        if (result.ChangedMeetings.Count == 0)
        {
            return;
        }

        await SaveManyAsync(result.ChangedMeetings);
        await tasks.SaveManyAsync([.. result.ChangedTasks, .. result.NewTasks]);
    }

    // Saves the meeting dialog: applies the prep rows to the tasks, then rolls the meeting on if its date has passed.
    // Returns how many new prep tasks went into the backlog.
    public async Task<int> SaveEditAsync(Meeting meeting, List<PrepDraft> rows, TaskStore tasks)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var prep = Meetings.ApplyPrep(meeting, rows, tasks.Tasks, today);

        await SaveAsync(meeting);

        if (prep.ChangedTasks.Count + prep.NewTasks.Count > 0)
        {
            await tasks.SaveManyAsync([.. prep.ChangedTasks, .. prep.NewTasks]);
        }

        await RollPastAsync(tasks);
        return prep.NewTasks.Count;
    }
}
