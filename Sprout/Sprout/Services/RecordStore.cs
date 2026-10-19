namespace Sprout.Services;

// Shared load/save/remove logic for the simple stores (projects, meetings, report weeks).
public abstract class RecordStore<T>(SproutDb db, string storeName) where T : class
{
    protected readonly List<T> Items = new();

    public IReadOnlyList<T> All => Items;
    public event Action? Changed;

    protected abstract object KeyOf(T item);

    protected void Notify() => Changed?.Invoke();

    public virtual async Task LoadAsync()
    {
        Items.Clear();
        Items.AddRange(await db.GetAllAsync<T>(storeName));
    }

    public async Task SaveAsync(T item)
    {
        var index = Items.FindIndex(x => KeyOf(x).Equals(KeyOf(item)));

        if (index < 0)
        {
            Items.Add(item);
        }
        else
        {
            Items[index] = item;
        }

        Notify();
        await db.PutAsync(storeName, item);
    }

    public async Task SaveManyAsync(IReadOnlyCollection<T> items)
    {
        foreach (var item in items)
        {
            var index = Items.FindIndex(x => KeyOf(x).Equals(KeyOf(item)));
            if (index < 0)
            {
                Items.Add(item);
            }
            else
            {
                Items[index] = item;
            }
        }

        Notify();
        await db.PutManyAsync(storeName, items);
    }

    public async Task RemoveAsync(T item)
    {
        var key = KeyOf(item);
        Items.RemoveAll(x => KeyOf(x).Equals(key));
        Notify();
        await db.DeleteAsync(storeName, key);
    }

    public async Task ReplaceAllAsync(IEnumerable<T> items)
    {
        Items.Clear();
        Items.AddRange(items);
        Notify();
        await db.ReplaceAllAsync(storeName, Items);
    }
}
