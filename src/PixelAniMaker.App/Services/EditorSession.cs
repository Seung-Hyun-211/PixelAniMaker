using System.Numerics;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using PixelAniMaker.Core.Editing;
using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.App.Services;

/// <summary>
/// State shared by every panel: the character, the direction and part being edited, the current tool
/// and mode, and the composited bitmaps shown by the canvas and the previews.
/// </summary>
/// <remarks>
/// Geometry (transforms, hit-testing) is kept in the stored source direction; the Right view is the
/// Left view mirrored, so canvas input and overlays go through <see cref="ToSource"/>/<see cref="FromSource"/>.
/// </remarks>
public sealed partial class EditorSession : ObservableObject
{
    public static readonly int[] ZoomSteps = [1, 2, 3, 4, 6, 8, 12, 16, 24, 32, 48];

    private readonly Dictionary<Direction, WriteableBitmap> _previewBitmaps = [];
    private readonly HashSet<Part> _hiddenParts = [];
    private readonly HashSet<Part> _lockedParts = [];

    [ObservableProperty] private Character _character = null!;
    [ObservableProperty] private Direction _direction = Direction.Front;
    [ObservableProperty] private Part _activePart = null!;
    [ObservableProperty] private EditorDocument _activeDocument = null!;
    [ObservableProperty] private WriteableBitmap _canvasBitmap = null!;
    [ObservableProperty] private ToolItem _currentTool = ToolCatalog.Pencil;
    [ObservableProperty] private bool _poseMode;

    /// <summary>Front/back views: strokes are mirrored across the body's centre line (onto the _l/_r counterpart).</summary>
    [ObservableProperty] private bool _symmetric;

    /// <summary>Layer of the active part that strokes go to (0 = bottom; clamped per part).</summary>
    [ObservableProperty] private int _activeLayer;

    /// <summary>Freehand pencil removes L-corner pixels so lines stay 1px thin.</summary>
    [ObservableProperty] private bool _pixelPerfect = true;

    /// <summary>Paint directly on generated frames (the touch-up layer) instead of on parts.</summary>
    [ObservableProperty] private bool _touchupMode;
    [ObservableProperty] private bool _dimOtherParts = true;
    [ObservableProperty] private int _zoom = 4;
    [ObservableProperty] private bool _showGrid = true;
    [ObservableProperty] private string _cursorText = "";

    /// <summary>Outline or shading settings changed since the character was loaded or saved (they are not in the undo history).</summary>
    [ObservableProperty] private bool _hasUnsavedSettings;

    /// <summary>Show the current frame's secondary motion on the editing canvas (off: the keyed pose as is).</summary>
    [ObservableProperty] private bool _showSecondaryOnCanvas;

    /// <summary>Secondary motion of the current animation frame per direction (set by the animation session).</summary>
    public Func<Direction, Core.Animation.SecondaryFrame?>? SecondaryProvider { get; set; }

    /// <summary>Whether exports include secondary motion (a project setting, saved with it).</summary>
    public bool SecondaryInExport
    {
        get => Character.SecondaryInExport;
        set
        {
            if (Character.SecondaryInExport == value)
                return;
            Character.SecondaryInExport = value;
            HasUnsavedSettings = true;
            OnPropertyChanged();
        }
    }

    partial void OnShowSecondaryOnCanvasChanged(bool value) => Refresh();

    /// <summary>Sets or (null) removes the active part's secondary motion, as one undoable step.</summary>
    public void SetActiveSecondary(Core.Animation.SecondarySettings? settings) =>
        Core.Animation.SecondaryChange.Apply(ActivePart, Character.History, settings?.Clamped());

    public EditorSession()
    {
        Reference.Changed += (_, _) => ImageUpdated?.Invoke(this, EventArgs.Empty);
        LoadCharacter(TemplateLoader.LoadMannequin());
    }

    /// <summary>Reference pictures shown on the canvas (per direction, not saved).</summary>
    public ReferenceLayer Reference { get; } = new();

    /// <summary>Composite of the current direction's source view (for hit-testing in source coordinates).</summary>
    public CompositeResult SourceComposite { get; private set; } = null!;

    /// <summary>Placement of each part for the current direction, in source coordinates.</summary>
    public IReadOnlyDictionary<Part, PartTransform> Transforms { get; private set; } = null!;

    public PartTransform ActiveTransform => Transforms[ActivePart];

    public Pose CurrentPose => Character.PoseFor(Direction);

    /// <summary>The tool strokes use: the selected tool, with the pencil's pixel-perfect option applied.</summary>
    public ITool ActiveTool => CurrentTool == ToolCatalog.Pencil && PixelPerfect ? PencilTool.PixelPerfectPencil : CurrentTool.Tool;

    /// <summary>Automatic 1px outline on composites (off: parts are used exactly as drawn).</summary>
    public bool AutoOutline
    {
        get => Character.Outline.Enabled;
        set
        {
            Character.Outline.Enabled = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Automatic shading on composites (a darker band on the edges away from the light).</summary>
    public bool AutoShading
    {
        get => Character.Shading.Enabled;
        set
        {
            Character.Shading.Enabled = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Light from the upper right instead of the upper left.</summary>
    public bool ShadingFromRight
    {
        get => Character.Shading.Light == LightFrom.TopRight;
        set
        {
            Character.Shading.Light = value ? LightFrom.TopRight : LightFrom.TopLeft;
            OnPropertyChanged();
        }
    }

    public decimal ShadingWidth
    {
        get => Character.Shading.Width;
        set
        {
            Character.Shading.Width = (int)value;
            OnPropertyChanged();
        }
    }

    public WriteableBitmap PreviewBitmap(Direction direction) => _previewBitmaps[direction];

    /// <summary>Shared compositor (keeps the rotation caches warm for the canvas and animation frames).</summary>
    public Compositor Compositor { get; } = new();

    /// <summary>Raised after the bitmaps have been refreshed.</summary>
    public event EventHandler? ImageUpdated;

    /// <summary>Raised when the undo history or dirty state changes.</summary>
    public event EventHandler? HistoryChanged;

    /// <summary>Raised when parts are added or removed (after the active part has been kept valid).</summary>
    public event EventHandler? PartsChanged;

    /// <summary>Raised when anything that changes rendered frames changes: pixels, palette, outline, undo/redo.</summary>
    public event EventHandler? ContentChanged;

    /// <summary>Replaces the character being edited (new template or a loaded project).</summary>
    public void LoadCharacter(Character character)
    {
        if (Character is not null)
        {
            Character.History.Changed -= OnHistoryChanged;
            Character.Palette.Changed -= OnContentChanged;
            Character.Outline.Changed -= OnSettingsChanged;
            Character.Shading.Changed -= OnSettingsChanged;
            Character.PoseChanged -= OnPoseChanged;
            Character.PartsChanged -= OnPartsChanged;
            Character.DirectionsChanged -= OnDirectionsChanged;
            Character.CanvasSizeChanged -= OnCanvasSizeChanged;
        }

        character.History.Changed += OnHistoryChanged;
        character.Palette.Changed += OnContentChanged;
        character.Outline.Changed += OnSettingsChanged;
        character.Shading.Changed += OnSettingsChanged;
        HasUnsavedSettings = false;
        character.PoseChanged += OnPoseChanged;
        character.PartsChanged += OnPartsChanged;
        character.DirectionsChanged += OnDirectionsChanged;
        character.CanvasSizeChanged += OnCanvasSizeChanged;

        _hiddenParts.Clear();
        _lockedParts.Clear();
        CreateBitmaps(character);

        Character = character;
        OnPropertyChanged(nameof(AutoOutline));
        OnPropertyChanged(nameof(AutoShading));
        OnPropertyChanged(nameof(SecondaryInExport));
        OnPropertyChanged(nameof(ShadingFromRight));
        OnPropertyChanged(nameof(ShadingWidth));
        RaiseOptionalPartsChanged();
        ActivePart = character.Find("chest") ?? character.Root;
        OnDirectionsChanged(this, EventArgs.Empty);   // the new character may not have the current (3/4) direction
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    private void CreateBitmaps(Character character)
    {
        CanvasBitmap = CompositeBitmap.Create(character.Width, character.Height);
        foreach (var d in DirectionExtensions.Every)
            _previewBitmaps[d] = CompositeBitmap.Create(character.Width, character.Height);
    }

    /// <summary>Canvas size in pixels; changes with <see cref="ResizeCanvas"/> (and its undo).</summary>
    public (int Width, int Height) CanvasSize => (Character.Width, Character.Height);

    /// <summary>Adds (negative: removes) margins around the canvas as one undo step; false when refused.</summary>
    public bool ResizeCanvas(CanvasMargins margins, IEnumerable<Core.Animation.AnimationClip> clips) =>
        CanvasResize.Apply(Character, clips, margins, Character.History);

    private void OnCanvasSizeChanged(object? sender, CanvasShift shift)
    {
        Reference.Shift(shift);
        CreateBitmaps(Character);
        OnPropertyChanged(nameof(CanvasSize));
        Refresh();
    }

    public void ZoomBy(int steps)
    {
        int i = Array.FindLastIndex(ZoomSteps, z => z <= Zoom);
        Zoom = ZoomSteps[Math.Clamp(i + steps, 0, ZoomSteps.Length - 1)];
    }

    /// <summary>
    /// Rotates a joint in the current direction as one undoable step, within the part's rotation limit
    /// (not for locked parts).
    /// </summary>
    public void SetRotation(Part part, double degrees)
    {
        if (!IsLocked(part))
            PoseChange.Apply(CurrentPose, Character.History, p => p.Set(part.Name, part.ClampRotation(degrees)));
    }

    /// <summary>Moves the active part (with its detail parts, and optionally the parts below it) in this direction's drawing order (undoable).</summary>
    public bool MoveActiveDrawOrder(DrawOrderMove move, bool withChildren) =>
        !IsLocked(ActivePart) && DrawOrderEdit.Move(Character, ActivePart, Direction, move, Character.History, withChildren);

    public bool CanMoveActiveDrawOrder(DrawOrderMove move, bool withChildren) =>
        DrawOrderEdit.CanMove(Character, ActivePart, Direction, move, withChildren);

    /// <summary>Parts as they are drawn now in this direction, back to front (the current pose's order changes included).</summary>
    public IReadOnlyList<Part> DrawnOrder =>
        DrawOrderEdit.WithOverrides(Character, Character.DrawOrder(Direction).ToList(), CurrentPose.Snapshot());

    /// <summary>The current pose's drawing order change for the active part (null: the direction's order).</summary>
    public OrderOverride? ActivePoseOrder => CurrentPose.GetOrder(ActivePart.Name);

    /// <summary>In the current pose only, draws the active part in front of or behind another part (undoable; K saves it in the key).</summary>
    public void SetActivePoseOrder(OrderOverride? order)
    {
        if (!IsLocked(ActivePart) && !ActivePart.IsDetail)
            PoseChange.Apply(CurrentPose, Character.History, p => p.SetOrder(ActivePart.Name, order), "순서 (이 포즈)");
    }

    /// <summary>Sets or (null) removes the active part's allowed rotation, as one undoable step.</summary>
    public void SetActiveRotationLimit(RotationLimit? limit) => RotationLimitChange.Apply(ActivePart, Character.History, limit);

    // ------------------------------------------------------------------ hide / lock (editing aids, not saved)

    /// <summary>Hidden parts are left out of the canvas only; previews, animation frames and exports still show them.</summary>
    public bool IsHidden(Part part) => _hiddenParts.Contains(part);

    /// <summary>Locked parts cannot be drawn on, rotated or moved (selecting them still works).</summary>
    public bool IsLocked(Part part) => _lockedParts.Contains(part);

    public void SetHidden(Part part, bool hidden)
    {
        if (hidden ? _hiddenParts.Add(part) : _hiddenParts.Remove(part))
            OnPartStateChanged();
    }

    public void SetLocked(Part part, bool locked)
    {
        if (locked ? _lockedParts.Add(part) : _lockedParts.Remove(part))
            OnPartStateChanged();
    }

    /// <summary>Canvas strokes reach the active part only while it is shown and unlocked.</summary>
    public bool CanDrawActivePart => ActivePart is not null && !IsHidden(ActivePart) && !IsLocked(ActivePart);

    public bool IsActivePartLocked => ActivePart is not null && IsLocked(ActivePart);

    /// <summary>Why canvas strokes do not reach the active part, or "" when they do.</summary>
    public string ActivePartBlockedReason =>
        ActivePart is null ? ""
        : IsLocked(ActivePart) ? "잠긴 파츠 — 그리기·회전·위치 옮기기가 막혀 있습니다"
        : IsHidden(ActivePart) ? "숨긴 파츠 — 캔버스에서 그릴 수 없습니다"
        : "";

    /// <summary>Raised when a part is hidden, shown, locked or unlocked.</summary>
    public event EventHandler? PartStatesChanged;

    private void OnPartStateChanged()
    {
        NotifyActivePartState();
        Refresh();
        PartStatesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void NotifyActivePartState()
    {
        OnPropertyChanged(nameof(CanDrawActivePart));
        OnPropertyChanged(nameof(IsActivePartLocked));
        OnPropertyChanged(nameof(ActivePartBlockedReason));
    }

    /// <summary>Moves the whole body (root offset, whole pixels) as one undoable step.</summary>
    public void SetOffset(Vector2 offset) =>
        PoseChange.Apply(CurrentPose, Character.History, p => p.Offset = offset, "몸 위치");

    public void ResetPose() =>
        PoseChange.Apply(CurrentPose, Character.History, p => p.Restore(PoseData.Rest), "포즈 초기화");

    /// <summary>Picks a drawing tool; always leaves pose mode, even when the tool is already selected.</summary>
    public void SelectTool(ToolItem tool)
    {
        CurrentTool = tool;
        PoseMode = false;
    }

    public void Undo() => ActiveDocument.Undo();

    public void Redo() => ActiveDocument.Redo();

    /// <summary>Undoes or redoes to the state with <paramref name="doneCount"/> actions applied (history panel).</summary>
    public void JumpInHistory(int doneCount)
    {
        ActiveDocument.EndStroke();
        Character.History.MoveTo(doneCount);
    }

    /// <summary>Display canvas point → source-direction point (mirrors x in the Right view).</summary>
    public Vector2 ToSource(double x, double y) =>
        new((float)(Direction.IsMirrored() ? Character.Width - x : x), (float)y);

    /// <summary>Source-direction point → display canvas point.</summary>
    public Vector2 FromSource(Vector2 p) => Direction.IsMirrored() ? p with { X = Character.Width - p.X } : p;

    partial void OnActivePartChanged(Part value)
    {
        NotifyActivePartState();
        ReopenDocument();
    }

    partial void OnDirectionChanged(Direction value)
    {
        Reference.Direction = value;
        ReopenDocument();
    }

    partial void OnCurrentToolChanged(ToolItem value)
    {
        PoseMode = false;
        UpdateMirror();
    }

    partial void OnSymmetricChanged(bool value) => UpdateMirror();

    partial void OnActiveLayerChanged(int value) => ReopenDocument();

    /// <summary>The layers of the active part in the current direction.</summary>
    public PartLayers ActiveLayers => ActiveTransform.View.Layers;

    /// <summary><see cref="ActiveLayer"/> limited to the layers the active part has.</summary>
    public int ActiveLayerIndex => Math.Clamp(ActiveLayer, 0, ActiveLayers.Count - 1);

    /// <summary>Adds an empty layer above the active one and makes it active.</summary>
    public void AddLayer()
    {
        var layers = ActiveLayers;
        int at = ActiveLayerIndex + 1;
        LayerChange.Apply(layers, Character.History, "레이어 추가",
            list => list.Insert(at, layers.CreateLayer(layers.FreeName(Localizer.T("레이어")))));
        ActiveLayer = at;
    }

    public void RemoveActiveLayer()
    {
        int at = ActiveLayerIndex;
        LayerChange.Apply(ActiveLayers, Character.History, "레이어 삭제", list => list.RemoveAt(at));
        ActiveLayer = Math.Max(0, at - 1);
    }

    /// <summary>Moves the active layer up (+1) or down (-1) in the stack.</summary>
    public void MoveActiveLayer(int delta)
    {
        int from = ActiveLayerIndex, to = from + delta;
        if (to < 0 || to >= ActiveLayers.Count)
            return;
        LayerChange.Apply(ActiveLayers, Character.History, "레이어 순서", list => (list[from], list[to]) = (list[to], list[from]));
        ActiveLayer = to;
    }

    public void RenameLayer(int index, string name) =>
        LayerChange.Apply(ActiveLayers, Character.History, "레이어 이름", list => list[index].Name = name);

    public void SetLayerVisible(int index, bool visible) =>
        LayerChange.Apply(ActiveLayers, Character.History, visible ? "레이어 보이기" : "레이어 숨기기", list => list[index].Visible = visible);

    /// <summary>Mirrors strokes while symmetric editing applies (not for moving a selection).</summary>
    private void UpdateMirror()
    {
        if (ActiveDocument is null)
            return;
        var counterpart = Core.Rigging.Symmetry.Counterpart(Character, ActivePart);
        ActiveDocument.Mirror = Symmetric && CurrentTool != ToolCatalog.Select && !IsHidden(counterpart) && !IsLocked(counterpart)
            ? Core.Rigging.Symmetry.For(Character, ActivePart, Direction, ActiveLayerIndex)
            : null;
    }

    partial void OnPoseModeChanged(bool value)
    {
        if (value)
            TouchupMode = false;
        Refresh();
    }

    partial void OnTouchupModeChanged(bool value)
    {
        if (value)
            PoseMode = false;
    }

    partial void OnDimOtherPartsChanged(bool value) => Refresh();

    /// <summary>The angle variant the active part is drawn with right now (null = base image).</summary>
    public int? ActiveVariantAngle => ActiveTransform.View.Variants.Pick(ActiveTransform.Angle * 180 / Math.PI);

    /// <summary>
    /// Adds a hand-drawable image for the active part at the 45° step nearest to its current rotation,
    /// starting from the rotated base image. Returns a message when nothing was added.
    /// </summary>
    public string? AddVariantAtCurrentAngle()
    {
        var view = ActiveTransform.View;
        int angle = AngleVariants.NearestStep(ActiveTransform.Angle * 180 / Math.PI);
        if (angle == 0)
            return "0° 근처는 기본 이미지를 씁니다. 파츠를 45° 이상 돌린 뒤 만드세요.";
        if (view.Variants.Get(angle) is not null)
            return $"{angle}° 이미지가 이미 있습니다.";
        VariantChange.Apply(view.Variants, Character.History, angle, VariantChange.RenderFromBase(view, angle));
        return null;
    }

    /// <summary>Directions the character is shown in (4, or 8 with 3/4 views), in sheet order.</summary>
    public IReadOnlyList<Direction> Directions => Character.Directions;

    public bool HasThreeQuarter => Character.HasThreeQuarter;

    /// <summary>Turns 3/4 views on (copies of front/back to redraw), as one undo step.</summary>
    public void AddThreeQuarter() => ThreeQuarterViews.Enable(Character, Character.History);

    /// <summary>Turns 3/4 views off, removing the clips' 3/4 keys and touch-ups too (one undo step).</summary>
    public void RemoveThreeQuarter(IEnumerable<Core.Animation.AnimationClip> clips) =>
        ThreeQuarterViews.Disable(Character, clips, Character.History);

    private void OnDirectionsChanged(object? sender, EventArgs e)
    {
        if (!Directions.Contains(Direction))
            Direction = Direction.Fallback();
        OnPropertyChanged(nameof(Directions));
        OnPropertyChanged(nameof(HasThreeQuarter));
    }

    public bool HasEyes => EyeParts.Has(Character);

    public bool CanAddEyes => EyeParts.CanAdd(Character);

    /// <summary>Adds the eye parts (undoable) and selects the right eye to draw on.</summary>
    public void AddEyes()
    {
        EyeParts.Add(Character);
        if (Character.Find(EyeParts.Right) is { } eye)
            ActivePart = eye;
    }

    public void RemoveEyes() => EyeParts.Remove(Character);

    public bool HasBust => BustPart.Has(Character);

    public bool CanAddBust => BustPart.CanAdd(Character);

    /// <summary>Adds the bust part (undoable) and selects it to draw on.</summary>
    public void AddBust(BustSize size)
    {
        BustPart.Add(Character, size);
        if (Character.Find(BustPart.Name) is { } bust)
            ActivePart = bust;
    }

    public void RemoveBust() => BustPart.Remove(Character);

    /// <summary>Adds a part (all its links) under the active part (undoable) and selects its first link to draw on.</summary>
    public void AddCustomPart(CustomPartOptions options)
    {
        var links = CustomParts.Add(Character, ActivePart, options);
        ActivePart = links[0];
    }

    /// <summary>Renames the active added part's shown name (undoable).</summary>
    public bool RenameActivePart(string label) => CustomParts.Rename(Character, ActivePart, label);

    /// <summary>True when the active part was added by the user and can be removed.</summary>
    public bool CanRemoveActivePart => CustomParts.CanRemove(ActivePart);

    /// <summary>Removes the active added part and the links under it (undoable); selects its parent.</summary>
    public void RemoveActivePart()
    {
        var parent = ActivePart.Parent;
        if (CustomParts.Remove(Character, ActivePart) && parent is not null)
            ActivePart = parent;
    }

    /// <summary>Joint of the active part in display coordinates when it can be moved (eye, bust, added part), else null.</summary>
    public Vector2? ActivePartPosition => ActivePart.IsMovable ? FromSource(ActivePart.View(Direction).RestPivot) : null;

    /// <summary>Moves the active movable part so its joint lands on <paramref name="display"/> (whole pixels, undoable).</summary>
    public void MoveActivePartTo(Vector2 display)
    {
        if (!ActivePart.IsMovable || IsLocked(ActivePart))
            return;
        PartMove.Apply(ActivePart, Direction, FromSource(display) - ActivePart.View(Direction).RestPivot, Character.History);
    }

    private void RaiseOptionalPartsChanged()
    {
        OnPropertyChanged(nameof(HasEyes));
        OnPropertyChanged(nameof(CanAddEyes));
        OnPropertyChanged(nameof(HasBust));
        OnPropertyChanged(nameof(CanAddBust));
    }

    private void OnPartsChanged(object? sender, EventArgs e)
    {
        if (!Character.Parts.Contains(ActivePart))
            ActivePart = Character.Find(EyeParts.HeadName) ?? Character.Root;
        _hiddenParts.RemoveWhere(p => !Character.Parts.Contains(p));
        _lockedParts.RemoveWhere(p => !Character.Parts.Contains(p));
        RaiseOptionalPartsChanged();
        PartsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Mirrored directions only (right, right-facing 3/4): draw the active part separately (true) or mirror again.</summary>
    public void SetOwnRight(bool separate)
    {
        if (Direction.IsMirrored())
            RightViewChange.Apply(ActivePart, Character.History, separate, Direction);
    }

    public void RemoveActiveVariant()
    {
        if (ActiveVariantAngle is { } angle)
            VariantChange.Apply(ActiveTransform.View.Variants, Character.History, angle, null);
    }

    /// <summary>Points drawing at the active part's image for the current direction.</summary>
    private void ReopenDocument()
    {
        if (ActivePart is null)
            return;
        AttachDocument(Character.ComputeTransforms(Direction)[ActivePart].EditImage(ActiveLayer));
        Refresh();
    }

    /// <summary>Makes <paramref name="image"/> (base or angle variant) the drawing target, if it is not already.</summary>
    private void AttachDocument(Core.Imaging.IndexedImage image)
    {
        if (ActiveDocument?.Image == image)
            return;
        if (ActiveDocument is not null)
        {
            ActiveDocument.EndStroke();
            ActiveDocument.PixelsChanged -= OnContentChanged;
            ActiveDocument.SelectionChanged -= OnSelectionChanged;
        }
        ActiveDocument = Character.CreateDocument(image);
        ActiveDocument.PixelsChanged += OnContentChanged;
        ActiveDocument.SelectionChanged += OnSelectionChanged;
    }

    private void OnHistoryChanged(object? sender, EventArgs e)
    {
        Refresh(); // undo/redo may have changed any part's pixels or pose
        HistoryChanged?.Invoke(this, EventArgs.Empty);
        ContentChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnContentChanged(object? sender, EventArgs e)
    {
        Refresh();
        ContentChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        HasUnsavedSettings = true;
        OnContentChanged(sender, e);
    }

    public void MarkSettingsSaved() => HasUnsavedSettings = false;

    private void OnPoseChanged(object? sender, EventArgs e) => Refresh();

    private void OnSelectionChanged(object? sender, EventArgs e) => ImageUpdated?.Invoke(this, EventArgs.Empty);

    private void Refresh()
    {
        if (Character is null || ActivePart is null)
            return;
        Transforms = Character.ComputeTransforms(Direction);
        AttachDocument(ActiveTransform.EditImage(ActiveLayer)); // rotating into or out of an angle variant switches the target
        UpdateMirror();                        // the mirror follows the pose
        var composites = Character.Directions.ToDictionary(d => d, d => Compositor.Compose(Character, d));
        var sway = ShowSecondaryOnCanvas ? SecondaryProvider?.Invoke(Direction) : null;
        var canvas = _hiddenParts.Count == 0 && sway is null ? composites[Direction]
            : Compositor.Compose(Character, Direction, hidden: _hiddenParts.Count == 0 ? null : _hiddenParts, secondary: sway);
        SourceComposite = canvas;   // hidden parts cannot be picked on the canvas
        if (Direction.IsMirrored())
        {
            // hit-testing works in source coordinates; the Right view may have its own images
            SourceComposite = SourceComposite.Clone();
            SourceComposite.MirrorHorizontally();
        }

        foreach (var (d, composite) in composites)
            CompositeBitmap.Write(composite, Character.Palette, _previewBitmaps[d]);
        CompositeBitmap.Write(canvas, Character.Palette, CanvasBitmap,
            DimOtherParts && !PoseMode ? Character.IndexOf(ActivePart) : null);
        ImageUpdated?.Invoke(this, EventArgs.Empty);
    }
}
