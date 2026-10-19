namespace Sprout.Services;

public class ToastService
{
    private Func<Task>? _undo;

    public string? Message { get; private set; }
    public bool CanUndo => _undo is not null;
    public event Action? Changed;

    public void Show(string message, Func<Task>? undo = null)
    {
        Message = message;
        _undo = undo;
        Changed?.Invoke();
    }

    public void Clear()
    {
        Message = null;
        _undo = null;
        Changed?.Invoke();
    }

    public async Task UndoAsync()
    {
        var undo = _undo;

        if (undo is null)
        {
            return;
        }

        _undo = null;
        Message = "Undone.";
        Changed?.Invoke();

        await undo();
    }
}