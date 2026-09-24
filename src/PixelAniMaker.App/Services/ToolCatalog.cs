using PixelAniMaker.Core.Editing;

namespace PixelAniMaker.App.Services;

/// <param name="ShortcutId">Entry in <see cref="ShortcutCatalog"/> that selects this tool.</param>
public sealed record ToolItem(ITool Tool, string Label, string ShortcutId);

/// <summary>The tools shown in the UI — the single place to register a new tool.</summary>
public static class ToolCatalog
{
    public static ToolItem Pencil { get; } = new(PencilTool.Pencil, "연필", "Tool.Pencil");
    public static ToolItem Eraser { get; } = new(PencilTool.Eraser, "지우개", "Tool.Eraser");
    public static ToolItem Fill { get; } = new(FillTool.Instance, "채우기", "Tool.Fill");
    public static ToolItem Eyedropper { get; } = new(EyedropperTool.Instance, "스포이드", "Tool.Eyedropper");

    public static IReadOnlyList<ToolItem> All { get; } = [Pencil, Eraser, Fill, Eyedropper];
}
