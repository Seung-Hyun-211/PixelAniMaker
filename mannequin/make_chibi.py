"""4-head (chibi) front mannequin, rendered at several texture sizes for comparison.

Geometry is authored in a 128x256 design space and scaled per output size.
"""
import math
from pathlib import Path
from PIL import Image, ImageDraw

OUT = Path(__file__).parent
BASE_W, BASE_H = 128, 256
SIZES = [(32, 64), (64, 128), (128, 256)]

FILL = (222, 206, 184, 255)
SHADE = (190, 170, 146, 255)
JOINT = (176, 150, 120, 255)
JOINT_SHADE = (150, 126, 98, 255)
INNER = (120, 96, 74, 255)
OUTLINE = (52, 40, 34, 255)


class Figure:
    def __init__(self, w, h, scale=None):
        """scale: design units -> pixels (default: fit the 128-wide design space to w)."""
        self.w, self.h, self.s = w, h, scale if scale is not None else w / BASE_W
        self.labels = Image.new("I", (w, h), 0)
        self.draw = ImageDraw.Draw(self.labels)
        self.kind = {0: None}
        self.details = []

    def _p(self, pts):
        return [(x * self.s, y * self.s) for x, y in pts]

    def _new(self, kind):
        n = len(self.kind)
        self.kind[n] = kind
        return n

    def _ellipse(self, cx, cy, r, n):
        r = max(r * self.s, 0.5)
        cx, cy = cx * self.s, cy * self.s
        self.draw.ellipse((cx - r, cy - r, cx + r, cy + r), fill=n)

    def poly(self, pts, kind="body"):
        self.draw.polygon(self._p(pts), fill=self._new(kind))

    def smooth_poly(self, pts, kind="body", ss=8):
        """Polygon rasterised at ss× and kept where at least half of a pixel is covered — no stray
        one-pixel spikes on curved outlines."""
        n = self._new(kind)
        big = Image.new("L", (self.w * ss, self.h * ss), 0)
        ImageDraw.Draw(big).polygon([(x * self.s * ss, y * self.s * ss) for x, y in pts], fill=255)
        mask = big.resize((self.w, self.h), Image.BOX).point(lambda v: 255 if v >= 128 else 0)
        self.labels.paste(n, (0, 0), mask)

    def ellipse(self, box, kind="body"):
        x0, y0, x1, y1 = box
        s = self.s
        self.draw.ellipse((x0 * s, y0 * s, x1 * s, y1 * s), fill=self._new(kind))

    def circle(self, c, r, kind="joint"):
        self._ellipse(c[0], c[1], r, self._new(kind))

    def capsule(self, p1, r1, p2, r2, kind="body"):
        n = self._new(kind)
        (x1, y1), (x2, y2) = p1, p2
        a = math.atan2(y2 - y1, x2 - x1) + math.pi / 2
        dx, dy = math.cos(a), math.sin(a)
        self.draw.polygon(self._p([(x1 + dx * r1, y1 + dy * r1), (x2 + dx * r2, y2 + dy * r2),
                                   (x2 - dx * r2, y2 - dy * r2), (x1 - dx * r1, y1 - dy * r1)]), fill=n)
        self._ellipse(x1, y1, r1, n)
        self._ellipse(x2, y2, r2, n)

    def line(self, pts, min_scale=0):
        if self.s >= min_scale:
            self.details.append(self._p(pts))

    def render(self):
        w, h = self.w, self.h
        lab = self.labels.load()
        img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        px = img.load()
        get = lambda x, y: lab[x, y] if 0 <= x < w and 0 <= y < h else 0
        rim_w = max(1, round(3 * self.s))
        for y in range(h):
            for x in range(w):
                l = lab[x, y]
                if not l:
                    continue
                nb = [get(x + 1, y), get(x - 1, y), get(x, y + 1), get(x, y - 1)]
                if 0 in nb:
                    px[x, y] = OUTLINE
                elif any(n < l for n in nb):
                    px[x, y] = INNER
                else:
                    joint = self.kind[l] == "joint"
                    rim = any(get(x + k, y) != l for k in range(1, rim_w + 1))
                    px[x, y] = (JOINT_SHADE if rim else JOINT) if joint else (SHADE if rim else FILL)
        d = ImageDraw.Draw(img)
        for pts in self.details:
            d.line(pts, fill=INNER)
        return img


def m(x):
    return BASE_W - 1 - x


# ---------------------------------------------------------------- head
# Head = 60 design px tall (top y 8, chin y 68), 4-head proportions.
# Round skull, cheeks tapering to a soft chin, ears at eye level (after the chibi reference sheet).

def smooth_closed(pts, steps=8):
    """Closed Catmull-Rom curve through the control points (for organic outlines)."""
    out, n = [], len(pts)
    for i in range(n):
        p0, p1, p2, p3 = pts[i - 1], pts[i], pts[(i + 1) % n], pts[(i + 2) % n]
        for k in range(steps):
            t = k / steps
            out.append(tuple(0.5 * (2 * p1[j] + (-p0[j] + p2[j]) * t + (2 * p0[j] - 5 * p1[j] + 4 * p2[j] - p3[j]) * t * t
                                    + (-p0[j] + 3 * p1[j] - 3 * p2[j] + p3[j]) * t ** 3) for j in (0, 1)))
    return out


# front: (half width, y) from the crown down to the chin — round skull widest at ear level, jaw narrowing
HEAD_FRONT = [(0, 8), (14.5, 11.9), (25.1, 22.5), (29, 37), (27.5, 46), (23.5, 54.5), (17, 61.5), (9, 66.5), (0, 68)]

# side, facing screen-left: (dx, y) clockwise from the chin — nose/mouth in front, skull bulging to the back
HEAD_SIDE = [(-20, 65), (-24, 61.5), (-25, 57), (-26.5, 53), (-26, 49), (-29, 46), (-27, 41.5), (-27.5, 34),
             (-25.5, 24), (-20, 15.5), (-11, 9.5), (0, 7.5), (11, 8.5), (21, 14), (27.5, 24), (30, 36), (28, 46.5),
             (22, 55), (11, 60.5), (-4, 62.5), (-12, 64.5)]


def head_front(f, cx, ears=True):
    """Front (or back) head; ears are drawn first so the head's edge draws the line between them."""
    if ears:
        for s in (-1, 1):
            f.ellipse((cx + s * 29 - 5, 36, cx + s * 29 + 5, 50))
    half = [(cx + dx, y) for dx, y in HEAD_FRONT]
    f.smooth_poly(smooth_closed(half + [(cx - dx, y) for dx, y in reversed(HEAD_FRONT[1:-1])]))


def head_side(f, cx):
    """Head facing screen-left, with the ear drawn over it so it shows as an inner line."""
    f.smooth_poly(smooth_closed([(cx + dx, y) for dx, y in HEAD_SIDE]))
    f.ellipse((cx + 3, 36, cx + 12, 50))


def chibi_front(w, h):
    """Head = 60 design px; figure spans y 8..249 (4 heads)."""
    f = Figure(w, h)
    for s in (lambda x: x, m):                                   # legs
        f.capsule((s(53), 140), 10, (s(54), 190), 8)
        f.capsule((s(54), 200), 8, (s(55), 235), 5)
        f.poly([(s(47), 236), (s(62), 236), (s(64), 246), (s(62), 249), (s(46), 249), (s(44), 246)])
        f.circle((s(54), 195), 6)
        f.circle((s(55), 238), 4)
    f.poly([(45, 124), (82, 124), (85, 140), (75, 150), (64, 153), (52, 150), (42, 140)])  # pelvis
    f.poly([(47, 110), (80, 110), (81, 126), (46, 126)])                                  # waist
    f.poly([(44, 74), (83, 74), (85, 86), (81, 112), (46, 112), (42, 86)])                # chest
    f.capsule((64, 62), 6, (64, 76), 6)                                                   # neck
    head_front(f, 63.5)                                                                   # head
    for s in (lambda x: x, m):                                   # arms
        f.capsule((s(41), 84), 6, (s(37), 118), 5)
        f.capsule((s(37), 126), 5, (s(35), 141), 4)
        f.poly([(s(30), 145), (s(40), 145), (s(42), 155), (s(39), 163), (s(32), 164), (s(28), 157)])
        f.circle((s(43), 80), 7)
        f.circle((s(37), 122), 5)
        f.circle((s(35), 144), 3)
    f.line([(63, 12), (63, 64)], min_scale=0.5)                  # face guide: centre
    f.line([(37, 42), (90, 42)], min_scale=0.5)                  # eye line (lower half for chibi)
    f.line([(63, 128), (63, 134)], min_scale=1)                  # navel
    return f


def main():
    # Individual textures + a comparison sheet (each upscaled to 512 px tall, 4-head guides).
    DISPLAY_H = 512
    panels = []
    for w, h in SIZES:
        img = chibi_front(w, h).render()
        img.save(OUT / f"chibi_front_{w}x{h}.png")
        k = DISPLAY_H // h
        panel = Image.new("RGBA", (w * k, DISPLAY_H), (245, 245, 245, 255))
        g = ImageDraw.Draw(panel)
        for i in range(5):                                   # head divisions
            y = round((8 + i * 60.25) / BASE_H * DISPLAY_H)
            g.line([(0, y), (w * k, y)], fill=(200, 215, 235, 255))
        for x in range(0, w * k, k):                         # pixel grid
            g.line([(x, 0), (x, DISPLAY_H)], fill=(236, 236, 236, 255))
        for y in range(0, DISPLAY_H, k):
            g.line([(0, y), (w * k, y)], fill=(236, 236, 236, 255))
        panel.alpha_composite(img.resize((w * k, DISPLAY_H), Image.NEAREST))
        panels.append((panel, f"{w}x{h}  (x{k})"))

    gap, label_h = 24, 24
    sheet = Image.new("RGBA", (sum(p.width for p, _ in panels) + gap * (len(panels) + 1),
                               DISPLAY_H + label_h + gap), (255, 255, 255, 255))
    d = ImageDraw.Draw(sheet)
    x = gap
    for panel, label in panels:
        sheet.paste(panel, (x, label_h))
        d.text((x, 6), label, fill=(60, 60, 60, 255))
        x += panel.width + gap
    sheet.save(OUT / "chibi_size_compare.png")
    print("done")


if __name__ == "__main__":
    main()
