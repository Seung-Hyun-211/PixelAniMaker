using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using PixelAniMaker.Core.Animation;
using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.App.Services;

/// <summary>
/// Animation state: the clip list, the frame being edited, keyframe editing, the baked frames and the
/// looping playback shown in the animation preview.
/// </summary>
/// <remarks>
/// Selecting a frame loads the clip's pose for that frame into the working pose of every direction.
/// Pose edits stay in the working pose until <see cref="SaveKey"/> writes them into the clip.
/// </remarks>
public sealed partial class AnimationSession : ObservableObject
{
    private static readonly TimeSpan BakeDelay = TimeSpan.FromMilliseconds(120);

    private readonly EditorSession _editor;
    private readonly DispatcherTimer _bakeTimer;
    private readonly DispatcherTimer _playTimer;
    private Dictionary<Direction, WriteableBitmap[]> _frames = [];
    private IReadOnlyDictionary<Direction, CompositeResult[]> _generated = new Dictionary<Direction, CompositeResult[]>();

    [ObservableProperty] private AnimationClip? _currentClip;
    [ObservableProperty] private int _currentFrame;
    [ObservableProperty] private int _playbackFrame;
    [ObservableProperty] private bool _isPlaying = true;
    [ObservableProperty] private bool _onionSkin;

    public AnimationSession(EditorSession editor)
    {
        _editor = editor;
        _bakeTimer = new DispatcherTimer { Interval = BakeDelay };
        _bakeTimer.Tick += (_, _) => { _bakeTimer.Stop(); Bake(); };
        _playTimer = new DispatcherTimer();
        _playTimer.Tick += (_, _) => AdvancePlayback();

        editor.ContentChanged += (_, _) => ScheduleBake();
        editor.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(EditorSession.Direction))
                OnPropertyChanged(nameof(CurrentKey));
        };
        Clips.CollectionChanged += (_, _) => HasUnsavedClipChanges = true;
        SetClips(TemplateLoader.LoadDefaultAnimations());
    }

    /// <summary>
    /// True after clip-level edits that the undo history does not record (adding/removing clips,
    /// renaming, frame count, fps, loop). Cleared by <see cref="MarkSaved"/>.
    /// </summary>
    [ObservableProperty] private bool _hasUnsavedClipChanges;

    public ObservableCollection<AnimationClip> Clips { get; } = [];

    /// <summary>The key at the current frame in the current direction, if any.</summary>
    public Keyframe? CurrentKey => CurrentClip?.KeyAt(_editor.Direction, CurrentFrame);

    /// <summary>Baked frame bitmap (null before the first bake).</summary>
    public WriteableBitmap? Frame(Direction direction, int frame) =>
        _frames.TryGetValue(direction, out var frames) && frame >= 0 && frame < frames.Length ? frames[frame] : null;

    /// <summary>A frame as generated from the pose, before touch-ups (null before the first bake).</summary>
    public CompositeResult? GeneratedFrame(Direction direction, int frame) =>
        _generated.TryGetValue(direction, out var frames) && frame >= 0 && frame < frames.Length ? frames[frame] : null;

    /// <summary>Raised after the frames have been re-baked.</summary>
    public event EventHandler? FramesUpdated;

    /// <summary>Removes the hand-painted fixes of the current frame in the current direction (undoable).</summary>
    public void ClearTouchup()
    {
        if (CurrentClip is { } clip)
            TouchupChange.Apply(clip.Touchups, _editor.Character.History, _editor.Direction, CurrentFrame, []);
    }

    // ------------------------------------------------------------------ keys

    /// <summary>Stores the working pose of the current direction as a key at the current frame.</summary>
    public void SaveKey()
    {
        if (CurrentClip is not { } clip)
            return;
        var easing = CurrentKey?.Easing ?? Easing.EaseInOut;
        KeyframeChange.Apply(clip, _editor.Character.History, _editor.Direction, CurrentFrame,
            new Keyframe(CurrentFrame, _editor.CurrentPose.Snapshot(), easing));
    }

    public void DeleteKey()
    {
        if (CurrentClip is { } clip)
            KeyframeChange.Apply(clip, _editor.Character.History, _editor.Direction, CurrentFrame, null);
    }

    public void SetEasing(Easing easing)
    {
        if (CurrentClip is { } clip && CurrentKey is { } key && key.Easing != easing)
            KeyframeChange.Apply(clip, _editor.Character.History, _editor.Direction, CurrentFrame, key with { Easing = easing });
    }

    /// <summary>True when the working pose differs from what the clip plays at this frame.</summary>
    public bool IsPoseUnsaved(Direction direction) =>
        CurrentClip is { } clip && !_editor.Character.PoseFor(direction).Snapshot().SameAs(clip.Evaluate(direction, CurrentFrame));

    public void Step(int delta)
    {
        if (CurrentClip is { } clip)
            CurrentFrame = ((CurrentFrame + delta) % clip.FrameCount + clip.FrameCount) % clip.FrameCount;
    }

    // ------------------------------------------------------------------ clips

    public void AddClip(AnimationClip clip)
    {
        clip.PropertyChanged += OnClipSettingChanged;
        Clips.Add(clip);
        CurrentClip = clip;
    }

    public void RemoveCurrentClip()
    {
        if (CurrentClip is not { } clip || Clips.Count <= 1)
            return;
        int i = Clips.IndexOf(clip);
        clip.PropertyChanged -= OnClipSettingChanged;
        Clips.Remove(clip);
        CurrentClip = Clips[Math.Min(i, Clips.Count - 1)];
    }

    /// <summary>A copy of the current clip (settings and every direction's keys).</summary>
    public AnimationClip? DuplicateCurrent() => CurrentClip?.CopyAs(Localizer.T(CurrentClip.Name + " 복사본"));

    /// <summary>Replaces the clip list (new project or a loaded file).</summary>
    public void SetClips(IEnumerable<AnimationClip> clips)
    {
        foreach (var clip in Clips)
            clip.PropertyChanged -= OnClipSettingChanged;
        Clips.Clear();
        foreach (var clip in clips)
        {
            clip.PropertyChanged += OnClipSettingChanged;
            Clips.Add(clip);
        }
        CurrentClip = Clips.FirstOrDefault();
        HasUnsavedClipChanges = false;
    }

    public void MarkSaved() => HasUnsavedClipChanges = false;

    private void OnClipSettingChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) =>
        HasUnsavedClipChanges = true;

    partial void OnCurrentClipChanged(AnimationClip? oldValue, AnimationClip? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.Changed -= OnClipChanged;
            oldValue.Touchups.Changed -= OnClipChanged;
        }
        if (newValue is not null)
        {
            newValue.Changed += OnClipChanged;
            newValue.Touchups.Changed += OnClipChanged;
        }
        PlaybackFrame = 0;
        if (CurrentFrame == 0)
            ApplyFrame();
        else
            CurrentFrame = 0;
        UpdatePlayTimer();
        Bake();
    }

    partial void OnCurrentFrameChanged(int value) => ApplyFrame();

    partial void OnIsPlayingChanged(bool value) => UpdatePlayTimer();

    private void OnClipChanged(object? sender, EventArgs e)
    {
        if (CurrentClip is { } clip && CurrentFrame >= clip.FrameCount)
            CurrentFrame = clip.FrameCount - 1;
        OnPropertyChanged(nameof(CurrentKey));
        UpdatePlayTimer();
        ScheduleBake();
    }

    /// <summary>Loads the clip's pose at the current frame into every direction's working pose.</summary>
    private void ApplyFrame()
    {
        if (CurrentClip is { } clip)
            foreach (var d in DirectionExtensions.Stored)
                _editor.Character.PoseFor(d).Restore(clip.Evaluate(d, CurrentFrame));
        OnPropertyChanged(nameof(CurrentKey));
    }

    // ------------------------------------------------------------------ baking & playback

    private void ScheduleBake()
    {
        _bakeTimer.Stop();
        _bakeTimer.Start();
    }

    private void Bake()
    {
        _frames = [];
        _generated = new Dictionary<Direction, CompositeResult[]>();
        if (CurrentClip is { } clip)
        {
            _generated = SpriteBaker.Bake(_editor.Character, clip, _editor.Compositor, touchups: false);
            _frames = _generated.ToDictionary(kv => kv.Key, kv => kv.Value.Select((generated, f) =>
            {
                var final = generated.Clone();
                clip.Touchups.ApplyTo(final, kv.Key, f);
                return CompositeBitmap.From(final, _editor.Character.Palette);
            }).ToArray());
        }
        FramesUpdated?.Invoke(this, EventArgs.Empty);
    }

    private void UpdatePlayTimer()
    {
        _playTimer.Stop();
        if (CurrentClip is not { } clip || !IsPlaying)
            return;
        _playTimer.Interval = TimeSpan.FromSeconds(1.0 / clip.Fps);
        _playTimer.Start();
    }

    private void AdvancePlayback()
    {
        if (CurrentClip is { } clip)
            PlaybackFrame = (PlaybackFrame + 1) % clip.FrameCount;
    }
}
