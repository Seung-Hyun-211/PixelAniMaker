"""Splits the 64x128 chibi mannequin into 16 skeleton parts for the app.

Writes one PNG per part plus skeleton.json into the app's template assets.
Coordinates are in the 128x256 design space of make_chibi.py and scaled to 64x128.
Side suffixes are anatomical: *_r is the character's right, i.e. the viewer's left in the front view.
"""
import json
from pathlib import Path

from make_chibi import Figure, m

W, H = 64, 128
S = W / 128
OUT = Path(__file__).parent.parent / "src/PixelAniMaker.App/Assets/Templates/chibi64"


def ident(x):
    return x


def limb_parts(side, s):
    """Arm and leg parts for one side. s maps design x for that side."""
    return [
        # name, label, parent, order, joint, padding, draw
        (f"thigh_{side}", f"허벅지 {side.upper()}", "pelvis", 0, (s(53), 138), 2,
         lambda f: f.capsule((s(53), 140), 10, (s(54), 190), 8)),
        (f"shin_{side}", f"종아리 {side.upper()}", f"thigh_{side}", 2, (s(54), 195), 2,
         lambda f: (f.capsule((s(54), 200), 8, (s(55), 235), 5), f.circle((s(54), 195), 6))),
        (f"foot_{side}", f"발 {side.upper()}", f"shin_{side}", 4, (s(55), 238), 2,
         lambda f: (f.poly([(s(47), 236), (s(62), 236), (s(64), 246), (s(62), 249), (s(46), 249), (s(44), 246)]),
                    f.circle((s(55), 238), 4))),
        (f"upper_arm_{side}", f"상완 {side.upper()}", "chest", 10, (s(43), 80), 2,
         lambda f: (f.capsule((s(41), 84), 6, (s(37), 118), 5), f.circle((s(43), 80), 7))),
        (f"forearm_{side}", f"전완 {side.upper()}", f"upper_arm_{side}", 11, (s(37), 122), 2,
         lambda f: (f.capsule((s(37), 126), 5, (s(34), 148), 4), f.circle((s(37), 122), 5))),
        (f"hand_{side}", f"손 {side.upper()}", f"forearm_{side}", 12, (s(34), 151), 2,
         lambda f: (f.poly([(s(29), 152), (s(39), 152), (s(41), 162), (s(38), 170), (s(31), 171), (s(27), 164)]),
                    f.circle((s(34), 151), 3))),
    ]


def all_parts():
    r, l = limb_parts("r", ident), limb_parts("l", m)
    # unique draw orders: left legs interleave with the right ones, left arm is drawn after the right arm
    l = [(n, lb, p, o + (1 if o < 6 else 3), j, pad, d) for n, lb, p, o, j, pad, d in l]
    torso = [
        ("pelvis", "골반", None, 6, (64, 130), 2,
         lambda f: f.poly([(45, 124), (82, 124), (85, 140), (75, 150), (64, 153), (52, 150), (42, 140)])),
        ("waist", "허리", "pelvis", 7, (64, 125), 2,
         lambda f: f.poly([(47, 110), (80, 110), (81, 126), (46, 126)])),
        ("chest", "가슴", "waist", 8, (64, 111), 2,
         lambda f: f.poly([(44, 74), (83, 74), (85, 86), (81, 112), (46, 112), (42, 86)])),
        ("head", "머리", "chest", 9, (64, 74), 6,   # extra room for hair and accessories
         lambda f: (f.capsule((64, 62), 6, (64, 76), 6), f.ellipse((35, 8, 92, 68)))),
    ]
    return torso + r + l


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    spec = {"width": W, "height": H, "parts": []}
    for name, label, parent, order, joint, pad, draw in all_parts():
        f = Figure(W, H)
        draw(f)
        img = f.render()
        x0, y0, x1, y1 = img.getbbox()
        x0, y0 = max(0, x0 - pad), max(0, y0 - pad)
        x1, y1 = min(W, x1 + pad), min(H, y1 + pad)
        img.crop((x0, y0, x1, y1)).save(OUT / f"{name}.png")
        spec["parts"].append({
            "name": name, "label": label, "parent": parent, "order": order,
            "x": x0, "y": y0, "jointX": joint[0] * S, "jointY": joint[1] * S,
            "image": f"{name}.png",
        })
    spec["parts"].sort(key=lambda p: p["order"])
    (OUT / "skeleton.json").write_text(json.dumps(spec, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"{len(spec['parts'])} parts -> {OUT}")


if __name__ == "__main__":
    main()
