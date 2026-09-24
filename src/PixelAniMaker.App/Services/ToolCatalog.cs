using PixelAniMaker.Core.Editing;

namespace PixelAniMaker.App.Services;

public sealed record ToolItem(ITool Tool, string Label, string Shortcut)
{
    public string Display => $"{Label}  ({Shortcut})";
}

/// <summary>The tools shown in the UI — the single place to register a new tool.</summary>
public static class ToolCatalog
{
    public static ToolItem Pencil { get; } = new(PencilTool.Pencil, "연필", "B");
    public static ToolItem Eraser { get; } = new(PencilTool.Eraser, "지우개", "E");
    public static ToolItem Fill { get; } = new(FillTool.Instance, "채우기", "G");
    public static ToolItem Eyedropper { get; } = new(EyedropperTool.Instance, "스포이드", "I");

    public static IReadOnlyList<ToolItem> All { get; } = [Pencil, Eraser, Fill, Eyedropper];
}
