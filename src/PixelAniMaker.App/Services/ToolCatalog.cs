using PixelAniMaker.Core.Editing;

namespace PixelAniMaker.App.Services;

/// <param name="ShortcutId">Entry in <see cref="ShortcutCatalog"/> that selects this tool.</param>
/// <param name="DrawsOutside">Strokes may add pixels past the part's current area (the area grows to fit).</param>
public sealed record ToolItem(ITool Tool, string Label, string ShortcutId, bool DrawsOutside = false);

/// <summary>The tools shown in the UI — the single place to register a new tool.</summary>
public static class ToolCatalog
{
    public static ToolItem Pencil { get; } = new(PencilTool.Pencil, "연필", "Tool.Pencil", DrawsOutside: true);
    public static ToolItem Eraser { get; } = new(PencilTool.Eraser, "지우개", "Tool.Eraser");
    public static ToolItem Fill { get; } = new(FillTool.Instance, "채우기", "Tool.Fill");
    public static ToolItem Eyedropper { get; } = new(EyedropperTool.Instance, "스포이드", "Tool.Eyedropper");
    public static ToolItem Line { get; } = new(ShapeTool.Line, "선", "Tool.Line", DrawsOutside: true);
    public static ToolItem Rectangle { get; } = new(ShapeTool.Rectangle, "사각형", "Tool.Rectangle", DrawsOutside: true);
    public static ToolItem Ellipse { get; } = new(ShapeTool.Ellipse, "원", "Tool.Ellipse", DrawsOutside: true);
    public static ToolItem Select { get; } = new(SelectMoveTool.Instance, "선택·이동", "Tool.Select");

    public static IReadOnlyList<ToolItem> All { get; } = [Pencil, Eraser, Fill, Eyedropper, Line, Rectangle, Ellipse, Select];
}
