namespace PixelAniMaker.Core.History;

public interface IUndoableAction
{
    string Name { get; }
    void Undo();
    void Redo();
}

/// <summary>Several actions undone and redone as one step.</summary>
public sealed class CompositeAction(string name, IReadOnlyList<IUndoableAction> actions) : IUndoableAction
{
    public string Name => name;

    public void Undo()
    {
        for (int i = actions.Count - 1; i >= 0; i--)
            actions[i].Undo();
    }

    public void Redo()
    {
        foreach (var a in actions)
            a.Redo();
    }
}

/// <summary>Undo/redo stack with a save point for dirty tracking.</summary>
public sealed class UndoHistory
{
    private readonly List<IUndoableAction> _done = [];
    private readonly Stack<IUndoableAction> _undone = new();
    private int _savePoint;
    private List<IUndoableAction>? _group;
    private string _groupName = "";

    /// <summary>
    /// Collects every action recorded until <see cref="EndGroup"/> into one undo step (e.g. a stroke
    /// that also resizes the part image). <see cref="Changed"/> is still raised for each of them.
    /// </summary>
    public void BeginGroup(string name)
    {
        EndGroup();
        _group = [];
        _groupName = name;
    }

    /// <summary>Actions recorded in the open group so far (0 when none is open).</summary>
    public int GroupCount => _group?.Count ?? 0;

    public void EndGroup()
    {
        var group = _group;
        _group = null;
        if (group is null || group.Count == 0)
            return;
        Record(group.Count == 1 ? group[0] : new CompositeAction(_groupName, group));
    }

    /// <summary>Undoes everything recorded in the open group and forgets it.</summary>
    public void CancelGroup()
    {
        var group = _group;
        _group = null;
        if (group is null)
            return;
        for (int i = group.Count - 1; i >= 0; i--)
            group[i].Undo();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? Changed;

    public bool CanUndo => _done.Count > 0;
    public bool CanRedo => _undone.Count > 0;
    public bool IsDirty => _savePoint != _done.Count;
    public IReadOnlyList<IUndoableAction> Done => _done;

    /// <summary>Actions redo would apply again, the next one first.</summary>
    public IReadOnlyList<IUndoableAction> Undone => [.. _undone];

    /// <summary>How many actions were applied when the document was last saved (-1: that state is gone).</summary>
    public int SavePoint => _savePoint;

    /// <summary>
    /// Undoes or redoes until <paramref name="doneCount"/> actions are applied (clamped to what exists),
    /// raising <see cref="Changed"/> once.
    /// </summary>
    public void MoveTo(int doneCount)
    {
        EndGroup();
        doneCount = Math.Clamp(doneCount, 0, _done.Count + _undone.Count);
        if (doneCount == _done.Count)
            return;
        while (_done.Count > doneCount)
        {
            var action = _done[^1];
            _done.RemoveAt(_done.Count - 1);
            action.Undo();
            _undone.Push(action);
        }
        while (_done.Count < doneCount)
        {
            var action = _undone.Pop();
            action.Redo();
            _done.Add(action);
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Applies <paramref name="action"/> and records it.</summary>
    public void Do(IUndoableAction action)
    {
        action.Redo();
        Push(action);
    }

    /// <summary>Records an action that has already been applied.</summary>
    public void Push(IUndoableAction action)
    {
        if (_group is not null)
        {
            _group.Add(action);
            Changed?.Invoke(this, EventArgs.Empty);
            return;
        }
        Record(action);
    }

    private void Record(IUndoableAction action)
    {
        if (_savePoint > _done.Count)
            _savePoint = -1; // the saved state was in the redo branch and is now unreachable
        _done.Add(action);
        _undone.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Undo()
    {
        EndGroup();
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
        EndGroup();
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
        EndGroup();
        _done.Clear();
        _undone.Clear();
        _savePoint = 0;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
