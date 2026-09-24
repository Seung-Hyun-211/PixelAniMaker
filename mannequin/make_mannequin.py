"""128x512 pixel-art mannequin template: front, side (facing left/right), back."""
import math
from pathlib import Path
from PIL import Image, ImageDraw

W, H = 128, 512
OUT = Path(__file__).parent

FILL = (222, 206, 184, 255)
SHADE = (190, 170, 146, 255)
JOINT = (176, 150, 120, 255)
JOINT_SHADE = (150, 126, 98, 255)
INNER = (120, 96, 74, 255)
OUTLINE = (52, 40, 34, 255)


class Figure:
    def __init__(self):
        self.labels = Image.new("I", (W, H), 0)
        self.draw = ImageDraw.Draw(self.labels)
        self.kind = {0: None}
        self.details = []

    def _new(self, kind):
        n = len(self.kind)
        self.kind[n] = kind
        return n

    def poly(self, pts, kind="body", merge=False):
        self.draw.polygon(pts, fill=len(self.kind) - 1 if merge else self._new(kind))

    def ellipse(self, box, kind="body", merge=False):
        self.draw.ellipse(box, fill=len(self.kind) - 1 if merge else self._new(kind))

    def circle(self, c, r, kind="joint"):
        self.ellipse((c[0] - r, c[1] - r, c[0] + r, c[1] + r), kind)

    def capsule(self, p1, r1, p2, r2, kind="body"):
        n = self._new(kind)
        (x1, y1), (x2, y2) = p1, p2
        a = math.atan2(y2 - y1, x2 - x1) + math.pi / 2
        dx, dy = math.cos(a), math.sin(a)
        self.draw.polygon([(x1 + dx * r1, y1 + dy * r1), (x2 + dx * r2, y2 + dy * r2),
                           (x2 - dx * r2, y2 - dy * r2), (x1 - dx * r1, y1 - dy * r1)], fill=n)
        self.draw.ellipse((x1 - r1, y1 - r1, x1 + r1, y1 + r1), fill=n)
        self.draw.ellipse((x2 - r2, y2 - r2, x2 + r2, y2 + r2), fill=n)

    def line(self, pts):
        self.details.append(("line", pts))

    def arc(self, box, start, end):
        self.details.append(("arc", (box, start, end)))

    def render(self, mirror=False):
        lab = self.labels.load()
        img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        px = img.load()
        get = lambda x, y: lab[x, y] if 0 <= x < W and 0 <= y < H else 0
        for y in range(H):
            for x in range(W):
                l = lab[x, y]
                if not l:
                    continue
                nb = [get(x + 1, y), get(x - 1, y), get(x, y + 1), get(x, y - 1)]
                if 0 in nb:
                    px[x, y] = OUTLINE
                elif any(n < l for n in nb):          # this part overlaps the one behind it
                    px[x, y] = INNER
                else:
                    joint = self.kind[l] == "joint"
                    rim = any(get(x + k, y) != l for k in (1, 2, 3)) or get(x, y + 2) != l
                    px[x, y] = (JOINT_SHADE if rim else JOINT) if joint else (SHADE if rim else FILL)
        d = ImageDraw.Draw(img)
        for kind, data in self.details:
            if kind == "line":
                d.line(data, fill=INNER)
            else:
                d.arc(data[0], data[1], data[2], fill=INNER)
        return img.transpose(Image.FLIP_LEFT_RIGHT) if mirror else img


def m(x):  # mirror x around the centre column
    return W - 1 - x


def front_back(back=False):
    f = Figure()
    for s in (lambda x: x, m):                        # legs
        f.capsule((s(51), 245), 13, (s(52), 352), 9)
        f.capsule((s(52), 368), 9, (s(54), 460), 6)
        f.poly([(s(45), 464), (s(62), 464), (s(65), 486), (s(62), 491), (s(44), 491), (s(41), 486)])
        f.circle((s(52), 361), 8)
        f.circle((s(54), 464), 5)
    f.poly([(42, 208), (85, 208), (90, 238), (79, 257), (64, 263), (48, 257), (37, 238)])   # pelvis
    f.poly([(47, 178), (80, 178), (84, 212), (43, 212)])                                  # waist
    f.poly([(36, 88), (91, 88), (91, 110), (86, 150), (81, 184), (46, 184), (41, 150), (36, 110)])  # chest
    f.capsule((64, 64), 7, (64, 88), 8)                                                   # neck
    f.ellipse((44, 16, 83, 70))                                                           # head
    for s in (lambda x: x, m):                        # arms
        f.capsule((s(35), 104), 9, (s(29), 190), 7)
        f.capsule((s(29), 202), 7, (s(25), 280), 5)
        f.poly([(s(19), 288), (s(31), 288), (s(33), 306), (s(29), 322), (s(21), 324), (s(16), 312)])
        f.circle((s(37), 99), 10)
        f.circle((s(29), 196), 6)
        f.circle((s(25), 285), 4)
    if back:
        f.line([(64, 92), (64, 176)])                  # spine
        f.arc((42, 100, 60, 136), 200, 330)            # shoulder blades
        f.arc((67, 100, 85, 136), 210, 340)
        f.line([(64, 228), (64, 256)])                 # glute split
        f.line([(60, 62), (60, 68)]); f.line([(67, 62), (67, 68)])
    else:
        f.line([(63, 20), (63, 66)])                   # face guide cross
        f.line([(46, 45), (81, 45)])
        f.line([(56, 56), (71, 56)])
        f.arc((44, 118, 63, 138), 0, 180)              # chest
        f.arc((64, 118, 83, 138), 0, 180)
        f.line([(63, 186), (63, 196)])                 # navel line
    return f


def side():
    """Facing left (front of the body toward x=0)."""
    f = Figure()
    f.capsule((63, 245), 15, (60, 352), 10)            # thigh
    f.capsule((60, 368), 9, (64, 460), 6)              # calf
    f.ellipse((57, 376, 74, 428), merge=True)          # calf muscle
    f.poly([(58, 462), (72, 460), (74, 486), (71, 491), (38, 491), (35, 486), (44, 476)])  # foot
    f.circle((59, 361), 8)
    f.circle((64, 464), 5)
    f.poly([(48, 208), (80, 208), (89, 234), (84, 258), (53, 258), (44, 238)])   # pelvis
    f.poly([(52, 178), (78, 178), (81, 212), (49, 212)])                         # waist
    f.poly([(49, 88), (80, 88), (85, 110), (83, 150), (78, 184), (52, 184), (46, 152), (41, 118)])  # chest
    f.capsule((67, 64), 8, (65, 88), 9)                # neck
    f.ellipse((43, 16, 88, 66))                        # skull
    f.poly([(46, 44), (66, 44), (64, 70), (54, 73), (46, 65)], merge=True)   # jaw
    f.capsule((65, 104), 9, (67, 190), 7)              # arm
    f.capsule((67, 202), 7, (62, 280), 5)
    f.poly([(56, 288), (68, 288), (71, 304), (67, 322), (59, 324), (55, 310)])
    f.circle((65, 100), 10)
    f.circle((67, 196), 6)
    f.circle((62, 285), 4)
    f.line([(44, 45), (70, 45)])                       # eye line
    f.ellipse((68, 40, 76, 54), "detail")              # ear
    f.line([(43, 116), (50, 116)])                     # chest peak
    f.line([(80, 236), (86, 240)])                     # glute
    return f


views = {
    "front": front_back().render(),
    "side_facing_left": side().render(),
    "side_facing_right": side().render(mirror=True),
    "back": front_back(back=True).render(),
}
sheet = Image.new("RGBA", (W * 4, H), (0, 0, 0, 0))
for i, (name, img) in enumerate(views.items()):
    img.save(OUT / f"mannequin_{name}.png")
    sheet.paste(img, (i * W, 0))
sheet.save(OUT / "mannequin_sheet.png")

# Preview: checker background, 8-head guide lines, 2x nearest-neighbour upscale.
prev = Image.new("RGBA", sheet.size, (245, 245, 245, 255))
g = ImageDraw.Draw(prev)
for i in range(9):
    y = 16 + round(i * 475 / 8)
    g.line([(0, y), (W * 4, y)], fill=(200, 215, 235, 255))
for i in range(1, 4):
    g.line([(i * W, 0), (i * W, H)], fill=(180, 180, 180, 255))
prev.alpha_composite(sheet)
prev.resize((W * 8, H * 2), Image.NEAREST).save(OUT / "mannequin_preview_2x.png")
print("done")
