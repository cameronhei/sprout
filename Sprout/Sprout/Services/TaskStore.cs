using Sprout.Core;

namespace Sprout.Services;

public class TaskStore(SproutDb db)
{
    private const string StoreName = "tasks";
    private readonly List<TaskItem> _tasks = new();
    private readonly HashSet<Guid> _skipped = new();

    public IReadOnlyList<TaskItem> Tasks => _tasks;
    public event Action? Changed;

    public bool IsSkipped(TaskItem task) => _skipped.Contains(task.Id);

    public TaskItem? Find(Guid id) => _tasks.FirstOrDefault(t => t.Id == id);

    public async Task LoadAsync()
    {
        var saved = await db.GetAllAsync<TaskItem>(StoreName);

        _tasks.Clear();
        _tasks.AddRange(saved.OrderBy(t => t.CreatedAt));
    }

    // A copy of every task, used to undo bigger changes in one step.
    public List<TaskItem> Snapshot() => _tasks.Select(t => t.Clone()).ToList();

    public Task RestoreAsync(List<TaskItem> snapshot) => ReplaceAllAsync(snapshot);

    public async Task<TaskItem?> QuickAddAsync(string title)
    {
        title = title.Trim();

        if (string.IsNullOrEmpty(title))
        {
            return null;
        }

        var task = new TaskItem { Title = title };
        task.Log("Captured from quick add");
        await AddAsync(task);
        return task;
    }

    public async Task<TaskItem?> LogFireAsync(string title)
    {
        title = title.Trim();

        if (string.IsNullOrEmpty(title))
        {
            return null;
        }

        var fire = new TaskItem
        {
            Title = title,
            Status = WorkStatus.Fire,
            FireDate = DateOnly.FromDateTime(DateTime.Now)
        };
        fire.Log("Logged from “I got pulled into something”");
        await AddAsync(fire);
        return fire;
    }

    public async Task AddAsync(TaskItem task)
    {
        _tasks.Add(task);
        Changed?.Invoke();
        await db.PutAsync(StoreName, task);
    }

    public async Task RemoveAsync(Guid id)
    {
        _tasks.RemoveAll(t => t.Id == id);
        Changed?.Invoke();
        await db.DeleteAsync(StoreName, id);
    }

    // Saves the edited copy from the task dialog, logs what changed and applies the review rule.
    public async Task<string> SaveAsync(TaskItem edited, Func<Guid, string?> projectName)
    {
        edited.Title = edited.Title.Trim();

        if (string.IsNullOrEmpty(edited.Title))
        {
            return "Give the task a title.";
        }

        var original = Find(edited.Id);
        var changes = original is null ? [] : TaskChanges.Describe(original, edited, projectName);

        foreach (var line in changes)
        {
            edited.Log(line);
        }

        var message = TaskChanges.ApplyReviewRule(edited, changes.Count > 0);
        await ReplaceAsync(edited);
        return message;
    }

    public async Task<string> SaveFireAsync(TaskItem edited)
    {
        edited.Title = edited.Title.Trim();

        if (string.IsNullOrEmpty(edited.Title))
        {
            return "Say briefly what happened.";
        }

        var original = Find(edited.Id);
        var changes = original is null ? [] : TaskChanges.DescribeFire(original, edited);

        foreach (var line in changes)
        {
            edited.Log(line);
        }

        await ReplaceAsync(edited);

        if (changes.Count == 0)
        {
            return "No changes.";
        }

        var timeChanged = original is not null && (original.Minutes != edited.Minutes || original.FireDate != edited.FireDate);
        return timeChanged ? "Firefight saved. Your capacity is updated." : "Firefight saved.";
    }

    // Marks a task done. If it repeats, the next one is created and returned.
    public async Task<TaskItem?> CompleteAsync(TaskItem task)
    {
        var done = task.Clone();
        done.Status = WorkStatus.Done;
        done.CompletedAt = DateTime.Now;
        done.Log("Marked done");
        await ReplaceAsync(done);

        if (done.Repeat is null)
        {
            return null;
        }

        var next = Repeats.CreateNext(done, DateOnly.FromDateTime(DateTime.Now));
        await AddAsync(next);
        return next;
    }

    // Changes one task and records why, e.g. UpdateAsync(task, t => t.Status = WorkStatus.OnHold, "Put on hold").
    public async Task UpdateAsync(TaskItem task, Action<TaskItem> change, string? activity = null)
    {
        var updated = task.Clone();
        change(updated);

        if (activity is not null)
        {
            updated.Log(activity);
        }

        await ReplaceAsync(updated);
    }

    public async Task<TaskItem?> AddNoteAsync(Guid id, string text)
    {
        var task = Find(id);
        text = text.Trim();

        if (task is null || text.Length == 0)
        {
            return null;
        }

        var updated = task.Clone();
        updated.Notes.Insert(0, new TaskNote { Text = text });
        updated.Log("Added a note");
        await ReplaceAsync(updated);
        return updated;
    }

    public async Task ReplaceAsync(TaskItem updated)
    {
        var index = _tasks.FindIndex(t => t.Id == updated.Id);

        if (index < 0)
        {
            return;
        }

        _tasks[index] = updated;
        Changed?.Invoke();

        await db.PutAsync(StoreName, updated);
    }

    // Adds or replaces several tasks in one database transaction.
    public async Task SaveManyAsync(IReadOnlyCollection<TaskItem> tasks)
    {
        foreach (var task in tasks)
        {
            var index = _tasks.FindIndex(t => t.Id == task.Id);
            if (index < 0)
            {
                _tasks.Add(task);
            }
            else
            {
                _tasks[index] = task;
            }
        }

        Changed?.Invoke();
        await db.PutManyAsync(StoreName, tasks);
    }

    public async Task ReplaceAllAsync(IEnumerable<TaskItem> tasks)
    {
        _tasks.Clear();
        _tasks.AddRange(tasks.OrderBy(t => t.CreatedAt));
        _skipped.Clear();
        Changed?.Invoke();

        await db.ReplaceAllAsync(StoreName, _tasks);
    }

    public void Skip(TaskItem task)
    {
        _skipped.Add(task.Id);
        Changed?.Invoke();
    }
}
