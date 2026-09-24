"""Default animation clips for the chibi96 template -> animations.json.

Angles are degrees, clockwise on screen, relative to the parent. Conventions for the mannequin:
  front view: *_r limbs are on the viewer's left, so +angle swings them outward; *_l outward is -angle
  left view (facing screen-left): +angle swings a hanging limb forward, a shin bends back with -angle,
                                  the torso leans forward with -angle
  back view: the mirror image of the front view (angles and x offset negated)
The right view is not stored; the app mirrors the left track.
"""
import json
from pathlib import Path

OUT = Path(__file__).parent.parent / "src/PixelAniMaker.App/Assets/Templates/chibi96/animations.json"


def key(frame, offset=(0, 0), easing=None, **rotations):
    k = {"frame": frame, "rotations": {p: a for p, a in rotations.items() if a}}
    if offset != (0, 0):
        k["offset"] = list(offset)
    if easing:
        k["easing"] = easing
    return k


def mirrored(keys):
    """Back track from a front track: the same motion seen from behind."""
    out = []
    for k in keys:
        m = dict(k, rotations={p: -a for p, a in k["rotations"].items()})
        if "offset" in k:
            m["offset"] = [-k["offset"][0], k["offset"][1]]
        out.append(m)
    return out


def swap_sides(**rotations):
    """Same pose with the left and right limbs exchanged (second half of a walk/run cycle)."""
    out = {}
    for p, a in rotations.items():
        if p.endswith("_l"):
            out[p[:-2] + "_r"] = a
        elif p.endswith("_r"):
            out[p[:-2] + "_l"] = a
        else:
            out[p] = a
    return out


def clip(name, frames, fps, loop, front, left):
    return {"name": name, "frames": frames, "fps": fps, "loop": loop,
            "tracks": {"front": front, "left": left, "back": mirrored(front)}}


# ---------------------------------------------------------------- clips

def idle():
    front = [key(0), key(2, offset=(0, 1), upper_arm_r=3, upper_arm_l=-3)]
    left = [key(0), key(2, offset=(0, 1), upper_arm_l=2, upper_arm_r=2)]
    return clip("대기", 4, 6, True, front, left)


def walk():
    contact = dict(thigh_l=25, shin_l=-5, thigh_r=-25, shin_r=-15,
                   upper_arm_l=-25, forearm_l=10, upper_arm_r=25, forearm_r=15)
    passing = dict(thigh_l=-3, shin_l=-5, thigh_r=8, shin_r=-35, upper_arm_l=0, forearm_l=8, upper_arm_r=0, forearm_r=8)
    left = [key(0, **contact), key(2, offset=(0, -1), **passing),
            key(4, **swap_sides(**contact)), key(6, offset=(0, -1), **swap_sides(**passing))]
    front = [key(0, upper_arm_r=4, upper_arm_l=4, thigh_r=-2, thigh_l=-2), key(2, offset=(0, -1)),
             key(4, upper_arm_r=-4, upper_arm_l=-4, thigh_r=2, thigh_l=2), key(6, offset=(0, -1))]
    return clip("걷기", 8, 10, True, front, left)


def run():
    stride = dict(waist=-8, thigh_l=45, shin_l=-25, thigh_r=-35, shin_r=-60,
                  upper_arm_l=-45, forearm_l=70, upper_arm_r=45, forearm_r=80)
    left = [key(0, **stride), key(1, offset=(0, -2), waist=-8, thigh_l=10, shin_l=-40, thigh_r=-5, shin_r=-80,
                                  upper_arm_l=0, forearm_l=75, upper_arm_r=0, forearm_r=75),
            key(3, **swap_sides(**stride)),
            key(4, offset=(0, -2), **swap_sides(waist=-8, thigh_l=10, shin_l=-40, thigh_r=-5, shin_r=-80,
                                                 upper_arm_l=0, forearm_l=75, upper_arm_r=0, forearm_r=75))]
    front = [key(0, upper_arm_r=10, forearm_r=-30, upper_arm_l=-10, forearm_l=30),
             key(1, offset=(0, -2), upper_arm_r=12, forearm_r=-35, upper_arm_l=-12, forearm_l=35),
             key(3, upper_arm_r=10, forearm_r=-30, upper_arm_l=-10, forearm_l=30),
             key(4, offset=(0, -2), upper_arm_r=12, forearm_r=-35, upper_arm_l=-12, forearm_l=35)]
    return clip("달리기", 6, 12, True, front, left)


def jump():
    crouch_side = dict(waist=-15, thigh_l=50, shin_l=-75, foot_l=25, thigh_r=45, shin_r=-70, foot_r=25,
                       upper_arm_l=-35, upper_arm_r=-40)
    air_side = dict(waist=-5, thigh_l=25, shin_l=-40, thigh_r=15, shin_r=-30, upper_arm_l=150, upper_arm_r=140)
    left = [key(0), key(1, offset=(0, 6), **crouch_side), key(2, offset=(0, -14), easing="Linear", **air_side),
            key(3, offset=(0, -10), waist=-3, thigh_l=20, shin_l=-30, thigh_r=15, shin_r=-25, upper_arm_l=60, upper_arm_r=50),
            key(4, offset=(0, 3), waist=-10, thigh_l=30, shin_l=-45, foot_l=15, thigh_r=25, shin_r=-40, foot_r=15)]
    front = [key(0), key(1, offset=(0, 6), upper_arm_r=25, upper_arm_l=-25, thigh_r=8, shin_r=-8, thigh_l=-8, shin_l=8),
             key(2, offset=(0, -14), easing="Linear", upper_arm_r=150, upper_arm_l=-150),
             key(3, offset=(0, -10), upper_arm_r=80, upper_arm_l=-80),
             key(4, offset=(0, 3), upper_arm_r=15, upper_arm_l=-15, thigh_r=5, shin_r=-5, thigh_l=-5, shin_l=5)]
    return clip("점프", 5, 8, False, front, left)


def attack():
    left = [key(0), key(1, chest=12, upper_arm_l=-70, forearm_l=-40),
            key(2, easing="Linear", waist=-12, upper_arm_l=110, forearm_l=15),
            key(3, waist=-8, upper_arm_l=80, forearm_l=10), key(4, upper_arm_l=10)]
    front = [key(0), key(1, chest=-5, upper_arm_r=130, forearm_r=20),
             key(2, easing="Linear", chest=6, upper_arm_r=-45, forearm_r=-20),
             key(3, chest=4, upper_arm_r=-30, forearm_r=-10), key(4, upper_arm_r=5)]
    return clip("공격", 5, 10, False, front, left)


def hit():
    left = [key(0), key(1, offset=(3, 0), waist=15, head=15, upper_arm_l=25, upper_arm_r=20),
            key(2, offset=(1, 0), waist=5, head=5, upper_arm_l=8, upper_arm_r=6)]
    front = [key(0), key(1, offset=(0, 1), head=10, upper_arm_r=20, upper_arm_l=-20),
             key(2, head=3, upper_arm_r=6, upper_arm_l=-6)]
    return clip("피격", 3, 10, False, front, left)


def main():
    clips = [idle(), walk(), run(), jump(), attack(), hit()]
    OUT.write_text(json.dumps({"animations": clips}, ensure_ascii=False, indent=1), encoding="utf-8")
    print(f"{len(clips)} clips -> {OUT}")


if __name__ == "__main__":
    main()
