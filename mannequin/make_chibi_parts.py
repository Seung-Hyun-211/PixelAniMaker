"""Splits the chibi mannequin into 16 skeleton parts for the app, for the front, left and back views.

Writes one PNG per part and view plus skeleton.json into the app's template assets.
Coordinates are design units (2 per canvas pixel) on a 192x256 design canvas = 96x128 pixels.
The right view is not generated: the app mirrors the left view.

Side suffixes are anatomical: *_r is the character's right arm/leg.
  front: *_r on the viewer's left      back: *_r on the viewer's right
  left (facing screen-left): the character's left side faces the viewer, so *_l is the near limb.
"""
import json
from pathlib import Path

from make_chibi import Figure, head_front, head_side

W, H = 96, 128
SCALE = 0.5
CX = 96                                # design x of the body centre line
TEMPLATES = Path(__file__).parent.parent / "src/PixelAniMaker.App/Assets/Templates"
OUT = TEMPLATES / "chibi96"
JOINT_DISCS = True     # False builds the plain mannequin (no ball joints), see main()

LABELS = {
    "pelvis": "골반", "waist": "허리", "chest": "가슴", "head": "머리",
    "thigh": "허벅지", "shin": "종아리", "foot": "발",
    "upper_arm": "상완", "forearm": "전완", "hand": "손",
}
PARENTS = {
    "pelvis": None, "waist": "pelvis", "chest": "waist", "head": "chest",
    "thigh": "pelvis", "shin": "thigh", "foot": "shin",
    "upper_arm": "chest", "forearm": "upper_arm", "hand": "forearm",
}
PADDING = {"head": 6}                  # extra room for hair and accessories; others get 2


def ident(x):
    return x


def disc(f, centre, r):
    """Ball joint circle; skipped for the plain mannequin."""
    if JOINT_DISCS:
        f.circle(centre, r)


def mirror(x):
    return 2 * CX - 1 - x


def shift(x, dx):
    return lambda v: x(v) + dx


# ---------------------------------------------------------------- shapes
# Each builder returns {part: (joint, draw)}; joint in design units, draw(fig) adds the shapes.
# Front/back shapes come from make_chibi.chibi_front (authored at centre 64, shifted by +32).

def front_torso():
    o = CX - 64
    return {
        "pelvis": ((64 + o, 130), lambda f: f.poly([(45 + o, 124), (82 + o, 124), (85 + o, 140), (75 + o, 150), (64 + o, 153), (52 + o, 150), (42 + o, 140)])),
        "waist": ((64 + o, 125), lambda f: f.poly([(47 + o, 110), (80 + o, 110), (81 + o, 126), (46 + o, 126)])),
        "chest": ((64 + o, 111), lambda f: f.poly([(44 + o, 74), (83 + o, 74), (85 + o, 86), (81 + o, 112), (46 + o, 112), (42 + o, 86)])),
        "head": ((64 + o, 74), lambda f: (f.capsule((64 + o, 62), 6, (64 + o, 76), 6), head_front(f, 63.5 + o))),
    }


def front_limbs(s):
    """One side's limbs; s maps an x authored for the viewer-left side (centre 64) to the canvas."""
    return {
        "thigh": ((s(53), 138), lambda f: f.capsule((s(53), 140), 10, (s(54), 190), 8)),
        "shin": ((s(54), 195), lambda f: (f.capsule((s(54), 200), 8, (s(55), 235), 5), disc(f, (s(54), 195), 6))),
        "foot": ((s(55), 238), lambda f: (f.poly([(s(47), 236), (s(62), 236), (s(64), 246), (s(62), 249), (s(46), 249), (s(44), 246)]),
                                          disc(f, (s(55), 238), 4))),
        "upper_arm": ((s(43), 80), lambda f: (f.capsule((s(41), 84), 6, (s(37), 118), 5), disc(f, (s(43), 80), 7))),
        "forearm": ((s(37), 122), lambda f: (f.capsule((s(37), 126), 5, (s(35), 141), 4), disc(f, (s(37), 122), 5))),
        "hand": ((s(35), 144), lambda f: (f.poly([(s(30), 145), (s(40), 145), (s(42), 155), (s(39), 163), (s(32), 164), (s(28), 157)]),
                                          disc(f, (s(35), 144), 3))),
    }


def side_torso():
    c = CX
    return {
        "pelvis": ((c, 130), lambda f: f.poly([(c - 16, 124), (c + 18, 124), (c + 22, 140), (c + 14, 152), (c - 12, 152), (c - 18, 140)])),
        "waist": ((c, 125), lambda f: f.poly([(c - 14, 110), (c + 14, 110), (c + 15, 126), (c - 15, 126)])),
        "chest": ((c, 111), lambda f: f.poly([(c - 18, 74), (c + 14, 74), (c + 16, 86), (c + 14, 112), (c - 14, 112), (c - 20, 90)])),
        "head": ((c, 74), lambda f: (f.capsule((c + 1, 62), 6, (c, 76), 6), head_side(f, c))),
    }


def side_limbs(dx):
    """Limbs seen from the side, facing screen-left; dx pushes the far limbs slightly back."""
    c = CX + dx
    return {
        "thigh": ((c, 138), lambda f: f.capsule((c, 140), 10, (c, 190), 8)),
        "shin": ((c, 195), lambda f: (f.capsule((c, 200), 8, (c + 1, 235), 5), disc(f, (c, 195), 6))),
        "foot": ((c + 1, 238), lambda f: (f.poly([(c - 16, 238), (c + 6, 236), (c + 8, 246), (c + 6, 249), (c - 16, 249), (c - 18, 245)]),
                                          disc(f, (c + 1, 238), 4))),
        "upper_arm": ((c + 1, 80), lambda f: (f.capsule((c + 1, 84), 6, (c + 1, 118), 5), disc(f, (c + 1, 80), 7))),
        "forearm": ((c + 1, 122), lambda f: (f.capsule((c + 1, 126), 5, (c, 141), 4), disc(f, (c + 1, 122), 5))),
        "hand": ((c, 144), lambda f: (f.poly([(c - 4, 145), (c + 5, 145), (c + 6, 155), (c + 3, 163), (c - 3, 164), (c - 6, 156)]),
                                      disc(f, (c, 144), 3))),
    }


# ---------------------------------------------------------------- 3/4 (facing screen-left)
# The body turns about its centre line: x positions on the near side move in to K_NEAR of their front
# distance, on the far side to K_FAR; limb radii stay. Front-left: the far side is screen-left (_r);
# back-left is seen from behind, so the far side is screen-right (_r there too).
K_NEAR, K_FAR = 0.85, 0.6
C3 = CX - 0.5                          # design centre line (mirror x -> 2*CX-1-x)


def turn(x, far_left):
    k = (K_FAR if x < C3 else K_NEAR) if far_left else (K_NEAR if x < C3 else K_FAR)
    return C3 + (x - C3) * k


def head_three_quarter(f, cx, back=False):
    """3/4 head: each row halfway between the front head (no ears) and the side head, so the chin and
    cheek move towards the face side and the skull stays round; one ear, drawn over it like the side."""
    def spans(draw):
        g = Figure(2 * CX, 256, 1.0)
        draw(g)
        img = g.render()
        rows = {}
        for y in range(img.height):
            xs = [x for x in range(img.width) if img.getpixel((x, y))[3]]
            if xs:
                rows[y] = (min(xs), max(xs) + 1)
        return rows
    front = spans(lambda g: head_front(g, cx, ears=False))
    side = spans(lambda g: head_side(g, cx + 0.5))
    bottom = max(front)
    ys = [y for y in sorted(set(front) | set(side)) if y <= bottom]
    blend = {y: tuple((a + b) / 2 for a, b in zip(front.get(y, side.get(y)), side.get(y, front.get(y)))) for y in ys}
    smooth = {}
    for y in ys:       # 3-row average keeps the outline smooth
        near = [blend[k] for k in (y - 1, y, y + 1) if k in blend]
        smooth[y] = (sum(v[0] for v in near) / len(near), sum(v[1] for v in near) / len(near))
    left = [(smooth[y][0], y) for y in ys]
    right = [(smooth[y][1], y) for y in reversed(ys)]
    f.smooth_poly(left + right)
    # the near ear: from the front it sits behind the cheek (halfway between the front and side ear),
    # from behind it is the left ear, halfway between its back-view and side-view places
    ear = cx - 10.75 if back else cx + 18.25
    f.ellipse((ear - 4.5, 36, ear + 4.5, 50))


def view_three_quarter(back):
    far_left = not back
    t = lambda x: turn(x, far_left)
    base = shift(ident, CX - 64)
    left_side = lambda x: t(base(x))
    right_side = lambda x: t(mirror(x + CX - 64))
    toe = -3                           # feet point a little towards screen-left
    limbs = {}
    for side, s in ((("r", left_side), ("l", right_side)) if not back else (("l", left_side), ("r", right_side))):
        for name, (joint, draw) in front_limbs(s).items():
            if name == "foot":
                joint = (joint[0] + toe, joint[1])
                draw = (lambda s: lambda f: (f.poly([(s(47) + toe, 236), (s(62) + toe, 236), (s(64) + toe, 246), (s(62) + toe, 249),
                                                      (s(46) + toe, 249), (s(44) + toe, 246)]),
                                             disc(f, (s(55) + toe, 238), 4)))(s)
            limbs[f"{name}_{side}"] = (joint, draw)
    o = CX - 64
    torso = {
        "pelvis": ((C3, 130), lambda f: f.poly([(t(x + o), y) for x, y in [(45, 124), (82, 124), (85, 140), (75, 150), (64, 153), (52, 150), (42, 140)]])),
        "waist": ((C3, 125), lambda f: f.poly([(t(x + o), y) for x, y in [(47, 110), (80, 110), (81, 126), (46, 126)]])),
        "chest": ((C3, 111), lambda f: f.poly([(t(x + o) - (2 if x < 64 and not back else 0), y)
                                                for x, y in [(44, 74), (83, 74), (85, 86), (81, 112), (46, 112), (42, 86)]])),
        "head": ((64 + o, 74), lambda f: (f.capsule((64 + o, 62), 6, (64 + o, 76), 6), head_three_quarter(f, 63.5 + o, back=back))),
    }
    parts = {**torso, **limbs}
    # back to front: far arm and leg behind the torso, the near arm in front
    order = ([f"{p}_r" for p in ARMS] + [f"{p}_r" for p in LEGS] + [f"{p}_l" for p in LEGS] + TORSO
             + [f"{p}_l" for p in ARMS])
    return parts, order


def view_front_left():
    return view_three_quarter(back=False)


def view_back_left():
    return view_three_quarter(back=True)


def with_side(limbs, side):
    return {f"{name}_{side}": v for name, v in limbs.items()}


LEGS = ["thigh", "shin", "foot"]
ARMS = ["upper_arm", "forearm", "hand"]
TORSO = ["pelvis", "waist", "chest", "head"]


def view_front():
    parts = {**front_torso(), **with_side(front_limbs(shift(ident, CX - 64)), "r"),
             **with_side(front_limbs(lambda x: mirror(x + CX - 64)), "l")}
    order = [f"{p}_{s}" for p in LEGS for s in "rl"] + TORSO + [f"{p}_r" for p in ARMS] + [f"{p}_l" for p in ARMS]
    return parts, order


def view_back():
    parts = {**front_torso(), **with_side(front_limbs(lambda x: mirror(x + CX - 64)), "r"),
             **with_side(front_limbs(shift(ident, CX - 64)), "l")}
    order = [f"{p}_{s}" for p in LEGS for s in "rl"] + TORSO + [f"{p}_r" for p in ARMS] + [f"{p}_l" for p in ARMS]
    return parts, order


def view_left():
    parts = {**side_torso(), **with_side(side_limbs(3), "r"), **with_side(side_limbs(0), "l")}
    order = ([f"{p}_r" for p in ARMS] + [f"{p}_r" for p in LEGS] + [f"{p}_l" for p in LEGS]
             + TORSO + [f"{p}_l" for p in ARMS])
    return parts, order


VIEWS = {"front": view_front, "left": view_left, "back": view_back,
         "frontleft": view_front_left, "backleft": view_back_left}


# ---------------------------------------------------------------- output

def render_view(view, parts, order):
    specs = {}
    for i, name in enumerate(order):
        joint, draw = parts[name]
        f = Figure(W, H, SCALE)
        draw(f)
        img = f.render()
        pad = PADDING.get(name, 2)
        x0, y0, x1, y1 = img.getbbox()
        x0, y0 = max(0, x0 - pad), max(0, y0 - pad)
        x1, y1 = min(W, x1 + pad), min(H, y1 + pad)
        file = f"{view}/{name}.png"
        (OUT / view).mkdir(parents=True, exist_ok=True)
        img.crop((x0, y0, x1, y1)).save(OUT / file)
        specs[name] = {"x": x0, "y": y0, "jointX": joint[0] * SCALE, "jointY": joint[1] * SCALE,
                       "order": i, "image": file}
    return specs


def main():
    global OUT, JOINT_DISCS
    for folder, discs in (("chibi96", True), ("chibi96_plain", False)):
        OUT, JOINT_DISCS = TEMPLATES / folder, discs
        build_template()


def build_template():
    views = {view: render_view(view, *build()) for view, build in VIEWS.items()}
    names = list(views["front"])
    parts = []
    for name in names:
        base, _, side = name.rpartition("_") if name.endswith(("_l", "_r")) else (name, "", "")
        label = LABELS[base] + (f" {side.upper()}" if side else "")
        parent = PARENTS[base]
        if parent and parent not in TORSO:
            parent = f"{parent}_{side}"
        parts.append({"name": name, "label": label, "parent": parent,
                      "views": {view: views[view][name] for view in VIEWS}})
    spec = {"width": W, "height": H, "parts": parts}
    (OUT / "skeleton.json").write_text(json.dumps(spec, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"{len(parts)} parts x {len(VIEWS)} views -> {OUT}")


if __name__ == "__main__":
    main()
