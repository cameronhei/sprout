using Sprout.Core;

namespace Sprout.Services;

public class ReportStore(SproutDb db) : RecordStore<ReportWeek>(db, "reports")
{
    protected override object KeyOf(ReportWeek item) => item.Id;

    // This week's choices, or a fresh set if Peter hasn't touched this week yet.
    public ReportWeek For(DateOnly weekStart)
    {
        var key = ReportWeek.KeyFor(weekStart);
        return Items.FirstOrDefault(r => r.Id == key) ?? new ReportWeek { Id = key };
    }
}
