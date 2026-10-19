using Sprout.Core;

namespace Sprout.Services;

public class ProjectStore(SproutDb db) : RecordStore<Project>(db, "projects")
{
    protected override object KeyOf(Project item) => item.Id;

    public Project? Find(Guid? id) => id is null ? null : Items.FirstOrDefault(p => p.Id == id);

    public string? NameOf(Guid id) => Find(id)?.Name;

    public IEnumerable<Project> Active => Items.Where(p => p.Status != ProjectStatus.Done).OrderBy(p => p.CreatedAt);
}
