using Sprout.Core;

namespace Sprout.Core.Tests;

public class TaskChangesTests
{
    [Fact]
    public void Describes_each_change()
    {
        var before = new TaskItem { Title = "Old" };
        var after = before.Clone();
        after.Title = "New";
        after.Size = TaskSize.M;
        after.Impact = ImpactLevel.High;
        after.IsFirm = true;
        after.IsBlocked = true;
        after.BlockedBy = "HR";

        var lines = TaskChanges.Describe(before, after, _ => null);

        Assert.Equal(
            ["Renamed to “New”", "Size set to medium", "Impact set to high", "Marked blocked: waiting on HR", "Marked as a firm deadline"],
            lines);
    }

    [Fact]
    public void Sized_review_task_moves_to_backlog()
    {
        var task = new TaskItem { Status = WorkStatus.NeedsReview, Size = TaskSize.S };

        var message = TaskChanges.ApplyReviewRule(task, anyChanges: true);

        Assert.Equal(WorkStatus.Backlog, task.Status);
        Assert.Equal("Moved to your backlog. It can be recommended now.", message);
    }

    [Fact]
    public void Unsized_backlog_task_moves_back_to_review()
    {
        var task = new TaskItem { Status = WorkStatus.Backlog };

        TaskChanges.ApplyReviewRule(task, anyChanges: true);

        Assert.Equal(WorkStatus.NeedsReview, task.Status);
    }

    [Fact]
    public void No_changes_says_so()
    {
        var task = new TaskItem { Status = WorkStatus.Backlog, Size = TaskSize.S };

        Assert.Equal("No changes.", TaskChanges.ApplyReviewRule(task, anyChanges: false));
    }

    [Fact]
    public void Clone_copies_lists()
    {
        var task = new TaskItem();
        var copy = task.Clone();
        copy.Log("Only on the copy");

        Assert.Empty(task.Activity);
    }
}
