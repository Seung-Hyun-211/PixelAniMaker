namespace PixelAniMaker.Core.History;

public interface IUndoableAction
{
    string Name { get; }
    void Undo();
    void Redo();
}

/// <summary>Undo/redo stack with a save point for dirty tracking.</summary>
public sealed class UndoHistory
{
    private readonly List<IUndoableAction> _done = [];
    private readonly Stack<IUndoableAction> _undone = new();
    private int _savePoint;

    public event EventHandler? Changed;

    public bool CanUndo => _done.Count > 0;
    public bool CanRedo => _undone.Count > 0;
    public bool IsDirty => _savePoint != _done.Count;
    public IReadOnlyList<IUndoableAction> Done => _done;

    /// <summary>Records an action that has already been applied.</summary>
    public void Push(IUndoableAction action)
    {
        if (_savePoint > _done.Count)
            _savePoint = -1; // the saved state was in the redo branch and is now unreachable
        _done.Add(action);
        _undone.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Undo()
    {
        if (!CanUndo)
            return;
        var action = _done[^1];
        _done.RemoveAt(_done.Count - 1);
        action.Undo();
        _undone.Push(action);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Redo()
    {
        if (!CanRedo)
            return;
        var action = _undone.Pop();
        action.Redo();
        _done.Add(action);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void MarkSaved()
    {
        _savePoint = _done.Count;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        _done.Clear();
        _undone.Clear();
        _savePoint = 0;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
