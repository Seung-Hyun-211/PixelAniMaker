using System.Numerics;
using PixelAniMaker.Core.Imaging;

namespace PixelAniMaker.Core.Rigging.Body;

/// <summary>A generated mannequin: the skeleton spec and its part images by file name.</summary>
public sealed record MannequinTemplate(CharacterSpec Spec, IReadOnlyDictionary<string, RgbaImage> Images)
{
    public Character Build(Palette? palette = null) => Spec.Build(file => Images[file], palette);
}

/// <summary>
/// Draws a mannequin of any <see cref="BodyProportions"/> with the same 16 parts (names, labels, parents)
/// as the built-in 4-head template, so the default clips and the eye, bust and added parts work on it.
/// The body is laid out in 3D (x: towards the character's left, y: down, z: forward) and every stored
/// direction is a projection of it: front 0°, front-left 45°, left 90°, back-left 135°, back 180°.
/// </summary>
public static class MannequinBuilder
{
    private static readonly (Direction Direction, double Angle)[] Views =
        [(Direction.Front, 0), (Direction.Left, 90), (Direction.Back, 180), (Direction.FrontLeft, 45), (Direction.BackLeft, 135)];

    /// <summary>Empty rows below the feet (and at least as many above the head) on the template canvas.</summary>
    public const int Margin = 4;

    /// <summary>Back to front when seen from the front or back.</summary>
    private static readonly string[] FrontOrder =
    [
        "thigh_r", "thigh_l", "shin_r", "shin_l", "foot_r", "foot_l", "pelvis", "waist", "chest", "head",
        "upper_arm_r", "forearm_r", "hand_r", "upper_arm_l", "forearm_l", "hand_l",
    ];

    /// <summary>Back to front when turned to screen-left: the far (right) arm and leg behind the body.</summary>
    private static readonly string[] SideOrder =
    [
        "upper_arm_r", "forearm_r", "hand_r", "thigh_r", "shin_r", "foot_r", "thigh_l", "shin_l", "foot_l",
        "pelvis", "waist", "chest", "head", "upper_arm_l", "forearm_l", "hand_l",
    ];

    private static readonly string[] Torso = ["pelvis", "waist", "chest", "head"];

    private static readonly Dictionary<string, (string Label, string? Parent)> Bones = new()
    {
        ["pelvis"] = ("골반", null), ["waist"] = ("허리", "pelvis"), ["chest"] = ("가슴", "waist"), ["head"] = ("머리", "chest"),
        ["thigh"] = ("허벅지", "pelvis"), ["shin"] = ("종아리", "thigh"), ["foot"] = ("발", "shin"),
        ["upper_arm"] = ("상완", "chest"), ["forearm"] = ("전완", "upper_arm"), ["hand"] = ("손", "forearm"),
    };

    public static MannequinTemplate Build(BodyProportions proportions, bool jointDiscs = true)
    {
        var body = new BodyLayout(proportions, jointDiscs);
        var images = new Dictionary<string, RgbaImage>();
        var views = FrontOrder.ToDictionary(n => n, _ => new Dictionary<string, PartViewSpec>());
        foreach (var (direction, angle) in Views)
        {
            var view = new View(angle, body.Width / 2f);
            var order = view.SideOn ? SideOrder : FrontOrder;
            foreach (var name in FrontOrder)
            {
                var raster = new LabelRaster(body.Width, body.Height);
                foreach (var (shape, joint) in body.Shapes(name, view))
                    raster.Add(shape, joint);
                var (image, x, y) = Crop(raster.Render(body.RimWidth), name == "head" ? 6 : 2);
                string file = CharacterSpec.ImagePath(direction, name);
                images[file] = image;
                var pivot = view.Project(body.Pivot(name));
                views[name][direction.Key()] = new PartViewSpec(x, y, pivot.X, pivot.Y, Array.IndexOf(order, name), file);
            }
        }
        var parts = FrontOrder.Select(name =>
        {
            var (bone, side) = Split(name);
            var (label, parent) = Bones[bone];
            if (parent is not null && !Torso.Contains(parent))
                parent += "_" + side;
            return new PartSpec(name, side is null ? label : $"{label} {side.ToUpperInvariant()}", parent, views[name]);
        }).ToList();
        return new MannequinTemplate(new CharacterSpec(body.Width, body.Height, parts), images);
    }

    private static (string Bone, string? Side) Split(string name) =>
        name.EndsWith("_r") || name.EndsWith("_l") ? (name[..^2], name[^1..]) : (name, null);

    /// <summary>The drawn pixels plus <paramref name="pad"/> around them, and where that crop sits.</summary>
    private static (RgbaImage Image, int X, int Y) Crop(RgbaImage full, int pad)
    {
        int x0 = full.Width, y0 = full.Height, x1 = -1, y1 = -1;
        for (int y = 0; y < full.Height; y++)
            for (int x = 0; x < full.Width; x++)
                if (!full.Pixels[y * full.Width + x].IsTransparent)
                    (x0, y0, x1, y1) = (Math.Min(x0, x), Math.Min(y0, y), Math.Max(x1, x), Math.Max(y1, y));
        if (x1 < 0)
            return (RgbaImage.Blank(1, 1), 0, 0);
        x0 = Math.Max(0, x0 - pad);
        y0 = Math.Max(0, y0 - pad);
        x1 = Math.Min(full.Width - 1, x1 + pad);
        y1 = Math.Min(full.Height - 1, y1 + pad);
        var crop = RgbaImage.Blank(x1 - x0 + 1, y1 - y0 + 1);
        for (int y = y0; y <= y1; y++)
            Array.Copy(full.Pixels, y * full.Width + x0, crop.Pixels, (y - y0) * crop.Width, crop.Width);
        return (crop, x0, y0);
    }

    /// <summary>A viewing angle: 0° sees the front, 90° the character's left side (facing screen-left).</summary>
    internal sealed class View(double degrees, float centreX)
    {
        private readonly float _cos = (float)Math.Round(Math.Cos(degrees * Math.PI / 180), 6);
        private readonly float _sin = (float)Math.Round(Math.Sin(degrees * Math.PI / 180), 6);

        /// <summary>Turned more than a little: the far arm and leg go behind the body.</summary>
        public bool SideOn => _sin > 0.3f;

        public float ScreenX(float x, float z) => centreX + x * _cos - z * _sin;

        public Vector2 Project(Vector3 p) => new(ScreenX(p.X, p.Z), p.Y);

        /// <summary>Towards the viewer.</summary>
        public float Depth(Vector3 p) => p.X * _sin + p.Z * _cos;
    }
}
