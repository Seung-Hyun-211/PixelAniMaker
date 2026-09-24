using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using PixelAniMaker.App.Services;
using PixelAniMaker.Core.Animation;

namespace PixelAniMaker.App.ViewModels;

public sealed partial class FrameCellViewModel(int index) : ObservableObject
{
    public int Index { get; } = index;
    public string Label => (Index + 1).ToString();

    [ObservableProperty] private Bitmap? _thumbnail;
    [ObservableProperty] private bool _hasKey;
    [ObservableProperty] private bool _isCurrent;
}

/// <summary>Clip selection and settings, the frame bar and keyframe commands for the current direction.</summary>
public sealed partial class TimelineViewModel : Tool
{
    private int _newClipCount;

    public TimelineViewModel(EditorSession session, AnimationSession animation)
    {
        Session = session;
        Animation = animation;
        Id = "Timeline";
        Title = "타임라인";
        CanClose = false;

        animation.FramesUpdated += (_, _) => UpdateCells();
        animation.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(AnimationSession.CurrentClip) or nameof(AnimationSession.CurrentFrame)
                or nameof(AnimationSession.CurrentKey))
                UpdateCells();
        };
        session.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(EditorSession.Direction))
                UpdateCells();
        };
        session.ImageUpdated += (_, _) => OnPropertyChanged(nameof(KeyStatus)); // pose edits refresh the image
        UpdateCells();
    }

    public EditorSession Session { get; }
    public AnimationSession Animation { get; }

    public ObservableCollection<FrameCellViewModel> Frames { get; } = [];

    public IReadOnlyList<Easing> EasingOptions { get; } = [Easing.EaseInOut, Easing.Linear, Easing.Step];

    /// <summary>Interpolation of the key at the current frame (null when there is no key).</summary>
    public Easing? CurrentEasing
    {
        get => Animation.CurrentKey?.Easing;
        set
        {
            if (value is { } easing)
                Animation.SetEasing(easing);
        }
    }

    public string KeyStatus =>
        Animation.CurrentClip is null ? ""
        : Animation.IsPoseUnsaved(Session.Direction) ? "포즈가 키와 다름 — K로 저장"
        : Animation.CurrentKey is null ? "키 없음 (보간된 포즈)"
        : "키 프레임";

    [RelayCommand] private void SelectFrame(int index) => Animation.CurrentFrame = index;
    [RelayCommand] private void PreviousFrame() => Animation.Step(-1);
    [RelayCommand] private void NextFrame() => Animation.Step(1);
    [RelayCommand] private void SaveKey() => Animation.SaveKey();
    [RelayCommand] private void DeleteKey() => Animation.DeleteKey();
    [RelayCommand] private void TogglePlay() => Animation.IsPlaying = !Animation.IsPlaying;

    [RelayCommand]
    private void NewClip() => Animation.AddClip(new AnimationClip($"새 동작 {++_newClipCount}", 8));

    [RelayCommand]
    private void DuplicateClip()
    {
        if (Animation.DuplicateCurrent() is { } copy)
            Animation.AddClip(copy);
    }

    [RelayCommand] private void DeleteClip() => Animation.RemoveCurrentClip();

    /// <summary>Rebuilds the frame bar when the frame count changes, otherwise updates it in place.</summary>
    private void UpdateCells()
    {
        int count = Animation.CurrentClip?.FrameCount ?? 0;
        while (Frames.Count > count)
            Frames.RemoveAt(Frames.Count - 1);
        while (Frames.Count < count)
            Frames.Add(new FrameCellViewModel(Frames.Count));

        var clip = Animation.CurrentClip;
        foreach (var cell in Frames)
        {
            cell.Thumbnail = Animation.Frame(Session.Direction, cell.Index);
            cell.HasKey = clip?.KeyAt(Session.Direction, cell.Index) is not null;
            cell.IsCurrent = cell.Index == Animation.CurrentFrame;
        }
        OnPropertyChanged(nameof(CurrentEasing));
        OnPropertyChanged(nameof(KeyStatus));
    }
}

/// <summary>Looping playback of the baked frames in all four directions.</summary>
public sealed class AnimationPreviewViewModel : Tool
{
    public AnimationPreviewViewModel(AnimationSession animation)
    {
        Animation = animation;
        Id = "AnimationPreview";
        Title = "애니메이션";
        CanClose = false;
    }

    public AnimationSession Animation { get; }
}
