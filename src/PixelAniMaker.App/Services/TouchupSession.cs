using Avalonia.Media.Imaging;
using PixelAniMaker.Core.Animation;
using PixelAniMaker.Core.Editing;
using PixelAniMaker.Core.History;
using PixelAniMaker.Core.Imaging;
using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.App.Services;

/// <summary>
/// Frame touch-up: an editable copy of the current generated frame (with its fixes) that the drawing
/// tools paint on. Each finished stroke is stored as the frame's pixel overrides in the shared undo
/// history, so fixes survive when frames are regenerated.
/// </summary>
public sealed class TouchupSession
{
    private readonly EditorSession _editor;
    private readonly AnimationSession _animation;
    private UndoHistory _strokes = new();   // private sink: tools push strokes here, we turn them into overrides
    private IndexedImage? _image;

    public TouchupSession(EditorSession editor, AnimationSession animation)
    {
        _editor = editor;
        _animation = animation;
        editor.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(EditorSession.TouchupMode) or nameof(EditorSession.Direction)
                or nameof(EditorSession.Character))
                Reload();
        };
        animation.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(AnimationSession.CurrentFrame) or nameof(AnimationSession.CurrentClip))
                Reload();
        };
        animation.FramesUpdated += (_, _) => Reload();
    }

    /// <summary>The drawing target while in touch-up mode (null when there is no generated frame yet).</summary>
    public EditorDocument? Document { get; private set; }

    public WriteableBitmap? Bitmap { get; private set; }

    /// <summary>Raised when the touch-up canvas changes (reloaded or painted).</summary>
    public event EventHandler? Updated;

    /// <summary>Number of overridden pixels in the current frame and direction.</summary>
    public int CurrentOverrideCount =>
        _animation.CurrentClip?.Touchups.Get(_editor.Direction, _animation.CurrentFrame).Count ?? 0;

    /// <summary>Rebuilds the canvas from the generated frame plus its stored fixes.</summary>
    private void Reload()
    {
        if (Document is not null)
        {
            Document.EndStroke();
            Document.PixelsChanged -= OnPixelsChanged;
        }
        Document = null;

        var generated = _animation.GeneratedFrame(_editor.Direction, _animation.CurrentFrame);
        if (!_editor.TouchupMode || generated is null || _animation.CurrentClip is not { } clip)
        {
            Updated?.Invoke(this, EventArgs.Empty);
            return;
        }

        var final = generated.Clone();
        clip.Touchups.ApplyTo(final, _editor.Direction, _animation.CurrentFrame);
        _image = new IndexedImage(final.Width, final.Height);
        for (int y = 0; y < final.Height; y++)
            for (int x = 0; x < final.Width; x++)
                _image.Set(x, y, final.Indices[y * final.Width + x]);

        _strokes = new UndoHistory();
        _strokes.Changed += OnStrokeCommitted;
        Document = new EditorDocument(_image, _editor.Character.Palette, _strokes, _editor.Character.Colors);
        Document.PixelsChanged += OnPixelsChanged;
        Bitmap = CompositeBitmap.Create(final.Width, final.Height);
        Render();
    }

    private void OnPixelsChanged(object? sender, EventArgs e) => Render();

    /// <summary>A stroke finished: store the difference to the generated frame as the frame's overrides.</summary>
    private void OnStrokeCommitted(object? sender, EventArgs e)
    {
        var generated = _animation.GeneratedFrame(_editor.Direction, _animation.CurrentFrame);
        if (_image is null || generated is null || _animation.CurrentClip is not { } clip)
            return;
        TouchupChange.Apply(clip.Touchups, _editor.Character.History, _editor.Direction, _animation.CurrentFrame,
            FrameTouchups.Diff(generated, _image));
    }

    private void Render()
    {
        if (_image is null || Bitmap is null)
            return;
        var view = new CompositeResult(_image.Width, _image.Height);
        _image.Pixels.CopyTo(view.Indices);
        CompositeBitmap.Write(view, _editor.Character.Palette, Bitmap);
        Updated?.Invoke(this, EventArgs.Empty);
    }
}
