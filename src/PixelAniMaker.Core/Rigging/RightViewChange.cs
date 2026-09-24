using PixelAniMaker.Core.History;

namespace PixelAniMaker.Core.Rigging;

/// <summary>Undoable switch of a part between a mirrored and a separately drawn Right view.</summary>
public sealed class RightViewChange(Part part, PartView? before, PartView? after) : IUndoableAction
{
    public string Name => after is null ? "우측면 반전으로 되돌리기" : "우측면 따로 그리기";

    public void Undo() => part.SetOwnRight(before);

    public void Redo() => part.SetOwnRight(after);

    /// <summary>
    /// Gives <paramref name="part"/> its own right view, starting as a copy of the Left view (same
    /// joints, images and angle variants), or with <paramref name="separate"/> false drops it.
    /// </summary>
    public static void Apply(Part part, UndoHistory history, bool separate)
    {
        if (part.HasOwnRight == separate)
            return;
        var before = separate ? null : part.View(Direction.Right);
        var after = separate ? CopyOf(part.View(Direction.Left)) : null;
        var change = new RightViewChange(part, before, after);
        change.Redo();
        history.Push(change);
    }

    private static PartView CopyOf(PartView view)
    {
        var copy = new PartView(view.Layers[0].Image.Clone(), view.RestPosition, view.RestPivot, view.DrawOrder);
        copy.Layers.Items.Clear();
        foreach (var layer in view.Layers.All)
            copy.Layers.Items.Add(new PartLayer(layer.Name, layer.Image.Clone(), layer.Visible));
        foreach (var (angle, variant) in view.Variants.All)
            copy.Variants.Set(angle, variant with { Image = variant.Image.Clone() });
        foreach (var (name, point) in view.Attachments.All)
            copy.Attachments.Set(name, point);
        return copy;
    }
}
