using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Dock.Model.Mvvm.Controls;
using PixelAniMaker.App.Services;

namespace PixelAniMaker.App.ViewModels;

/// <summary>One state in the history list: <see cref="DoneCount"/> actions applied (0 = as opened).</summary>
public sealed record HistoryItem(int DoneCount, string Label, bool Applied, bool IsSaved)
{
    /// <summary>States after the current one (reachable with redo) are shown dimmed.</summary>
    public double Opacity => Applied ? 1 : 0.45;
}

/// <summary>The undo history as a list: picking a row undoes or redoes to that state.</summary>
public sealed partial class HistoryViewModel : Tool
{
    private bool _syncing;

    [ObservableProperty] private HistoryItem? _selected;

    public HistoryViewModel(EditorSession session)
    {
        Session = session;
        Id = "History";
        Title = "기록";
        CanClose = false;
        session.HistoryChanged += (_, _) => Rebuild();
        Rebuild();
    }

    public EditorSession Session { get; }

    public ObservableCollection<HistoryItem> Items { get; } = [];

    partial void OnSelectedChanged(HistoryItem? value)
    {
        if (!_syncing && value is not null)
            Session.JumpInHistory(value.DoneCount);
    }

    private void Rebuild()
    {
        var history = Session.Character.History;
        int current = history.Done.Count;
        var actions = history.Done.Concat(history.Undone).ToList();

        _syncing = true;
        Items.Clear();
        Items.Add(Item(0, Localizer.T("처음 상태")));
        for (int i = 0; i < actions.Count; i++)
            Items.Add(Item(i + 1, Localizer.T(actions[i].Name)));
        Selected = Items[current];
        _syncing = false;

        // the first state is the document as opened or created; later states are marked once saved there
        HistoryItem Item(int doneCount, string label)
        {
            bool saved = doneCount > 0 && doneCount == history.SavePoint;
            return new(doneCount, saved ? $"{label}  ·  {Localizer.T("저장됨")}" : label, doneCount <= current, saved);
        }
    }
}
