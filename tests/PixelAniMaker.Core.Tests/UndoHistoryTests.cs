using PixelAniMaker.Core.History;

namespace PixelAniMaker.Core.Tests;

public class UndoHistoryTests
{
    private sealed class Append(List<string> log, string name) : IUndoableAction
    {
        public string Name => name;
        public void Undo() => log.Remove(name);
        public void Redo() => log.Add(name);
    }

    private static (UndoHistory History, List<string> Log) ThreeSteps()
    {
        var history = new UndoHistory();
        var log = new List<string>();
        foreach (var name in new[] { "a", "b", "c" })
        {
            var action = new Append(log, name);
            action.Redo();
            history.Push(action);
        }
        return (history, log);
    }

    [Fact]
    public void Moving_to_a_point_undoes_or_redoes_every_step_in_between_with_one_event()
    {
        var (history, log) = ThreeSteps();
        int events = 0;
        history.Changed += (_, _) => events++;

        history.MoveTo(1);
        Assert.Equal(["a"], log);
        Assert.Equal(["b", "c"], history.Undone.Select(a => a.Name));   // next redo first
        Assert.Equal(1, events);

        history.MoveTo(3);
        Assert.Equal(["a", "b", "c"], log);
        history.MoveTo(0);
        Assert.Empty(log);
        history.MoveTo(99);                                                // clamped
        Assert.Equal(3, history.Done.Count);
        history.MoveTo(3);                                                 // already there: no event
        Assert.Equal(4, events);
    }

    [Fact]
    public void The_save_point_follows_saves_and_is_lost_when_its_branch_is_replaced()
    {
        var (history, log) = ThreeSteps();
        history.MarkSaved();
        Assert.Equal(3, history.SavePoint);
        history.MoveTo(2);
        Assert.True(history.IsDirty);
        history.MoveTo(3);
        Assert.False(history.IsDirty);

        history.MoveTo(1);
        var other = new Append(log, "d");
        other.Redo();
        history.Push(other);
        Assert.Equal(-1, history.SavePoint);
        Assert.Empty(history.Undone);
    }
}
