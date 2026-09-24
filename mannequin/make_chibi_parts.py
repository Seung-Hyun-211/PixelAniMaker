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

from make_chibi import Figure

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
        "head": ((64 + o, 74), lambda f: (f.capsule((64 + o, 62), 6, (64 + o, 76), 6), f.ellipse((35 + o, 8, 92 + o, 68)))),
    }


def front_limbs(s):
    """One side's limbs; s maps an x authored for the viewer-left side (centre 64) to the canvas."""
    return {
        "thigh": ((s(53), 138), lambda f: f.capsule((s(53), 140), 10, (s(54), 190), 8)),
        "shin": ((s(54), 195), lambda f: (f.capsule((s(54), 200), 8, (s(55), 235), 5), disc(f, (s(54), 195), 6))),
        "foot": ((s(55), 238), lambda f: (f.poly([(s(47), 236), (s(62), 236), (s(64), 246), (s(62), 249), (s(46), 249), (s(44), 246)]),
                                          disc(f, (s(55), 238), 4))),
        "upper_arm": ((s(43), 80), lambda f: (f.capsule((s(41), 84), 6, (s(37), 118), 5), disc(f, (s(43), 80), 7))),
        "forearm": ((s(37), 122), lambda f: (f.capsule((s(37), 126), 5, (s(34), 148), 4), disc(f, (s(37), 122), 5))),
        "hand": ((s(34), 151), lambda f: (f.poly([(s(29), 152), (s(39), 152), (s(41), 162), (s(38), 170), (s(31), 171), (s(27), 164)]),
                                          disc(f, (s(34), 151), 3))),
    }


def side_torso():
    c = CX
    return {
        "pelvis": ((c, 130), lambda f: f.poly([(c - 16, 124), (c + 18, 124), (c + 22, 140), (c + 14, 152), (c - 12, 152), (c - 18, 140)])),
        "waist": ((c, 125), lambda f: f.poly([(c - 14, 110), (c + 14, 110), (c + 15, 126), (c - 15, 126)])),
        "chest": ((c, 111), lambda f: f.poly([(c - 18, 74), (c + 14, 74), (c + 16, 86), (c + 14, 112), (c - 14, 112), (c - 20, 90)])),
        "head": ((c, 74), lambda f: (f.capsule((c + 1, 62), 6, (c, 76), 6), f.ellipse((c - 30, 8, c + 26, 68)))),
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
        "forearm": ((c + 1, 122), lambda f: (f.capsule((c + 1, 126), 5, (c, 148), 4), disc(f, (c + 1, 122), 5))),
        "hand": ((c, 151), lambda f: (f.poly([(c - 4, 152), (c + 5, 152), (c + 6, 162), (c + 3, 170), (c - 3, 171), (c - 6, 163)]),
                                      disc(f, (c, 151), 3))),
    }


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


VIEWS = {"front": view_front, "left": view_left, "back": view_back}


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
