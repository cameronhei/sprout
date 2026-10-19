namespace Sprout.Core;

public static class BacklogOrder
{
    // Peter's saved order, kept for tasks that still exist. Tasks he hasn't placed yet
    // slot in just above the first task Sprout scores lower (the mockup's rule).
    public static List<Guid> Resolve(IEnumerable<TaskItem> backlog, IReadOnlyList<Guid>? saved, DateOnly today)
    {
        var tasks = backlog.ToList();
        var byId = tasks.ToDictionary(t => t.Id);
        var byScore = tasks
            .OrderByDescending(t => Recommender.BaseScore(t, today))
            .ThenBy(t => t.CreatedAt)
            .ToList();

        if (saved is null || saved.Count == 0)
        {
            return byScore.Select(t => t.Id).ToList();
        }

        var order = saved.Where(byId.ContainsKey).Distinct().ToList();

        foreach (var task in byScore.Where(t => !order.Contains(t.Id)))
        {
            var score = Recommender.BaseScore(task, today);
            var at = order.FindIndex(id => Recommender.BaseScore(byId[id], today) < score);

            if (at < 0)
            {
                order.Add(task.Id);
            }
            else
            {
                order.Insert(at, task.Id);
            }
        }

        return order;
    }

    // +15 at the top of the list down to -10 at the bottom.
    public static int Boost(int position, int count) =>
        count < 2 ? 0 : (int)Math.Round(15 - position * 25.0 / (count - 1), MidpointRounding.AwayFromZero);

    public static List<Guid> Move(IReadOnlyList<Guid> order, Guid dragged, Guid target)
    {
        var list = order.ToList();
        var from = list.IndexOf(dragged);
        var to = list.IndexOf(target);

        if (from < 0 || to < 0 || from == to)
        {
            return list;
        }

        list.RemoveAt(from);
        list.Insert(to, dragged);
        return list;
    }
}
