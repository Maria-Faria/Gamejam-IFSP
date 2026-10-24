# Resizes, rigs and animates the monster models (F:/fatec/Monster_Views_24_PNGs/*.fbx) for the roguelike.
# Every monster gets its own small generic skeleton (Root > Hips > Spine > Head, arms, optional tail/legs and
# socket bones the game spawns projectiles from), skin weights computed from bone distance, an Idle loop and
# its attack clips. Sizes are relative to the player characters (edna.fbx: 1.109 m, the largest side of its
# bounding box).
# usage: blender -b -P make_monsters.py -- <src_dir> <out_dir> [preview_dir] [only_name ...]
# Outputs: <out_dir>/<Folder>/<name>.fbx, <name>.clips.json (clip lengths, loops and animation events),
#          <out_dir>/<Folder>/Textures/<name>_{albedo,normal,metallicSmoothness}.png
import bpy, bmesh, math, os, sys, json
import numpy as np
from mathutils import Matrix, Vector, Quaternion

args = sys.argv[sys.argv.index('--') + 1:]
SRC, OUT = args[0], args[1]
PREVIEW = args[2] if len(args) > 2 and args[2] != '-' else None
ONLY = set(args[3:])
FPS = 30
REF_SIZE = 1.109        # edna.fbx, largest bounding box side
TEX_SIZE = 2048

# Blender space: Z up, the monsters face -Y, their left is +X.
F, BK, U, D, X = Vector((0, -1, 0)), Vector((0, 1, 0)), Vector((0, 0, 1)), Vector((0, 0, -1)), Vector((1, 0, 0))

def n(*vs):
    v = Vector((0, 0, 0))
    for a in vs:
        v += a
    return v.normalized()

# Joints in normalized bounding box coordinates of the source mesh: (x across 0..1 towards the monster's
# left, depth 0 back..1 front, height 0 top..1 bottom), read off front/side views. Arms are given for the
# left side and mirrored.
MONSTERS = {
    "ball_shadow": dict(folder="BallShadow", scale=0.5, hover=0.12, kind="ball", eyes=dict(c=(.5, .5), r=.5)),
    "twisted_ball": dict(folder="TwistedBall", scale=0.5, hover=0.12, kind="ball", base_color=(.16, .11, .2),
                         eyes=dict(c=(.47, .49), r=.3)),
    "regular": dict(folder="Regular", scale=0.75, hover=0.10, kind="shooter", hands="R",
                    pelvis=(.5, .5, .72), chest=(.5, .5, .56), neck=(.5, .5, .48), top=(.5, .45, .02), tail=(.5, .5, .99),
                    arm=[(.66, .55, .56), (.76, .62, .60), (.85, .70, .61), (.95, .80, .68)]),
    "3_eyed": dict(folder="ThreeEyed", scale=0.75, hover=0.10, kind="shooter", hands="both", teleport=True,
                   pelvis=(.5, .38, .85), chest=(.5, .40, .62), neck=(.5, .42, .50), top=(.5, .35, .02),
                   arm=[(.66, .52, .58), (.76, .66, .61), (.83, .80, .62), (.90, .93, .76)]),
    "ghost": dict(folder="Ghost", scale=0.75, hover=0.10, kind="puncher",
                  pelvis=(.5, .5, .88), chest=(.5, .5, .55), neck=(.5, .55, .45), top=(.5, .5, .02),
                  arm=[(.74, .58, .45), (.82, .68, .50), (.88, .80, .56), (.96, .92, .70)]),
    "crescent": dict(folder="Crescent", scale=1.0, hover=0.10, kind="shooter", hands="L",
                     pelvis=(.5, .42, .88), chest=(.5, .45, .62), neck=(.5, .5, .50), top=(.5, .55, .02),
                     arm=[(.68, .55, .62), (.78, .68, .64), (.86, .84, .65), (.92, .95, .79)]),
    "ghoul": dict(folder="Ghoul", scale=1.0, hover=0.10, kind="puncher",
                  pelvis=(.5, .5, .88), chest=(.5, .55, .55), neck=(.5, .6, .45), top=(.5, .65, .02),
                  arm=[(.75, .60, .42), (.83, .72, .47), (.88, .85, .50), (.95, .95, .70)]),
    "horned": dict(folder="Horned", scale=1.0, hover=0.08, kind="puncher", combo=True, shoot="R",
                   pelvis=(.5, .45, .88), chest=(.5, .5, .58), neck=(.5, .55, .45), top=(.5, .55, .08),
                   arm=[(.70, .55, .55), (.80, .70, .60), (.86, .83, .62), (.93, .95, .78)]),
    "fat": dict(folder="Fat", scale=1.7, hover=0.0, kind="fat",
                pelvis=(.5, .45, .80), chest=(.5, .48, .50), neck=(.5, .55, .42), top=(.5, .72, .05),
                mouth=(.5, .92, .38), arm_r=(0.13, 0.12, 0.12),
                arm=[(.76, .55, .35), (.85, .70, .45), (.87, .85, .55), (.90, .96, .68)]),
    "muscle": dict(folder="Muscle", scale=1.0, hover=0.0, kind="muscle", lean=1.0,
                   pelvis=(.5, .35, .62), chest=(.5, .40, .35), neck=(.5, .55, .32), top=(.5, .80, .02),
                   arm_r=(0.17, 0.16, 0.15), limbs_only_outside=True,
                   arm=[(.72, .40, .28), (.86, .45, .45), (.86, .60, .62), (.82, .72, .76)],
                   leg=[(.62, .32, .66), (.65, .32, .82), (.67, .30, .94), (.67, .48, .99)]),
    "muscle_boss": dict(folder="MuscleBoss", scale=2.6, hover=0.0, kind="brute", lean=0.0,
                        pelvis=(.5, .45, .62), chest=(.5, .45, .40), neck=(.5, .48, .33), top=(.5, .5, .02),
                        arm_r=(0.17, 0.16, 0.18), limbs_only_outside=True, hard_hands=True,
                        arm=[(.72, .45, .38), (.83, .55, .47), (.87, .72, .56), (.90, .95, .64)],
                        leg=[(.60, .45, .62), (.63, .48, .80), (.66, .45, .93), (.68, .62, .99)]),
    "claw": dict(folder="Claw", scale=0.75, hover=0.10, kind="claw", hands="R", teleport=True,
                 pelvis=(.5, .45, .75), chest=(.5, .5, .52), neck=(.5, .55, .40), top=(.5, .62, .0), tail=(.5, .3, .99),
                 arm_r=(0.09, 0.09, 0.15), limbs_only_outside=True,
                 arm=[(.70, .55, .40), (.80, .58, .47), (.86, .64, .55), (.90, .80, .72)]),
    "skull_face": dict(folder="SkullFace", scale=2.0, hover=0.06, kind="skull",
                       pelvis=(.5, .45, .88), chest=(.5, .5, .55), neck=(.5, .55, .45), top=(.5, .5, .02),
                       eye=(.5, .93, .42),
                       arm=[(.68, .55, .50), (.78, .68, .58), (.85, .80, .65), (.92, .90, .82)]),
}

# ---------------------------------------------------------------------------------------------- mesh

def load(name):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=os.path.join(SRC, name + ".fbx"))
    for o in list(bpy.context.scene.objects):
        if o.type != 'MESH':
            bpy.data.objects.remove(o)
    mesh = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
    mesh.data.transform(mesh.matrix_world)
    mesh.matrix_world = Matrix.Identity(4)
    mesh.name = mesh.data.name = name
    return mesh

def bbox(mesh):
    co = np.empty(len(mesh.data.vertices) * 3, np.float32)
    mesh.data.vertices.foreach_get('co', co)
    co = co.reshape(-1, 3)
    return Vector(co.min(0)), Vector(co.max(0))

def normalize(mesh, spec):
    """Scale to the size class, center on X/Y, lowest point at the hover height."""
    lo, hi = bbox(mesh)
    k = spec["scale"] * REF_SIZE / max(hi - lo)
    ctr = (lo + hi) / 2
    hover = spec["hover"] * (hi.z - lo.z) * k
    mesh.data.transform(Matrix.Translation((0, 0, hover)) @ Matrix.Scale(k, 4) @ Matrix.Translation((-ctr.x, -ctr.y, -lo.z)))
    mesh.data.update()
    return k

def joint_fn(lo, hi):
    d = hi - lo
    def j(p, side=1):
        fx, fs, fz = p
        fx = 0.5 + (fx - 0.5) * side
        return Vector((lo.x + fx * d.x, hi.y - fs * d.y, hi.z - fz * d.z))
    return j

# ---------------------------------------------------------------------------------------------- rig

def build_rig(mesh, spec):
    lo, hi = bbox(mesh)
    j = joint_fn(lo, hi)
    H = hi.z
    arm_data = bpy.data.armatures.new(mesh.name + "_rig")
    rig = bpy.data.objects.new("Armature", arm_data)
    bpy.context.scene.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode='EDIT')
    eb = arm_data.edit_bones
    S = max(hi - lo)
    radius = {}

    def bone(name, head, tail, parent=None, r=None, connect=False):
        b = eb.new(name)
        b.head, b.tail = head, tail
        b.roll = 0
        if parent:
            b.parent = eb[parent]
            b.use_connect = connect
        if r is not None:
            radius[name] = r * S
        else:
            b.use_deform = False
        return b

    bone("Root", Vector((0, 0, 0)), Vector((0, 0, 0.15 * H)))
    if spec["kind"] == "ball":
        bone("Body", Vector((0, 0, lo.z)), Vector((0, 0, hi.z)), "Root", r=1.0)
        bone("Muzzle", Vector((0, lo.y, (lo.z + hi.z) / 2)), Vector((0, lo.y - 0.1 * S, (lo.z + hi.z) / 2)), "Body")
    else:
        bone("Hips", j(spec["pelvis"]), j(spec["chest"]), "Root", r=0.30)
        bone("Spine", j(spec["chest"]), j(spec["neck"]), "Hips", r=0.28, connect=True)
        bone("Head", j(spec["neck"]), j(spec["top"]), "Spine", r=0.26, connect=True)
        if "tail" in spec:
            bone("Tail", j(spec["pelvis"]), j(spec["tail"]), "Hips", r=0.12)
        for side, sfx in ((1, "L"), (-1, "R")):
            sh, el, wr, tip = (j(p, side) for p in spec["arm"])
            ru, rf, rh = spec.get("arm_r", (0.075, 0.065, 0.10))
            bone("UpperArm." + sfx, sh, el, "Spine", r=ru)
            bone("ForeArm." + sfx, el, wr, "UpperArm." + sfx, r=rf, connect=True)
            bone("Hand." + sfx, wr, tip, "ForeArm." + sfx, r=rh, connect=True)
            bone("Muzzle." + sfx, tip, tip + (tip - wr).normalized() * 0.05 * S, "Hand." + sfx)
            if "leg" in spec:
                hip, knee, ankle, toe = (j(p, side) for p in spec["leg"])
                bone("UpLeg." + sfx, hip, knee, "Hips", r=0.13)
                bone("Leg." + sfx, knee, ankle, "UpLeg." + sfx, r=0.12, connect=True)
                bone("Foot." + sfx, ankle, toe, "Leg." + sfx, r=0.11, connect=True)
        if "mouth" in spec:
            m = j(spec["mouth"])
            bone("Mouth", m, m + F * 0.05 * S, "Head")
        if "eye" in spec:
            e = j(spec["eye"])
            bone("Eye", e, e + F * 0.05 * S, "Head")
    bpy.ops.object.mode_set(mode='OBJECT')
    return rig, radius

def seg_dist(P, a, b):
    ab = b - a
    t = np.clip(((P - a) @ ab) / max(ab @ ab, 1e-12), 0, 1)
    return np.linalg.norm(P - (a + t[:, None] * ab), axis=1)

def islands(me):
    """Vertex index arrays of the mesh's connected pieces (over face edges)."""
    parent = list(range(len(me.vertices)))
    def find(a):
        while parent[a] != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        return a
    for f in me.polygons:
        vs = list(f.vertices)
        for v in vs[1:]:
            ra, rb = find(vs[0]), find(v)
            if ra != rb:
                parent[ra] = rb
    used = np.zeros(len(me.vertices), bool)
    for f in me.polygons:
        used[list(f.vertices)] = True
    groups = {}
    for v in np.nonzero(used)[0]:
        groups.setdefault(find(int(v)), []).append(int(v))
    return [np.array(g) for g in groups.values()]

def split_faces(me, side_a, region):
    """Faces in `region` (per-vertex mask) with vertices on both sides of the per-vertex mask `side_a`."""
    out = set()
    for f in me.polygons:
        vs = list(f.vertices)
        if all(region[v] for v in vs):
            a = sum(1 for v in vs if side_a[v])
            if 0 < a < len(vs):
                out.add(f.index)
    return out

def skin(mesh, rig, radius, limbs_only_outside=False, hard_hands=False):
    """Weights fall off with distance to each bone measured in units of that bone's thickness, so thin arm
    bones only take the arm and the thick body bones take the rest. Arm/leg bones only reach vertices on
    their own side, the head only what is above the neck. Then a few passes of smoothing over the edges."""
    me = mesh.data
    P = np.empty(len(me.vertices) * 3); me.vertices.foreach_get('co', P); P = P.reshape(-1, 3)
    bones = [b for b in rig.data.bones if b.name in radius]
    W = np.zeros((len(P), len(bones)))
    chain_s = {}
    for i, b in enumerate(bones):
        a, c = np.array(b.head_local), np.array(b.tail_local)
        s = seg_dist(P, a, c) / radius[b.name]
        w = 1.0 / (s ** 4 + 1e-4)
        S = radius["Hips"] / 0.30 if "Hips" in radius else 1.0
        if b.name.endswith((".L", ".R")):
            side = 1 if b.name.endswith(".L") else -1
            is_leg = "Leg" in b.name or "Foot" in b.name
            root = rig.data.bones[("UpLeg." if is_leg else "UpperArm.") + b.name[-1]]
            x0 = abs(root.head_local.x)
            if is_leg:
                ramp = np.clip(P[:, 0] * side / max(x0 * 0.35, 1e-4), 0, 1)
                ramp *= np.clip((root.head_local.z + 0.05 * S - P[:, 2]) / (0.1 * S), 0, 1)
            else:
                ramp = np.clip((P[:, 0] * side - x0 * 0.55) / max(x0 * 0.45, 1e-4), 0, 1)
            w *= ramp
        if b.name == "Head":
            w *= np.clip((P[:, 2] - (b.head_local.z - 0.08 * S)) / (0.1 * S), 0, 1)
        if limbs_only_outside and b.name in ("Hips", "Spine"):
            # big limbs hanging next to the body (a fist by the thigh) belong to the limb, not the torso
            x0 = abs(rig.data.bones["UpperArm.L"].head_local.x)
            w *= 1 - np.clip((np.abs(P[:, 0]) - x0 * 0.95) / (x0 * 0.25), 0, 1)
        W[:, i] = w
        if b.name.startswith(("ForeArm", "Hand", "Leg", "Foot")):
            chain = ("leg" if b.name.startswith(("Leg", "Foot")) else "arm") + b.name[-1]
            inside = np.where(ramp > 0.5, s, np.inf)
            chain_s[chain] = np.minimum(chain_s[chain], inside) if chain in chain_s else inside
    # whatever sits inside a forearm/hand/shin/foot capsule belongs to that limb only (a fist resting
    # against the thigh must not keep torso or leg weights, or it tears off when the arm moves)
    def limb_exclusive(W):
        for chain, cs in chain_s.items():
            own = np.clip((cs - 1.0) / 0.4, 0, 1)
            members = ("UpperArm", "ForeArm", "Hand") if chain.startswith("arm") else ("UpLeg", "Leg", "Foot")
            for i, b in enumerate(bones):
                if not (b.name.startswith(members) and b.name.endswith(chain[-1])):
                    W[:, i] *= own
        return W / (W.sum(1, keepdims=True) + 1e-12)
    W = limb_exclusive(W) if limbs_only_outside else W
    # smoothing over mesh edges
    E = np.empty(len(me.edges) * 2, np.int64); me.edges.foreach_get('vertices', E); E = E.reshape(-1, 2)
    deg = np.bincount(E.ravel(), minlength=len(P)).astype(float)[:, None] + 1e-9
    for _ in range(4):
        acc = np.zeros_like(W)
        np.add.at(acc, E[:, 0], W[E[:, 1]])
        np.add.at(acc, E[:, 1], W[E[:, 0]])
        W = 0.5 * W + 0.5 * acc / deg
    W = limb_exclusive(W) if limbs_only_outside else W
    if hard_hands:
        cut_faces = set()
        # big fists hanging by the hips, modelled pressed into them: below the elbow there is no blend
        # between the arm and the body/legs at all, or a strip of skin stretches from the fist to the hip
        # when the arm swings out
        for sfx in "LR":
            arm_i = [i for i, b in enumerate(bones) if b.name in ("UpperArm." + sfx, "ForeArm." + sfx, "Hand." + sfx)]
            body_i = [i for i, b in enumerate(bones) if not b.name.startswith(("UpperArm", "ForeArm", "Hand"))]
            if not arm_i:
                continue
            elbow_z = rig.data.bones["ForeArm." + sfx].head_local.z
            fa, fb = W[:, arm_i].sum(1), W[:, body_i].sum(1)
            low = (P[:, 2] < elbow_z) & (fa > 1e-3) & (fb > 1e-3)
            mine = low & (fa >= fb)
            W[np.ix_(mine, body_i)] = 0
            W[np.ix_(low & ~mine, arm_i)] = 0
            cut_faces |= split_faces(me, W[:, arm_i].sum(1) > 0.5, P[:, 2] < elbow_z)
        W /= W.sum(1, keepdims=True) + 1e-12
        # the fist is fused into the hip: cut the faces that bridge the two so they part cleanly
        bm = bmesh.new(); bm.from_mesh(me); bm.faces.ensure_lookup_table()
        bmesh.ops.delete(bm, geom=[bm.faces[i] for i in sorted(cut_faces)], context='FACES_ONLY')
        bm.to_mesh(me); bm.free()
        print("cut", len(cut_faces), "faces between fists and body")
        # bits that came loose (lumps modelled inside a fist) go wholly with the nearest bone
        for isl in islands(me):
            if len(isl) > 0.02 * len(P):
                continue
            c = P[isl].mean(0)
            near = min(range(len(bones)), key=lambda i: seg_dist(c[None], np.array(bones[i].head_local),
                                                                  np.array(bones[i].tail_local))[0] / radius[bones[i].name])
            W[isl] = 0
            W[isl, near] = 1
    # four strongest influences
    order = np.argsort(-W, axis=1)
    keep = np.zeros_like(W, bool)
    np.put_along_axis(keep, order[:, :4], True, axis=1)
    W = np.where(keep, W, 0)
    W /= W.sum(1, keepdims=True) + 1e-12
    for i, b in enumerate(bones):
        g = mesh.vertex_groups.new(name=b.name)
        idx = np.nonzero(W[:, i] > 1e-3)[0]
        for v in idx:
            g.add([int(v)], float(W[v, i]), 'REPLACE')
    mesh.parent = rig
    mod = mesh.modifiers.new("Armature", 'ARMATURE')
    mod.object = rig

# ---------------------------------------------------------------------------------------------- posing

class Poser:
    """Keys poses written in the monster's rest-pose space. Rotations are deltas relative to the parent in
    rest-pose world axes; arm/leg bones can instead be aimed at a direction given in the body's frame, which
    keeps a pose readable whatever the arm length. Root location is in metres, Root scale is
    (sideways, forward, up)."""

    def __init__(self, rig, H):
        self.rig, self.H = rig, H
        self.stance = None      # the neutral pose clips start and end on, when it is not the rest pose
        self.lean = 1.0         # how far muscle_stance straightens the back
        self.rest = {b.name: b.matrix_local.to_3x3() for b in rig.data.bones}
        self.dir = {b.name: (b.tail_local - b.head_local).normalized() for b in rig.data.bones}

    def has(self, b):
        return b in self.rig.pose.bones

    def key(self, frame, pose):
        rig = self.rig
        acc = {}      # rotation of each aimed bone relative to the body (the frame of its chain's first parent)
        for pb in rig.pose.bones:
            pb.rotation_mode = 'QUATERNION'
            pb.rotation_quaternion = Quaternion()
            pb.location = Vector()
            pb.scale = Vector((1, 1, 1))
        for name, spec in pose.items():
            if not self.has(name):
                continue
            pb = rig.pose.bones[name]
            R = self.rest[name]
            if "aim" in spec:
                # carry the bone along with its parent first, then turn it the shortest way onto the target:
                # no twist between a forearm and its hand, so the wrist does not shear
                Ap = acc.get(rig.data.bones[name].parent.name, Matrix.Identity(3))
                carried = Ap @ self.dir[name]
                cur = slerp_dir(carried, spec["aim"].normalized(), spec.get("k", 1.0))
                A = carried.rotation_difference(cur).to_matrix() @ Ap
                Dm = Ap.inverted() @ A
                acc[name] = A
            else:
                Dm = Matrix.Identity(3)
                for ax, deg in zip("XYZ", spec.get("rot", (0, 0, 0))):
                    if deg:
                        Dm = Matrix.Rotation(math.radians(deg), 3, ax) @ Dm
            pb.rotation_quaternion = (R.inverted() @ Dm @ R).to_quaternion()
            if "loc" in spec:
                pb.location = R.inverted() @ Vector(spec["loc"])
            if "scale" in spec:
                sx, sf, su = spec["scale"]
                # Root points up: local X = world X, local Y = up, local Z = -Y = forward
                pb.scale = Vector((sx, su, sf))
        for pb in rig.pose.bones:
            pb.keyframe_insert("rotation_quaternion", frame=frame)
            pb.keyframe_insert("location", frame=frame)
            pb.keyframe_insert("scale", frame=frame)

def slerp_dir(a, b, k):
    q = a.rotation_difference(b)
    return Quaternion().slerp(q, k) @ a

def make_clip(rig, poser, name, length, keys, loop=False, events=()):
    """keys: list of (frame, pose dict). events: list of (frame, name)."""
    act = bpy.data.actions.new(name)
    act.use_fake_user = True
    rig.animation_data_create()
    rig.animation_data.action = act
    for f, pose in keys:
        poser.key(f, pose)
    clip = dict(name=name, frames=length, loop=loop,
                events=[dict(time=f / length, name=e) for f, e in events])
    return act, clip, [f for f, _ in keys]

# ---------------------------------------------------------------------------------------------- motion library

def side_vec(sfx):
    return X if sfx == "L" else -X

def arm(sfx, upper=None, fore=None, hand=None, k=1.0):
    p = {}
    if upper is not None: p["UpperArm." + sfx] = dict(aim=upper, k=k)
    if fore is not None: p["ForeArm." + sfx] = dict(aim=fore, k=k)
    if hand is not None: p["Hand." + sfx] = dict(aim=hand, k=k)
    return p

def merge(*ds):
    out = {}
    for d in ds:
        out.update(d)
    return out

def rest_arm(poser, sfx, droop=0.0, curl=0.0):
    """The rest arm, drooped down a little and claws curled."""
    if not poser.has("UpperArm." + sfx):
        return {}
    u = poser.dir["UpperArm." + sfx]; f = poser.dir["ForeArm." + sfx]; h = poser.dir["Hand." + sfx]
    return arm(sfx, n(u, D * droop), n(f, D * (droop * 1.3)), n(h, D * (droop * 1.5 + curl)))

def float_idle(poser, H, phase_arms=True):
    """2 s hover: bob, a slow sway, arms drifting, claws flexing."""
    keys = []
    for i, f in enumerate((0, 15, 30, 45, 60)):
        s = math.sin(i * math.pi / 2)
        c = math.cos(i * math.pi / 2)
        pose = {"Root": dict(loc=(0, 0, 0.035 * H * s), rot=(0, 2.5 * c, 0)),
                "Hips": dict(rot=(2 * c, 0, 0)),
                "Spine": dict(rot=(2.5 * s, 0, 2 * c)),
                "Head": dict(rot=(-2 * s, 0, 4 * c)),
                "Tail": dict(rot=(6 * c, 8 * s, 0))}
        pose.update(rest_arm(poser, "L", droop=0.12 + 0.12 * s, curl=0.2 + 0.2 * c))
        pose.update(rest_arm(poser, "R", droop=0.12 - 0.12 * s, curl=0.2 - 0.2 * c))
        keys.append((f, pose))
    return keys

def ground_idle(poser, H, squash=False):
    keys = []
    for i, f in enumerate((0, 20, 40, 60)):
        s = math.sin(i * 2 * math.pi / 3)
        if poser.stance is not None:
            pose = muscle_stance(s, 0.0, poser.lean)
            pose["Head"]["rot"] = (pose["Head"]["rot"][0], 0, 3 * math.cos(i * 2 * math.pi / 3))
            pose["Root"] = dict(loc=(0, 0, -.006 * H * s))
            keys.append((f, pose))
            continue
        pose = {"Spine": dict(rot=(-3 * s, 0, 0)), "Head": dict(rot=(3 * s, 0, 3 * math.cos(i * 2 * math.pi / 3)))}
        if squash:
            pose["Root"] = dict(scale=(1 + 0.03 * s, 1 + 0.03 * s, 1 - 0.035 * s))
        pose.update(rest_arm(poser, "L", droop=0.08 + 0.06 * s, curl=0.2))
        pose.update(rest_arm(poser, "R", droop=0.08 + 0.06 * s, curl=0.2))
        keys.append((f, pose))
    keys[-1] = (60, keys[0][1])
    return keys

def base(poser, H):
    if poser.stance is not None:
        return dict(poser.stance)
    p = merge(rest_arm(poser, "L", 0.12, 0.2), rest_arm(poser, "R", 0.12, 0.2))
    return p

def muscle_stance(breath=0.0, swing=0.0, lean=1.0):
    """The muscle one is modelled hunched over, knuckles to the floor, as if about to charge: stand it up
    (lean 1), chest out, fists hanging at its sides. muscle_boss already stands straight (lean 0).
    breath: -1..1 chest rise, swing: -1..1 arm swing (walking)."""
    p = {"Hips": dict(rot=(-8 * lean, 0, 0)), "Spine": dict(rot=(-15 * lean - 2.5 * breath, 0, 0)),
         "Head": dict(rot=(6 * lean + 2 * breath, 0, 0))}
    for s, k in (("L", 1), ("R", -1)):
        O = side_vec(s)
        sw = swing * k
        p.update(arm(s, n(D, O * (.42 + .04 * breath), F * (.08 + .35 * sw)), n(D, O * .12, F * (.3 + .3 * sw)), n(D, F * .35)))
    return p

def float_move(poser, H):
    """Gliding: leant into the direction of travel, arms trailing, bobbing."""
    keys = []
    for i, f in enumerate((0, 8, 16, 24, 32)):
        s, c = math.sin(i * math.pi / 2), math.cos(i * math.pi / 2)
        pose = {"Root": dict(loc=(0, 0, 0.03 * H * s), rot=(9, 3 * c, 0)),
                "Spine": dict(rot=(3, 0, 3 * c)), "Head": dict(rot=(-8, 0, -3 * c)),
                "Tail": dict(rot=(-14, 10 * c, 0))}
        pose.update(rest_arm(poser, "L", droop=0.3 + 0.06 * s, curl=0.35))
        pose.update(rest_arm(poser, "R", droop=0.3 - 0.06 * s, curl=0.35))
        keys.append((f, pose))
    return keys

def muscle_walk(poser, H):
    """Heavy stomping walk, 32 frames per two steps."""
    keys = []
    for f in range(0, 33, 4):
        ph = f / 32 * 2 * math.pi
        p = muscle_stance(0, math.sin(ph), poser.lean)
        p.update(legs(ph, 22, 30))
        p.update({"Hips": dict(rot=(-6 * poser.lean, 0, 5 * math.sin(ph))), "Spine": dict(rot=(-13 * poser.lean, 0, -6 * math.sin(ph))),
                  "Root": dict(loc=(0, 0, .02 * H * abs(math.cos(ph))), rot=(0, 2.5 * math.sin(ph), 0))})
        keys.append((f, p))
    return keys

def fat_waddle(poser, H):
    """Rocking from side to side, a squash on every step."""
    keys = []
    for f in range(0, 33, 4):
        ph = f / 32 * 2 * math.pi
        w = math.sin(ph)
        p = merge(rest_arm(poser, "L", 0.12 + 0.08 * w, 0.2), rest_arm(poser, "R", 0.12 - 0.08 * w, 0.2))
        p.update({"Root": dict(rot=(4, 7 * w, 0), loc=(0, 0, .025 * H * abs(math.cos(ph))),
                               scale=(1 + .03 * abs(w), 1, 1 - .03 * abs(w))),
                  "Spine": dict(rot=(0, -4 * w, 0)), "Head": dict(rot=(0, -3 * w, 0))})
        keys.append((f, p))
    return keys

def ball_move(poser, H):
    """Bouncing along: squash, hop, stretch."""
    return [(0, {"Root": dict(scale=(1.12, 1.12, .86))}),
            (5, {"Root": dict(loc=(0, 0, .18 * H), scale=(.92, .92, 1.12), rot=(10, 0, 0))}),
            (10, {"Root": dict(loc=(0, 0, .26 * H), scale=(1, 1, 1), rot=(14, 0, 0))}),
            (15, {"Root": dict(loc=(0, 0, .12 * H), scale=(.95, .95, 1.08), rot=(8, 0, 0))}),
            (20, {"Root": dict(scale=(1.12, 1.12, .86))})]

def shoot_clip(poser, H, hands):
    """Charge a bolt in the hand(s), throw it forward from the palm, recoil. Fire at frame 16."""
    sides = ["L", "R"] if hands == "both" else [hands]
    keys = [(0, base(poser, H))]
    wind, fire, recoil = base(poser, H), base(poser, H), base(poser, H)
    twist = {"L": 1, "R": -1}
    for s in sides:
        O = side_vec(s)
        if hands == "both":
            wind.update(arm(s, n(O * .8, BK * .3, U * .4), n(U * .7, O * .3, F * .2), n(U, F * .3)))
            fire.update(arm(s, n(F, O * .35), n(F, -O * .15), n(F, U * .5)))
            recoil.update(arm(s, n(F * .8, O * .4, U * .3), n(F * .7, U * .5), n(F * .5, U)))
        else:
            wind.update(arm(s, n(O * .7, BK * .45, U * .35), n(U * .7, F * .35, O * .2), n(F * .5, U * .6)))
            fire.update(arm(s, n(F, O * .15, U * .05), n(F), n(F, U * .45)))
            recoil.update(arm(s, n(F * .8, U * .3, O * .15), n(F * .8, U * .45), n(F * .4, U)))
    tw = 0 if hands == "both" else twist[sides[0]]
    wind.update({"Spine": dict(rot=(-6, 0, 16 * tw)), "Head": dict(rot=(4, 0, -8 * tw)),
                 "Root": dict(loc=(0, 0.02 * H, 0.02 * H))})
    fire.update({"Spine": dict(rot=(10, 0, -12 * tw)), "Head": dict(rot=(-4, 0, 6 * tw)),
                 "Root": dict(loc=(0, -0.04 * H, 0))})
    recoil.update({"Spine": dict(rot=(-5, 0, -6 * tw)), "Root": dict(loc=(0, 0.02 * H, 0.01 * H))})
    keys += [(10, wind), (13, wind), (16, fire), (22, recoil), (36, base(poser, H))]
    return keys, [(16, "Fire")]

def punch_pose(poser, H, s, phase):
    O = side_vec(s)
    tw = 1 if s == "L" else -1
    p = base(poser, H)
    if phase == "wind":
        p.update(arm(s, n(O * .6, BK * .7, U * .1), n(F * .6, O * .5, U * .2), n(F, D * .3)))
        p.update({"Hips": dict(rot=(-2, 0, 10 * tw)), "Spine": dict(rot=(-3, 0, 11 * tw)), "Head": dict(rot=(5, 0, -12 * tw)),
                  "Root": dict(loc=(0, 0.04 * H, 0.01 * H))})
    elif phase == "hit":
        p.update(arm(s, n(F, O * .05), n(F, -O * .05), n(F, D * .25)))
        p.update({"Hips": dict(rot=(5, 0, -12 * tw)), "Spine": dict(rot=(6, 0, -13 * tw)), "Head": dict(rot=(-6, 0, 10 * tw)),
                  "Root": dict(loc=(0, -0.16 * H, -0.01 * H))})
    return p

def punch_clip(poser, H, combo):
    if not combo:
        keys = [(0, base(poser, H)), (9, punch_pose(poser, H, "R", "wind")), (13, punch_pose(poser, H, "R", "hit")),
                (17, punch_pose(poser, H, "R", "hit")), (30, base(poser, H))]
        return keys, [(13, "Hit")]
    # left jab then right hook
    keys = [(0, base(poser, H)), (8, punch_pose(poser, H, "L", "wind")), (12, punch_pose(poser, H, "L", "hit")),
            (16, punch_pose(poser, H, "L", "hit")), (22, punch_pose(poser, H, "R", "wind")),
            (26, punch_pose(poser, H, "R", "hit")), (30, punch_pose(poser, H, "R", "hit")), (44, base(poser, H))]
    return keys, [(12, "Hit"), (26, "Hit")]

def flail_clip(poser, H):
    """Both arms flail, slapping up and down in front out of phase for 2 s, the body wobbling; a hit on
    every downward slap."""
    keys = []
    for f in range(0, 61, 3):
        t = f / 60 * 6 * math.pi           # three slaps per arm
        p = {}
        for s, ph in (("L", 0), ("R", math.pi)):
            O = side_vec(s)
            e = math.sin(t + ph)
            el = math.sin(t + ph - .8)
            upper = n(O * .75, F * .75, U * (.05 + .55 * e))
            fore = n(O * .25, F * .85, U * (.1 + .7 * el))
            p.update(arm(s, upper, fore, n(fore, D * .4)))
        w = math.sin(t)
        p.update({"Spine": dict(rot=(4, 6 * math.cos(t), 8 * w)), "Head": dict(rot=(0, -5 * math.cos(t), -8 * w)),
                  "Root": dict(scale=(1 + .03 * w, 1, 1 - .03 * abs(w)), rot=(0, 3 * math.cos(t), 0))})
        keys.append((f, p))
    keys[0] = (0, base(poser, H)); keys[-1] = (60, base(poser, H))
    events = [(f, "Hit") for f in (5, 15, 25, 35, 45, 55)]
    return keys, events

def jump_clip(poser, H):
    """Crouch, launch high, hang with arms up, belly-flop smash. The game moves the body sideways between
    TakeOff and Land."""
    b = base(poser, H)
    up = merge(arm("L", n(X, U * .55, F * .3), n(X * .6, U * .5, F * .4), n(U, F * .5)), arm("R", n(-X, U * .55, F * .3), n(-X * .6, U * .5, F * .4), n(U, F * .5)))
    down = merge(arm("L", n(D, X * .6, F * .3), n(D, F * .2), n(D)), arm("R", n(D, -X * .6, F * .3), n(D, F * .2), n(D)))
    keys = [(0, b),
            (10, merge(b, {"Root": dict(scale=(1.15, 1.15, .78)), "Spine": dict(rot=(10, 0, 0))},
                       arm("L", n(X, F * .2, D * .5), n(X * .5, F * .3, D)), arm("R", n(-X, F * .2, D * .5), n(-X * .5, F * .3, D)))),
            (14, merge(up, {"Root": dict(loc=(0, 0, .25 * H), scale=(.88, .88, 1.18))})),
            (22, merge(up, {"Root": dict(loc=(0, 0, 1.0 * H), scale=(1, 1, 1.05)), "Spine": dict(rot=(-8, 0, 0))})),
            (30, merge(up, {"Root": dict(loc=(0, 0, .55 * H), scale=(.95, .95, 1.1)), "Spine": dict(rot=(12, 0, 0))})),
            (34, merge(down, {"Root": dict(loc=(0, 0, 0), scale=(1.3, 1.3, .68)), "Spine": dict(rot=(18, 0, 0)),
                              "Head": dict(rot=(-10, 0, 0))})),
            (40, merge(down, {"Root": dict(scale=(.95, .95, 1.06))})),
            (46, merge(b, {"Root": dict(scale=(1.03, 1.03, .97))})),
            (54, b)]
    return keys, [(14, "TakeOff"), (34, "Land")]

def spit_clip(poser, H):
    b = base(poser, H)
    keys = [(0, b),
            (12, merge(b, {"Spine": dict(rot=(-14, 0, 0)), "Head": dict(rot=(-22, 0, 0)),
                           "Root": dict(scale=(1.08, 1.05, 1.06), loc=(0, .04 * H, 0))},
                       arm("L", n(X, F * .2, D * .3), n(X * .5, F * .5, D * .3)), arm("R", n(-X, F * .2, D * .3), n(-X * .5, F * .5, D * .3)))),
            (16, merge(b, {"Spine": dict(rot=(-16, 0, 0)), "Head": dict(rot=(-26, 0, 0)),
                           "Root": dict(scale=(1.1, 1.06, 1.08), loc=(0, .05 * H, 0))})),
            (19, merge(b, {"Spine": dict(rot=(14, 0, 0)), "Head": dict(rot=(20, 0, 0)),
                           "Root": dict(scale=(.95, 1.08, .95), loc=(0, -.08 * H, 0))})),
            (25, merge(b, {"Spine": dict(rot=(10, 0, 0)), "Head": dict(rot=(12, 0, 0)),
                           "Root": dict(loc=(0, -.05 * H, 0))})),
            (40, b)]
    return keys, [(19, "Fire")]

def legs(phase, amp=30, knee=40, bob=0.0):
    """Leg swing for a run cycle; phase in radians."""
    out = {}
    for s, ph in (("L", 0), ("R", math.pi)):
        a = math.sin(phase + ph)
        out["UpLeg." + s] = dict(rot=(-amp * a, 0, 0))
        out["Leg." + s] = dict(rot=(knee * max(0, math.cos(phase + ph)) + 8, 0, 0))
        out["Foot." + s] = dict(rot=(-10 * a, 0, 0))
    return out

def charge_pose(poser, H, phase):
    p = {}
    for s, ph in (("L", math.pi), ("R", 0)):
        O = side_vec(s)
        a = math.sin(phase + ph)
        p.update(arm(s, n(BK * (.8 + .4 * a), O * .5, D * .5), n(BK * .6, D * .6, O * .2), n(BK * .3, D)))
    p.update(legs(phase, 35, 45))
    p.update({"Hips": dict(rot=(18, 0, 6 * math.sin(phase))), "Spine": dict(rot=(16, 0, -6 * math.sin(phase))),
              "Head": dict(rot=(-24, 0, 0)),
              "Root": dict(loc=(0, 0, .03 * H * abs(math.cos(phase))))})
    return p

def charge_clip(poser, H):
    keys = [(f, charge_pose(poser, H, f / 16 * 2 * math.pi)) for f in range(0, 17, 2)]
    return keys, []

def wallhit_clip(poser, H):
    """Running into a wall: slammed back, stagger, shake it off."""
    c = charge_pose(poser, H, 0)
    flung = merge(arm("L", n(X, BK * .5, D * .1), n(X, U * .3), n(X, U * .5)), arm("R", n(-X, BK * .5, D * .1), n(-X, U * .3), n(-X, U * .5)))
    dazed = merge(rest_arm(poser, "L", .45, .3), rest_arm(poser, "R", .45, .3))
    keys = [(0, c),
            (3, merge(flung, {"Hips": dict(rot=(-10, 0, 0)), "Spine": dict(rot=(-28, 0, 0)), "Head": dict(rot=(-30, 0, 0)),
                              "Root": dict(loc=(0, .08 * H, 0), scale=(1.06, .9, 1.0))},
                      legs(0, 10, 20))),
            (10, merge(flung, {"Hips": dict(rot=(-14, 0, 0)), "Spine": dict(rot=(-20, 6, 0)), "Head": dict(rot=(-18, 10, 0)),
                               "Root": dict(loc=(0, .18 * H, -.04 * H))},
                       legs(0, 5, 35))),
            (20, merge(dazed, {"Hips": dict(rot=(4, 10, 0)), "Spine": dict(rot=(10, 8, 6)), "Head": dict(rot=(16, 14, 10)),
                               "Root": dict(loc=(0, .16 * H, -.03 * H))}, legs(0, 0, 30))),
            (30, merge(dazed, {"Hips": dict(rot=(4, -10, 0)), "Spine": dict(rot=(10, -8, -6)), "Head": dict(rot=(16, -14, -10)),
                               "Root": dict(loc=(0, .14 * H, -.03 * H))}, legs(0, 0, 30))),
            (38, merge(dazed, {"Spine": dict(rot=(6, 4, 0)), "Head": dict(rot=(6, 0, 18)),
                               "Root": dict(loc=(0, .1 * H, -.02 * H))}, legs(0, 0, 20))),
            (43, merge(dazed, {"Spine": dict(rot=(4, 0, 0)), "Head": dict(rot=(4, 0, -18))}, legs(0, 0, 12))),
            (52, base(poser, H))]
    return keys, [(3, "Impact")]

def spin_pose(poser, H, angle, out=1.0):
    p = merge(arm("L", n(X, F * .15, D * (.35 + .3 * (1 - out))), n(X, F * .2, D * .2), n(X, F * .3, D * .2)),
              arm("R", n(-X, -F * .15, D * (.35 + .3 * (1 - out))), n(-X, -F * .2, D * .2), n(-X, -F * .3, D * .2)),
              legs(0, 0, 18))
    p.update({"Root": dict(rot=(0, 0, angle)), "Spine": dict(rot=(4, 0, 0)), "Head": dict(rot=(-4, 0, 0))})
    return p

def spin_clip(poser, H):
    keys = [(f, spin_pose(poser, H, -360 * f / 24)) for f in range(0, 25, 3)]
    return keys, []

def dizzy_clip(poser, H):
    keys = []
    for f in range(0, 31, 3):        # decelerating last turn
        t = f / 30
        ang = -360 * (1 - (1 - t) ** 2)
        keys.append((f, spin_pose(poser, H, ang, 1 - t)))
    dazed = merge(rest_arm(poser, "L", .5, .4), rest_arm(poser, "R", .5, .4))
    for i, f in enumerate(range(36, 73, 6)):
        a = i * math.pi / 2
        keys.append((f, merge(dazed, legs(0, 0, 28), {
            "Hips": dict(rot=(4, 10 * math.cos(a), 0)), "Spine": dict(rot=(8, 8 * math.cos(a), 0)),
            "Head": dict(rot=(10 * math.sin(a), 16 * math.cos(a), 12 * math.sin(a))),
            "Root": dict(rot=(4 * math.sin(a), 6 * math.cos(a), 0), loc=(0, 0, -.02 * H))})))
    keys.append((84, base(poser, H)))
    return keys, []

def flurry_clip(poser, H):
    """A flurry of punches: fists up in a guard, then eight jabs left-right-left-right as fast as it can throw
    them, the shoulders twisting into each one and the body leaning further in, then a breath to recover.
    A hit on every jab."""
    b = base(poser, H)
    guard = merge(arm("L", n(F * .6, X * .45, D * .25), n(F * .2, -X * .4, U * .8), n(F, U * .3)),
                  arm("R", n(F * .6, -X * .45, D * .25), n(F * .2, X * .4, U * .8), n(F, U * .3)))
    def jab(s, i):
        O = side_vec(s)
        tw = 1 if s == "L" else -1
        other = "R" if s == "L" else "L"
        Oo = side_vec(other)
        lean = min(1.0, i / 5)
        p = merge(arm(s, n(F, O * .05, U * .02), n(F, -O * .05), n(F, D * .15)),
                  arm(other, n(F * .3, BK * .45, Oo * .5, D * .2), n(F * .5, -Oo * .3, U * .7), n(F, U * .4)),
                  legs(0, 6, 22))
        p.update({"Hips": dict(rot=(4 + 4 * lean, 0, -10 * tw)), "Spine": dict(rot=(6 + 6 * lean, 0, -14 * tw)),
                  "Head": dict(rot=(-6 - 4 * lean, 0, 10 * tw)),
                  "Root": dict(loc=(.02 * H * tw, -(.06 + .06 * lean) * H, -.01 * H))})
        return p
    keys = [(0, b), (7, merge(guard, legs(0, 6, 24), {"Spine": dict(rot=(8, 0, 0)), "Head": dict(rot=(-8, 0, 0)),
                                                       "Root": dict(loc=(0, -.03 * H, -.02 * H))}))]
    events = []
    for i in range(8):
        f = 11 + i * 5
        keys.append((f, jab("L" if i % 2 == 0 else "R", i)))
        events.append((f, "Hit"))
    keys += [(52, jab("R", 7)), (60, merge(guard, {"Spine": dict(rot=(-4, 0, 0)), "Root": dict(loc=(0, 0, .01 * H))})),
             (72, b)]
    return keys, events

def laser_clip(poser, H):
    """Gather a charge between the claws (1.3 s), fire a beam for 1 s, recover."""
    b = base(poser, H)
    gather = merge(arm("L", n(F * .7, X * .5), n(F * .4, -X * .8), n(F * .3, -X)), arm("R", n(F * .7, -X * .5), n(F * .4, X * .8), n(F * .3, X)))
    fire = merge(arm("L", n(F, X * .2), n(F, -X * .1), n(F, U * .2)), arm("R", n(F, -X * .2), n(F, X * .1), n(F, U * .2)))
    keys = [(0, b), (8, merge(gather, {"Spine": dict(rot=(-6, 0, 0)), "Head": dict(rot=(-6, 0, 0)),
                                       "Root": dict(loc=(0, 0, .04 * H))}))]
    for i, f in enumerate(range(12, 41, 4)):
        j = 1 if i % 2 else -1
        keys.append((f, merge(gather, {"Spine": dict(rot=(-8 - i * .5, j * 1.5, 0)), "Head": dict(rot=(-8, 0, j * 2)),
                                       "Root": dict(loc=(j * .006 * H, 0, .05 * H + i * .004 * H),
                                                    scale=(1 + i * .006, 1 + i * .006, 1 + i * .006))})))
    keys.append((44, merge(fire, {"Spine": dict(rot=(-10, 0, 0)), "Head": dict(rot=(2, 0, 0)),
                                  "Root": dict(loc=(0, .05 * H, .08 * H))})))
    for i, f in enumerate(range(48, 75, 4)):
        j = 1 if i % 2 else -1
        keys.append((f, merge(fire, {"Spine": dict(rot=(-7, j * 1.2, 0)), "Head": dict(rot=(2, 0, j)),
                                     "Root": dict(loc=(j * .005 * H, .04 * H, .08 * H))})))
    keys += [(82, merge(gather, {"Spine": dict(rot=(4, 0, 0)), "Root": dict(loc=(0, 0, .03 * H))})), (96, b)]
    return keys, [(6, "Charge"), (44, "Fire"), (76, "FireEnd")]

def teleport_clip(poser, H):
    """Twist down into smoke, vanish (the game moves the body), twist back out."""
    b = base(poser, H)
    hug = merge(arm("L", n(F, -X * .3, D * .3), n(-X, F * .3), n(-X)), arm("R", n(F, X * .3, D * .3), n(X, F * .3), n(X)))
    keys = [(0, b),
            (6, merge(hug, {"Root": dict(scale=(1.08, 1.08, .9)), "Spine": dict(rot=(12, 0, 0))})),
            (10, merge(hug, {"Root": dict(rot=(0, 0, 120), scale=(.85, .85, 1.1), loc=(0, 0, .03 * H))})),
            (14, merge(hug, {"Root": dict(rot=(0, 0, 240), scale=(.45, .45, .7), loc=(0, 0, .05 * H))})),
            (18, merge(hug, {"Root": dict(rot=(0, 0, 359), scale=(.02, .02, .02), loc=(0, 0, .1 * H))})),
            (26, merge(hug, {"Root": dict(rot=(0, 0, 359), scale=(.02, .02, .02), loc=(0, 0, .1 * H))})),
            (30, merge(hug, {"Root": dict(rot=(0, 0, 240), scale=(.5, .5, .8), loc=(0, 0, .05 * H))})),
            (34, merge(hug, {"Root": dict(rot=(0, 0, 120), scale=(1.1, 1.1, 1.15))})),
            (38, merge(b, {"Root": dict(scale=(1.06, 1.06, .95))}, arm("L", n(X, U * .4), n(X, U * .5)), arm("R", n(-X, U * .4), n(-X, U * .5)))),
            (48, b)]
    return keys, [(18, "Vanish"), (26, "Appear")]

def spinshoot_clip(poser, H):
    """Two full turns with the claws out, a bolt from each claw every 6 frames."""
    keys = [(0, base(poser, H))]
    out = merge(arm("L", n(X, F * .25), n(X, F * .3), n(X, F * .3)), arm("R", n(-X, -F * .25), n(-X, -F * .3), n(-X, -F * .3)))
    for f in range(6, 67, 3):
        ang = -720 * (f - 6) / 60
        keys.append((f, merge(out, {"Root": dict(rot=(0, 0, ang), loc=(0, 0, .08 * H)), "Spine": dict(rot=(-4, 0, 0))})))
    keys.append((76, base(poser, H)))
    return keys, [(f, "Fire") for f in range(9, 64, 6)]

def slash_pose(poser, H, s, phase):
    O = side_vec(s)
    tw = 1 if s == "L" else -1
    p = base(poser, H)
    if phase == "wind":
        p.update(arm(s, n(O * .6, U * .6, BK * .3), n(U * .8, O * .3), n(U, F * .3)))
        p.update({"Spine": dict(rot=(-6, 0, 14 * tw)), "Head": dict(rot=(4, 0, -8 * tw)), "Root": dict(loc=(0, .03 * H, .03 * H))})
    else:
        p.update(arm(s, n(F * .8, -O * .45, D * .35), n(F * .5, -O * .75, D * .3), n(-O, D * .3, F * .3)))
        p.update({"Spine": dict(rot=(10, 0, -16 * tw)), "Head": dict(rot=(-6, 0, 10 * tw)), "Root": dict(loc=(0, -.12 * H, 0))})
    return p

def slash_clip(poser, H):
    """Rake with the left claw then the right, swooping forward on each swipe."""
    keys = [(0, base(poser, H)), (6, slash_pose(poser, H, "L", "wind")), (10, slash_pose(poser, H, "L", "hit")),
            (13, slash_pose(poser, H, "L", "hit")), (18, slash_pose(poser, H, "R", "wind")),
            (22, slash_pose(poser, H, "R", "hit")), (25, slash_pose(poser, H, "R", "hit")), (36, base(poser, H))]
    return keys, [(10, "Hit"), (22, "Hit")]

def cast_clip(poser, H, lift=.06):
    """Channelling a bullet pattern: claws raised and spread, hovering a little higher, pulsing. Loops."""
    keys = []
    for i, f in enumerate((0, 10, 20, 30, 40)):
        s = math.sin(i * math.pi / 2)
        p = merge(arm("L", n(X, U * (.5 + .1 * s), F * .2), n(X * .6, U * .7, F * .2), n(U, X * .3)),
                  arm("R", n(-X, U * (.5 + .1 * s), F * .2), n(-X * .6, U * .7, F * .2), n(U, -X * .3)))
        p.update({"Root": dict(loc=(0, 0, (lift + .015 * s) * H), scale=(1 + .015 * s, 1 + .015 * s, 1 + .015 * s)),
                  "Spine": dict(rot=(-8 - 2 * s, 0, 0)), "Head": dict(rot=(-6, 0, 0))})
        keys.append((f, p))
    return keys

def ball_idle(poser, H):
    keys = []
    for i, f in enumerate((0, 15, 30, 45, 60)):
        s = math.sin(i * math.pi / 2)
        keys.append((f, {"Root": dict(loc=(0, 0, .06 * H * s), scale=(1 + .04 * s, 1 + .04 * s, 1 - .05 * s),
                                      rot=(0, 3 * math.cos(i * math.pi / 2), 0))}))
    return keys

def lunge_clip(poser, H):
    keys = [(0, {}),
            (8, {"Root": dict(loc=(0, .12 * H, -.02 * H), scale=(1.15, .8, 1.12))}),
            (12, {"Root": dict(loc=(0, -.1 * H, .02 * H), scale=(.85, 1.35, .88), rot=(-10, 0, 0))}),
            (18, {"Root": dict(loc=(0, -.06 * H, 0), scale=(.9, 1.2, .9), rot=(-6, 0, 0))}),
            (22, {"Root": dict(scale=(1.12, .85, 1.1))}),
            (30, {})]
    return keys, [(10, "Hit")]

def clips_for(name, spec, poser, H):
    kind = spec["kind"]
    out = []
    if kind == "ball":
        out.append(("Idle", 60, ball_idle(poser, H), True, []))
        k, e = lunge_clip(poser, H); out.append(("Lunge", 30, k, False, e))
        out.append(("Move", 20, ball_move(poser, H), True, []))
        return out
    if kind in ("muscle", "brute"):
        poser.lean = spec.get("lean", 1.0)
        poser.stance = muscle_stance(0.0, 0.0, poser.lean)
    if kind in ("fat", "muscle", "brute"):
        out.append(("Idle", 60, ground_idle(poser, H, squash=kind == "fat"), True, []))
        out.append(("Move", 32, fat_waddle(poser, H) if kind == "fat" else muscle_walk(poser, H), True, []))
    else:
        out.append(("Idle", 60, float_idle(poser, H), True, []))
        out.append(("Move", 32, float_move(poser, H), True, []))
    if kind == "shooter":
        k, e = shoot_clip(poser, H, spec["hands"]); out.append(("Shoot", 36, k, False, e))
        if spec.get("teleport"):
            k, e = teleport_clip(poser, H); out.append(("Teleport", 48, k, False, e))
    elif kind == "claw":
        k, e = shoot_clip(poser, H, spec["hands"]); out.append(("Shoot", 36, k, False, e))
        k, e = slash_clip(poser, H); out.append(("Slash", 36, k, False, e))
        if spec.get("teleport"):
            k, e = teleport_clip(poser, H); out.append(("Teleport", 48, k, False, e))
    elif kind == "puncher":
        k, e = punch_clip(poser, H, spec.get("combo")); out.append(("Punch", 44 if spec.get("combo") else 30, k, False, e))
        if spec.get("shoot"):
            k, e = shoot_clip(poser, H, spec["shoot"]); out.append(("Shoot", 36, k, False, e))
    elif kind == "fat":
        k, e = flail_clip(poser, H); out.append(("Flail", 60, k, False, e))
        k, e = jump_clip(poser, H); out.append(("Jump", 54, k, False, e))
        k, e = spit_clip(poser, H); out.append(("Spit", 40, k, False, e))
    elif kind in ("muscle", "brute"):
        k, e = charge_clip(poser, H); out.append(("Charge", 16, k, True, e))
        k, e = wallhit_clip(poser, H); out.append(("ChargeWallHit", 52, k, False, e))
        if kind == "muscle":
            k, e = punch_clip(poser, H, False); out.append(("Punch", 30, k, False, e))
        if kind == "brute":
            k, e = spin_clip(poser, H); out.append(("Spin", 24, k, True, e))
            k, e = dizzy_clip(poser, H); out.append(("SpinDizzy", 84, k, False, e))
            k, e = flurry_clip(poser, H); out.append(("Flurry", 72, k, False, e))
            out.append(("Cast", 40, cast_clip(poser, H, lift=0.0), True, []))
    elif kind == "skull":
        k, e = laser_clip(poser, H); out.append(("ChargedLaser", 96, k, False, e))
        k, e = teleport_clip(poser, H); out.append(("Teleport", 48, k, False, e))
        k, e = spinshoot_clip(poser, H); out.append(("SpinShoot", 76, k, False, e))
        k, e = shoot_clip(poser, H, "R"); out.append(("Throw", 36, k, False, e))
        out.append(("Cast", 40, cast_clip(poser, H), True, []))
    return out

# ---------------------------------------------------------------------------------------------- textures

def paint_eyes(albedo, mesh, spec):
    """The shadow balls have no eyes in their texture: paint the angry glowing half-moon eyes of the concept
    art onto the front of the ball, by rasterizing the front-facing triangles in UV space."""
    eyes = spec["eyes"]
    lo, hi = bbox(mesh)
    d = hi - lo
    cx, cz = lo.x + eyes["c"][0] * d.x, hi.z - eyes["c"][1] * d.z
    R = eyes["r"] * d.x
    spread, w, h, tilt = eyes.get("spread", .36), eyes.get("w", .27), eyes.get("h", .22), eyes.get("tilt", .55)
    img = albedo.reshape(TEX_SIZE, TEX_SIZE, 4)
    white, violet = np.array([.95, .88, 1.0]), np.array([.62, .38, 1.0])

    def mask(x, z):
        ex, ez = (x - cx) / R, (z - cz) / R
        m = np.zeros_like(ex)
        core = np.zeros_like(ex)
        for s in (1, -1):
            a = s * ex - spread
            ell = (a / w) ** 2 + (ez / h) ** 2
            top = h * 0.25 + tilt * a            # slanted upper lid, lower towards the middle: angry
            mm = np.clip((1.0 - ell) / 0.15, 0, 1) * np.clip((top - ez) / 0.03, 0, 1)
            m = np.maximum(m, mm)
            core = np.maximum(core, np.clip((0.75 - ell) / 0.5, 0, 1) * mm)
        return m, core

    me = mesh.data
    me.calc_loop_triangles()
    uv = me.uv_layers.active.data
    co = [v.co for v in me.vertices]
    painted = 0
    for tri in me.loop_triangles:
        if tri.normal.y > -0.35:
            continue                              # only the front of the ball
        P = [co[i] for i in tri.vertices]
        xs, zs = [p.x for p in P], [p.z for p in P]
        if max(abs(cx - x) for x in xs) > R * 0.9 or max(abs(cz - z) for z in zs) > R * 0.5:
            continue
        T = np.array([uv[l].uv[:] for l in tri.loops]) * TEX_SIZE
        x0, y0 = np.floor(T.min(0)).astype(int)
        x1, y1 = np.ceil(T.max(0)).astype(int)
        if x1 - x0 > 256 or y1 - y0 > 256:
            continue
        gx, gy = np.meshgrid(np.arange(x0, x1 + 1) + .5, np.arange(y0, y1 + 1) + .5)
        (ax, ay), (bx, by), (qx, qy) = T
        den = (by - qy) * (ax - qx) + (qx - bx) * (ay - qy)
        if abs(den) < 1e-9:
            continue
        l1 = ((by - qy) * (gx - qx) + (qx - bx) * (gy - qy)) / den
        l2 = ((qy - ay) * (gx - qx) + (ax - qx) * (gy - qy)) / den
        l3 = 1 - l1 - l2
        inside = (l1 >= -0.02) & (l2 >= -0.02) & (l3 >= -0.02)
        if not inside.any():
            continue
        px = l1 * xs[0] + l2 * xs[1] + l3 * xs[2]
        pz = l1 * zs[0] + l2 * zs[1] + l3 * zs[2]
        m, core = mask(px, pz)
        m *= inside
        sel = m > 0
        if not sel.any():
            continue
        ix = np.clip(gx[sel].astype(int), 0, TEX_SIZE - 1)
        iy = np.clip(gy[sel].astype(int), 0, TEX_SIZE - 1)
        col = violet[None] + (white - violet)[None] * core[sel][:, None]
        k = m[sel][:, None]
        img[iy, ix, :3] = img[iy, ix, :3] * (1 - k) + col * k
        painted += int(sel.sum())
    print("EYES", spec["folder"], painted, "texels")

def save_textures(name, tex_dir, mesh, spec):
    os.makedirs(tex_dir, exist_ok=True)
    imgs = {}
    for img in bpy.data.images:
        if img.size[0] == 0:
            continue
        stem = os.path.splitext(img.name)[0]
        kind = "albedo"
        for k in ("metallic", "normal", "roughness"):
            if stem.endswith("_" + k):
                kind = k
        imgs[kind] = img
    if "albedo" not in imgs and "base_color" not in spec:
        print("NO_TEXTURES", name)
        return False

    def pixels(img):
        if img.size[0] != TEX_SIZE:
            img.scale(TEX_SIZE, TEX_SIZE)
        p = np.empty(TEX_SIZE * TEX_SIZE * 4, np.float32)
        img.pixels.foreach_get(p)
        return p

    def write(kind, p, colorspace):
        out = bpy.data.images.new(f"{name}_{kind}", TEX_SIZE, TEX_SIZE, alpha=True)
        out.colorspace_settings.name = colorspace
        out.pixels.foreach_set(p)
        out.filepath_raw = os.path.join(tex_dir, f"{name}_{kind}.png")
        out.file_format = 'PNG'
        out.save()

    if "albedo" in imgs:
        albedo = pixels(imgs["albedo"])
    else:
        # untextured model (twisted_ball): a flat smoky base colour
        albedo = np.tile(np.array(list(spec["base_color"]) + [1.0], np.float32), TEX_SIZE * TEX_SIZE)
    if "eyes" in spec:
        paint_eyes(albedo, mesh, spec)
    write("albedo", albedo, 'sRGB')
    if "normal" in imgs:
        write("normal", pixels(imgs["normal"]), 'Non-Color')
    met = pixels(imgs["metallic"]).reshape(-1, 4) if "metallic" in imgs else np.zeros((TEX_SIZE * TEX_SIZE, 4), np.float32)
    rough = pixels(imgs["roughness"]).reshape(-1, 4) if "roughness" in imgs else np.full((TEX_SIZE * TEX_SIZE, 4), .7, np.float32)
    ms = np.empty_like(met)
    ms[:, 0] = ms[:, 1] = ms[:, 2] = met[:, 0]
    ms[:, 3] = 1 - rough[:, 0]
    write("metallicSmoothness", ms.ravel(), 'Non-Color')
    return True

# ---------------------------------------------------------------------------------------------- preview

def render_preview(name, rig, mesh, clips, H):
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_WORKBENCH'
    sc.display.shading.light = 'STUDIO'
    sc.display.shading.color_type = 'SINGLE'
    sc.render.resolution_x = sc.render.resolution_y = 260
    sc.render.film_transparent = False
    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam'))
    sc.collection.objects.link(cam); sc.camera = cam
    lo, hi = bbox(mesh)
    cam.data.type = 'ORTHO'; cam.data.ortho_scale = max(hi - lo) * 1.35
    d = Vector((-0.6, -1, 0.3)).normalized()
    cam.location = Vector((0, 0, H * 0.6)) + d * 10
    cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
    os.makedirs(PREVIEW, exist_ok=True)
    for act, clip, ks in clips:
        rig.animation_data.action = act
        pick = [ks[round(i * (len(ks) - 1) / 5)] for i in range(6)] if len(ks) > 6 else ks
        for i, f in enumerate(pick):
            sc.frame_set(f)
            hands = [rig.matrix_world @ rig.pose.bones[b].head for b in ("Muzzle.L", "Muzzle.R") if b in rig.pose.bones]
            print("POSE", name, clip["name"], f, " ".join("(%.2f %.2f %.2f)" % tuple(h) for h in hands))
            sc.render.filepath = os.path.join(PREVIEW, f"{name}__{clip['name']}__{i}.png")
            bpy.ops.render.render(write_still=True)

# ---------------------------------------------------------------------------------------------- main

def export(name, spec):
    folder = os.path.join(OUT, spec["folder"])
    os.makedirs(folder, exist_ok=True)
    mesh = load(name)
    k = normalize(mesh, spec)
    has_tex = save_textures(name, os.path.join(folder, "Textures"), mesh, spec)
    rig, radius = build_rig(mesh, spec)
    skin(mesh, rig, radius, spec.get("limbs_only_outside", False), spec.get("hard_hands", False))
    lo, hi = bbox(mesh)
    H = hi.z
    poser = Poser(rig, H)
    sc = bpy.context.scene
    sc.render.fps = FPS
    clips = []
    for cname, length, keys, loop, events in clips_for(name, spec, poser, H):
        clips.append(make_clip(rig, poser, cname, length, keys, loop, events))
    # rest pose for export, every action kept for the "all actions" bake
    rig.animation_data.action = None
    for pb in rig.pose.bones:
        pb.rotation_quaternion = Quaternion(); pb.location = Vector(); pb.scale = Vector((1, 1, 1))
    for m in mesh.data.materials:
        if m:
            m.name = name + "_mat"
    sc.frame_start, sc.frame_end = 0, max(c["frames"] for _, c, _k in clips)
    for o in bpy.data.objects:
        o.select_set(o in (rig, mesh))
    bpy.context.view_layer.objects.active = rig
    path = os.path.join(folder, name + ".fbx")
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={'ARMATURE', 'MESH'},
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
        mesh_smooth_type='FACE', use_mesh_modifiers=False, add_leaf_bones=False,
        primary_bone_axis='Y', secondary_bone_axis='X', armature_nodetype='NULL',
        path_mode='STRIP', embed_textures=False,
        bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=True, bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0)
    meta = dict(name=name, size=round(max(hi - lo), 4), height=round(H, 4), hover=round(lo.z, 4),
                scale=round(k, 4), textured=has_tex, clips=[c for _, c, _k in clips])
    with open(os.path.join(folder, name + ".clips.json"), "w") as fh:
        json.dump(meta, fh, indent=1)
    print("EXPORTED", name, "size %.3f height %.3f bottom %.3f" % (max(hi - lo), H, lo.z),
          "clips", [c["name"] for _, c, _k in clips])
    if PREVIEW:
        render_preview(name, rig, mesh, clips, H)

for name, spec in MONSTERS.items():
    if ONLY and name not in ONLY:
        continue
    export(name, spec)
