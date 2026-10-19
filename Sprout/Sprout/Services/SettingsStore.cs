using Sprout.Core;

namespace Sprout.Services;

public class SettingsStore(SproutDb db)
{
    private const string StoreName = "settings";

    public SproutSettings Current { get; private set; } = new();
    public event Action? Changed;

    public async Task LoadAsync()
    {
        Current = await db.GetAsync<SproutSettings>(StoreName, "settings") ?? new SproutSettings();
    }

    public async Task SaveAsync(SproutSettings settings)
    {
        settings.HoursPerDay = Math.Clamp(settings.HoursPerDay, 1, 16);
        settings.Buffer = Math.Clamp(settings.Buffer, 0, 12);

        Current = settings;
        Changed?.Invoke();

        await db.PutAsync(StoreName, settings);
    }
}
