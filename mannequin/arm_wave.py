"""Arm template + 'hand up, wave left-right' animation baked to a sprite sheet.

Compares two ways of producing frames:
  naive     - each part is drawn once (outline + shading baked in) and the image is rotated
              pixel-by-pixel and stacked.  Tilted pixels / broken outlines.
  corrected - parts store only interior colours.  Each frame:
              1. keyframes are lerped (eased) into a pose
              2. every part is rotated about its joint with RotSprite-style sampling
                 (Scale2x x3 = 8x upscale, sample at the rotated pixel centre)
              3. parts are composited into a label map
              4. a clean 1px outline, inner joint lines and light-from-top-left shading are
                 regenerated on the pixel grid
"""
import math
from pathlib import Path
from PIL import Image, ImageDraw

OUT = Path(__file__).parent / "arm_wave"
OUT.mkdir(exist_ok=True)

# colour indices inside part images
T, FILL, JOINT, BAND = 0, 1, 2, 3
PAL = {FILL: (222, 206, 184), JOINT: (176, 150, 120), BAND: (104, 132, 176)}
SHADE = {FILL: (190, 170, 146), JOINT: (150, 126, 98), BAND: (80, 102, 140)}
INNER = (120, 96, 74)
OUTLINE = (52, 40, 34)
BG = (245, 245, 245)


# ---------------------------------------------------------------- part images
class Part:
    """Pixel image in local space. Pivot (joint) is at local (0,0); rest pose points down (+y)."""

    def __init__(self, name, box, draw_fn, s):
        x0, y0, x1, y1 = [round(v * s) for v in box]
        self.name, self.ox, self.oy = name, -x0, -y0
        self.w, self.h = x1 - x0 + 1, y1 - y0 + 1
        self.img = Image.new("L", (self.w, self.h), T)
        d = ImageDraw.Draw(self.img)
        draw_fn(d, lambda x, y: (x * s + self.ox, y * s + self.oy), s)
        self.px = self.img.load()
        self.up = scale8(self.img)

    def get(self, x, y):
        """Colour index at local pixel coords (floats)."""
        ix, iy = math.floor(x + self.ox), math.floor(y + self.oy)
        if 0 <= ix < self.w and 0 <= iy < self.h:
            return self.px[ix, iy]
        return T

    def get8(self, x, y):
        """Colour index sampled from the Scale2x x3 image (RotSprite sampling)."""
        ix, iy = math.floor((x + self.ox) * 8), math.floor((y + self.oy) * 8)
        w, h = self.up.size
        if 0 <= ix < w and 0 <= iy < h:
            return self.up.getpixel((ix, iy))
        return T


def scale2x(img):
    w, h = img.size
    src, out = img.load(), Image.new("L", (w * 2, h * 2))
    dst = out.load()
    g = lambda x, y: src[min(max(x, 0), w - 1), min(max(y, 0), h - 1)]
    for y in range(h):
        for x in range(w):
            p, a, b, c, d = g(x, y), g(x, y - 1), g(x + 1, y), g(x - 1, y), g(x, y + 1)
            e0 = a if (c == a and c != d and a != b) else p
            e1 = b if (a == b and a != c and b != d) else p
            e2 = c if (d == c and d != b and c != a) else p
            e3 = d if (b == d and b != a and d != c) else p
            dst[2 * x, 2 * y], dst[2 * x + 1, 2 * y] = e0, e1
            dst[2 * x, 2 * y + 1], dst[2 * x + 1, 2 * y + 1] = e2, e3
    return out


def scale8(img):
    return scale2x(scale2x(scale2x(img)))


def cap(d, P, a, ra, b, rb, s, col):
    """Capsule between local points a and b (vertical)."""
    ra, rb = ra * s, rb * s
    (ax, ay), (bx, by) = P(*a), P(*b)
    d.polygon([(ax - ra, ay), (bx - rb, by), (bx + rb, by), (ax + ra, ay)], fill=col)
    d.ellipse((ax - ra, ay - ra, ax + ra, ay + ra), fill=col)
    d.ellipse((bx - rb, by - rb, bx + rb, by + rb), fill=col)


def disc(d, P, c, r, s, col=JOINT):
    x, y = P(*c)
    r = r * s
    d.ellipse((x - r, y - r, x + r, y + r), fill=col)


def make_parts(s):
    """Arm from the chibi template (128x256 design units), scaled by s."""
    def upper(d, P, s):
        cap(d, P, (0, 0), 6, (0, 34), 5, s, FILL)

    def fore(d, P, s):
        cap(d, P, (0, 4), 5, (0, 26), 4, s, FILL)
        x0, y0 = P(-6, 19)
        x1, y1 = P(6, 23)
        d.rectangle((x0, y0, x1, y1), fill=BAND)            # wristband: shows details rotate too
        d.polygon([P(-5, 23), P(5, 23), P(4, 27), P(-4, 27)], fill=FILL)

    def hand(d, P, s):
        d.polygon([P(-5, 1), P(5, 1), P(7, 11), P(4, 19), P(-3, 20), P(-7, 13)], fill=FILL)
        x, y = P(7, 7)
        r = 3 * s
        d.ellipse((x - r, y - r, x + r, y + r), fill=FILL)  # thumb

    def jd(r):
        return lambda d, P, s: disc(d, P, (0, 0), r, s)

    return {
        "upper_arm": Part("upper_arm", (-8, -8, 8, 41), upper, s),
        "forearm": Part("forearm", (-7, 0, 9, 29), fore, s),
        "hand": Part("hand", (-8, 0, 11, 21), hand, s),
        "shoulder": Part("shoulder", (-8, -8, 8, 8), jd(7), s),
        "elbow": Part("elbow", (-6, -6, 6, 6), jd(5), s),
        "wrist": Part("wrist", (-4, -4, 4, 4), jd(3), s),
    }


# ---------------------------------------------------------------- skeleton
# (name, parent, joint position in parent's local space [design units], draw order)
SKELETON = [
    ("upper_arm", None, (0, 0), 0),
    ("forearm", "upper_arm", (0, 38), 1),
    ("hand", "forearm", (0, 29), 2),
    ("shoulder", "upper_arm", (0, 0), 3),
    ("elbow", "forearm", (0, 0), 4),
    ("wrist", "hand", (0, 0), 5),
]


def ease(t):
    return t * t * (3 - 2 * t)


def lerp_keys(keys, f):
    """keys: [(frame, value)] -> eased lerp at frame f."""
    for (f0, v0), (f1, v1) in zip(keys, keys[1:]):
        if f0 <= f <= f1:
            return v0 + (v1 - v0) * ease((f - f0) / (f1 - f0))
    return keys[-1][1]


FRAMES = 8
# relative joint angles in degrees (0 = hanging down, positive = clockwise on screen)
KEYS = {
    "upper_arm": [(0, 197), (4, 203), (8, 197)],      # raised, tilted outward, slight sway
    "forearm": [(0, -45), (4, 5), (8, -45)],          # forearm up, swinging left <-> right
    "hand": [(0, 15), (4, -15), (8, 15)],              # wrist lags behind (follow-through)
}


def pose(frame):
    return {n: lerp_keys(KEYS[n], frame) if n in KEYS else 0 for n, *_ in SKELETON}


def world_transforms(angles, root, s):
    """-> {part: (jx, jy, theta_rad)}"""
    wt = {}
    for name, parent, joint, _ in SKELETON:
        if parent is None:
            jx, jy, th = root[0], root[1], 0.0
        else:
            px, py, pth = wt[parent]
            lx, ly = joint[0] * s, joint[1] * s
            c, sn = math.cos(pth), math.sin(pth)
            jx, jy, th = px + c * lx - sn * ly, py + sn * lx + c * ly, pth
        wt[name] = (jx, jy, th + math.radians(angles.get(name, 0)))
    return wt


# ---------------------------------------------------------------- rendering
def to_local(wt, x, y):
    jx, jy, th = wt
    dx, dy = x + 0.5 - jx, y + 0.5 - jy
    c, s = math.cos(-th), math.sin(-th)
    return c * dx - s * dy, s * dx + c * dy


def render_corrected(parts, wt, W, H):
    labels = [[0] * W for _ in range(H)]
    colors = [[T] * W for _ in range(H)]
    for name, _, _, order in sorted(SKELETON, key=lambda r: r[3]):
        part = parts[name]
        for y in range(H):
            for x in range(W):
                c = part.get8(*to_local(wt[name], x, y))
                if c != T:
                    labels[y][x], colors[y][x] = order + 1, c
    img = Image.new("RGB", (W, H), BG)
    px = img.load()
    L = lambda x, y: labels[y][x] if 0 <= x < W and 0 <= y < H else 0
    for y in range(H):
        for x in range(W):
            l = labels[y][x]
            if not l:
                continue
            nb = [L(x + 1, y), L(x - 1, y), L(x, y + 1), L(x, y - 1)]
            if 0 in nb:
                px[x, y] = OUTLINE
            elif any(n < l for n in nb):
                px[x, y] = INNER
            else:
                rim = L(x + 1, y) != l or L(x + 2, y) != l or L(x, y + 2) != l
                px[x, y] = (SHADE if rim else PAL)[colors[y][x]]
    return img


def bake_rest(part):
    """Rest-pose image with outline + shading baked in (what a naive editor would store)."""
    img = Image.new("RGBA", (part.w, part.h), (0, 0, 0, 0))
    px = img.load()
    g = lambda x, y: part.px[x, y] if 0 <= x < part.w and 0 <= y < part.h else T
    for y in range(part.h):
        for x in range(part.w):
            c = g(x, y)
            if c == T:
                continue
            if T in (g(x + 1, y), g(x - 1, y), g(x, y + 1), g(x, y - 1)):
                px[x, y] = OUTLINE + (255,)
            else:
                rim = g(x + 1, y) == T or g(x + 2, y) == T
                px[x, y] = (SHADE if rim else PAL)[c] + (255,)
    return img


def render_naive(parts, baked, wt, W, H):
    img = Image.new("RGB", (W, H), BG)
    px = img.load()
    for name, _, _, _ in sorted(SKELETON, key=lambda r: r[3]):
        part, b = parts[name], baked[name].load()
        for y in range(H):
            for x in range(W):
                lx, ly = to_local(wt[name], x, y)
                ix, iy = math.floor(lx + part.ox), math.floor(ly + part.oy)
                if 0 <= ix < part.w and 0 <= iy < part.h and b[ix, iy][3]:
                    px[x, y] = b[ix, iy][:3]
    return img


# ---------------------------------------------------------------- output
def build(s, cell_w, cell_h, root):
    parts = make_parts(s)
    baked = {n: bake_rest(p) for n, p in parts.items()}
    corrected, naive = [], []
    for f in range(FRAMES):
        wt = world_transforms(pose(f), root, s)
        corrected.append(render_corrected(parts, wt, cell_w, cell_h))
        naive.append(render_naive(parts, baked, wt, cell_w, cell_h))
    return parts, corrected, naive


def sheet(frames):
    w, h = frames[0].size
    out = Image.new("RGB", (w * len(frames), h))
    for i, f in enumerate(frames):
        out.paste(f, (i * w, 0))
    return out


def gif(frames, path, k):
    big = [f.resize((f.width * k, f.height * k), Image.NEAREST) for f in frames]
    big[0].save(path, save_all=True, append_images=big[1:], duration=100, loop=0)


results = {}
for label, s, cw, ch, root in [("64x128", 0.5, 32, 56, (12, 50)), ("128x256", 1.0, 64, 112, (24, 100))]:
    parts, corrected, naive = build(s, cw, ch, root)
    sheet(corrected).save(OUT / f"arm_wave_{label}_sheet.png")
    sheet(naive).save(OUT / f"arm_wave_{label}_naive_sheet.png")
    gif(corrected, OUT / f"arm_wave_{label}.gif", 512 // ch)
    results[label] = (corrected, naive)
    if s == 1.0:
        for n, p in parts.items():
            p.img.point(lambda v: 255 if v else 0).save(OUT / f"part_{n}_mask.png")

# comparison image: per scale, row 1 = naive, row 2 = corrected, upscaled
rows = []
for label, (corrected, naive) in results.items():
    k = 224 // corrected[0].height
    for name, frames in (("naive (rotate + stack)", naive), ("corrected (bake)", corrected)):
        rows.append((f"{label}  {name}", sheet(frames).resize(
            (corrected[0].width * FRAMES * k, corrected[0].height * k), Image.NEAREST)))
pad, lh = 16, 18
W = max(r.width for _, r in rows) + pad * 2
H = sum(r.height + lh + pad for _, r in rows) + pad
cmp_img = Image.new("RGB", (W, H), (255, 255, 255))
d = ImageDraw.Draw(cmp_img)
y = pad
for text, r in rows:
    d.text((pad, y), text, fill=(40, 40, 40))
    cmp_img.paste(r, (pad, y + lh))
    y += r.height + lh + pad
cmp_img.save(OUT / "arm_wave_compare.png")
print("done")
