using PixelAniMaker.Core.History;
using PixelAniMaker.Core.Project;
using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.Core.Tests;

public class RotationLimitTests
{
    [Fact]
    public void Clamping_keeps_allowed_angles_and_otherwise_takes_the_nearer_end()
    {
        var limit = new RotationLimit(-30, 150);
        Assert.Equal(40, limit.Clamp(40));
        Assert.Equal(150, limit.Clamp(170));
        Assert.Equal(-30, limit.Clamp(-60));
        Assert.Equal(150, limit.Clamp(-170));    // 40° from 150 going round, 140° from -30
        Assert.Equal(150, limit.Clamp(-200));    // = 160°
        Assert.True(limit.Contains(390));        // = 30°
        Assert.Equal((-10.0, 20.0), (new RotationLimit(20, -10).Min, new RotationLimit(20, -10).Max));
    }

    [Fact]
    public void A_part_without_a_limit_turns_freely()
    {
        var part = GoldenTests.Build().Character.Find("arm_r")!;
        Assert.Null(part.Limit);
        Assert.Equal(-170, part.ClampRotation(190));
    }

    [Fact]
    public void Limits_are_undoable_and_saved_only_when_set()
    {
        var project = GoldenTests.Build();
        var c = project.Character;
        var arm = c.Find("arm_r")!;
        Assert.DoesNotContain("\"limit\"", CharacterSpec.From(c).ToJson());

        var history = new UndoHistory();
        RotationLimitChange.Apply(arm, history, new RotationLimit(-45, 120));
        Assert.Equal(120, arm.ClampRotation(135));
        history.Undo();
        Assert.Null(arm.Limit);
        history.Redo();

        using var ms = new MemoryStream();
        ProjectFile.Save(project, ms, new RawCodec());
        ms.Position = 0;
        var loaded = ProjectFile.Load(ms, new RawCodec()).Character;
        Assert.Equal(new RotationLimit(-45, 120), loaded.Find("arm_r")!.Limit);
        Assert.Null(loaded.Find("arm_l")!.Limit);
    }
}
