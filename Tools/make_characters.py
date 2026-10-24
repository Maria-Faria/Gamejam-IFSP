# Rigs and animates the seven playable scientists (F:/fatec/char/CAHRS/*.fbx) for the game, and prepares the
# pistol they carry (F:/fatec/decor/decor/gun.fbx).
# Five of the models come with a Mixamo skeleton and skin weights, which are kept (the "mixamorig:" prefix
# is dropped). bibha and ednalva come without one: Annie's skeleton is stretched over their bounding box and
# the skin is weighted to it by Blender's automatic (bone heat) weights — the seven share the same chibi
# T-pose; if bone heat fails, Annie's own weights are copied across by nearest surface instead.
# Clips (Blender space: the characters face -Y, their left is +X, Z up):
#   Idle      breathing, arms down, the pistol held low in the right hand
#   Run_F, Run_B, Run_L, Run_R, Run_FL, Run_FR, Run_BL, Run_BR
#             a running cycle in each of the 8 directions relative to where she faces (legs swing along the
#             direction of travel, the body leans into it); the game blends them on the local velocity
#   Aim       the right arm raised straight ahead, pistol up to shoot (an upper-body layer in the game)
# The pistol's FBX comes without its textures: its colours are projected from the side-view concept art
# (GUN_ART, "Pistola de cristal em perfil direito") across the mesh.
# usage: blender -b -P make_characters.py -- <src_dir> <gun_fbx> <out_dir> [preview_dir|-] [only_key ...]
# Outputs: <out_dir>/<Key>/<key>.fbx, <key>.clips.json, Textures/<key>_{albedo,normal,metallicSmoothness}.png
#          <out_dir>/Pistol/pistol.fbx, pistol.json (grip and muzzle), Textures/pistol_*.png
import bpy, math, os, sys, json
import numpy as np
from mathutils import Matrix, Vector, Quaternion

args = sys.argv[sys.argv.index('--') + 1:]
SRC, GUN, OUT = args[0], args[1], args[2]
PREVIEW = args[3] if len(args) > 3 and args[3] != '-' else None
ONLY = set(args[4:])
FPS = 30
TEX_SIZE = 2048
WEIGHT_SOURCE = "annie"
GUN_ART = "F:/fatec/decor/Pistola de cristal em perfil direito-9.png"

# key (= Unity folder/prefab name), source file; in the order of the character selection menu
CHARACTERS = [
    ("Enedina", "ednalva"),
    ("Hertha", "hertha"),
    ("Maria", "maria"),
    ("Chien", "chien"),
    ("Patricia", "patricia"),
    ("Annie", "annie"),
    ("Bibha", "bibha"),
]

F, BK, U, D, X = Vector((0, -1, 0)), Vector((0, 1, 0)), Vector((0, 0, 1)), Vector((0, 0, -1)), Vector((1, 0, 0))


def n(*vs):
    v = Vector((0, 0, 0))
    for a in vs:
        v += a
    return v.normalized()

# ---------------------------------------------------------------------------------------------- loading


def import_fbx(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    for o in [o for o in new if o.type not in ('MESH', 'ARMATURE')]:
        bpy.data.objects.remove(o)
    new = [o for o in bpy.data.objects if o not in before]
    arm = next((o for o in new if o.type == 'ARMATURE'), None)
    mesh = next(o for o in new if o.type == 'MESH')
    return arm, mesh


def bake_transforms(arm, mesh):
    """Bakes the object transforms into the data so everything is in metres, world space."""
    if mesh.parent:
        mw = mesh.matrix_world.copy()
        mesh.parent = None
        mesh.matrix_world = mw
    for o in bpy.data.objects:
        o.select_set(o in (arm, mesh))
    bpy.context.view_layer.objects.active = arm or mesh
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)


def bbox(mesh):
    co = np.empty(len(mesh.data.vertices) * 3, np.float32)
    mesh.data.vertices.foreach_get('co', co)
    co = co.reshape(-1, 3)
    return Vector(co.min(0)), Vector(co.max(0))


def strip_prefix(arm, mesh):
    for b in arm.data.bones:
        b.name = b.name.replace("mixamorig:", "")
    for g in mesh.vertex_groups:
        g.name = g.name.replace("mixamorig:", "")


def borrow_rig(mesh, src_path):
    """Stretches the reference character's rig over `mesh` and copies its skin weights across."""
    arm, ref = import_fbx(src_path)
    bake_transforms(arm, ref)
    strip_prefix(arm, ref)
    lo, hi = bbox(mesh)
    rlo, rhi = bbox(ref)
    s = Vector(((hi - lo)[i] / max((rhi - rlo)[i], 1e-6) for i in range(3)))
    fit = Matrix.Translation(lo) @ Matrix.Diagonal(s.to_4d()) @ Matrix.Translation(-rlo)
    for o in (arm, ref):
        o.matrix_world = fit @ o.matrix_world
    bake_transforms(arm, ref)
    for mod in list(ref.modifiers):
        ref.modifiers.remove(mod)
    # bone heat first: weights that follow this body's own shape
    for o in bpy.data.objects:
        o.select_set(o in (mesh, arm))
    bpy.context.view_layer.objects.active = arm
    try:
        bpy.ops.object.parent_set(type='ARMATURE_AUTO')
        weighted = sum(1 for v in mesh.data.vertices if v.groups)
    except RuntimeError:
        weighted = 0
    if weighted > len(mesh.data.vertices) * 0.98:
        bpy.data.objects.remove(ref)
        print("WEIGHTS bone heat", weighted, "/", len(mesh.data.vertices))
        return arm
    print("WEIGHTS bone heat failed (", weighted, "), copying from", WEIGHT_SOURCE)
    mesh.parent = None
    for m in list(mesh.modifiers):
        mesh.modifiers.remove(m)
    mesh.vertex_groups.clear()
    for g in ref.vertex_groups:
        mesh.vertex_groups.new(name=g.name)
    dt = mesh.modifiers.new("weights", 'DATA_TRANSFER')
    dt.object = ref
    dt.use_vert_data = True
    dt.data_types_verts = {'VGROUP_WEIGHTS'}
    dt.vert_mapping = 'POLYINTERP_NEAREST'
    dt.layers_vgroup_select_src = 'ALL'
    dt.layers_vgroup_select_dst = 'NAME'
    bpy.context.view_layer.objects.active = mesh
    bpy.ops.object.modifier_apply(modifier=dt.name)
    bpy.data.objects.remove(ref)
    mod = mesh.modifiers.new("Armature", 'ARMATURE')
    mod.object = arm
    return arm


def load_character(src):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    arm, mesh = import_fbx(os.path.join(SRC, src + ".fbx"))
    bake_transforms(arm, mesh)
    if arm is None:
        arm = borrow_rig(mesh, os.path.join(SRC, WEIGHT_SOURCE + ".fbx"))
    else:
        strip_prefix(arm, mesh)
    # feet on the ground, centred
    lo, hi = bbox(mesh)
    shift = Matrix.Translation((-(lo.x + hi.x) / 2, -(lo.y + hi.y) / 2, -lo.z))
    mesh.data.transform(shift)
    arm.data.transform(shift)
    mesh.data.update()
    if not any(m.type == 'ARMATURE' for m in mesh.modifiers):
        mesh.modifiers.new("Armature", 'ARMATURE').object = arm
    for m in mesh.modifiers:
        if m.type == 'ARMATURE':
            m.object = arm
    mesh.parent = arm
    return arm, mesh

# ---------------------------------------------------------------------------------------------- textures


def save_textures(name, tex_dir, mesh=None):
    os.makedirs(tex_dir, exist_ok=True)
    imgs = {}
    # only this model's own images (the reference rig's FBX may be loaded in the same file)
    own = None
    if mesh is not None:
        own = {nd.image for m in mesh.data.materials if m and m.use_nodes for nd in m.node_tree.nodes
               if nd.type == 'TEX_IMAGE' and nd.image}
    for img in bpy.data.images:
        if own is not None and img not in own:
            continue
        if img.size[0] == 0:
            continue
        stem = os.path.splitext(img.name)[0]
        kind = "albedo"
        for k in ("metallic", "normal", "roughness"):
            if stem.endswith("_" + k):
                kind = k
        imgs.setdefault(kind, img)

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

    if "albedo" not in imgs:
        return False
    write("albedo", pixels(imgs["albedo"]), 'sRGB')
    if "normal" in imgs:
        write("normal", pixels(imgs["normal"]), 'Non-Color')
    met = pixels(imgs["metallic"]).reshape(-1, 4) if "metallic" in imgs else np.zeros((TEX_SIZE * TEX_SIZE, 4), np.float32)
    rough = pixels(imgs["roughness"]).reshape(-1, 4) if "roughness" in imgs else np.full((TEX_SIZE * TEX_SIZE, 4), .7, np.float32)
    ms = np.empty_like(met)
    ms[:, 0] = ms[:, 1] = ms[:, 2] = met[:, 0]
    ms[:, 3] = 1 - rough[:, 0]
    write("metallicSmoothness", ms.ravel(), 'Non-Color')
    return True


def project_side_art(mesh, art, tex_dir, name):
    """Colours a model from a side-view picture of it: a new UV map projects the picture straight across
    (along X), its barrel (-Y) to the right of the picture, so both sides show the artwork."""
    img = bpy.data.images.load(art)
    w, h = img.size
    px = np.empty(w * h * 4, np.float32)
    img.pixels.foreach_get(px)
    px = px.reshape(h, w, 4)
    rows = np.where(px[..., 3].max(1) > .5)[0]
    cols = np.where(px[..., 3].max(0) > .5)[0]
    u0, u1, v0, v1 = cols.min() / w, (cols.max() + 1) / w, rows.min() / h, (rows.max() + 1) / h
    # opaque background behind the art so the texture filtering does not bleed black in
    a = px[..., 3:4]
    mean = (px[..., :3] * a).sum((0, 1)) / max(a.sum(), 1)
    px[..., :3] = px[..., :3] * a + mean * (1 - a)
    px[..., 3] = 1
    os.makedirs(tex_dir, exist_ok=True)
    out = bpy.data.images.new(f"{name}_albedo", w, h, alpha=True)
    out.pixels.foreach_set(px.ravel())
    out.filepath_raw = os.path.join(tex_dir, f"{name}_albedo.png")
    out.file_format = 'PNG'
    out.save()
    me = mesh.data
    while me.uv_layers:
        me.uv_layers.remove(me.uv_layers[0])
    uv = me.uv_layers.new(name="UVMap")
    lo, hi = bbox(mesh)
    for loop in me.loops:
        co = me.vertices[loop.vertex_index].co
        fu = (hi.y - co.y) / (hi.y - lo.y)          # barrel tip (-Y) on the right
        fv = (co.z - lo.z) / (hi.z - lo.z)
        uv.data[loop.index].uv = (u0 + fu * (u1 - u0), v0 + fv * (v1 - v0))
    ms = np.zeros((w * h, 4), np.float32)
    ms[:, 3] = .45
    out2 = bpy.data.images.new(f"{name}_metallicSmoothness", w, h, alpha=True)
    out2.pixels.foreach_set(ms.ravel())
    out2.filepath_raw = os.path.join(tex_dir, f"{name}_metallicSmoothness.png")
    out2.file_format = 'PNG'
    out2.save()

# ---------------------------------------------------------------------------------------------- posing


class Poser:
    """Keys poses written in the rest-pose space (same scheme as make_monsters.py). Rotations ("rot", degrees
    about X, Y, Z) are deltas relative to the parent in rest-pose world axes; limb bones can instead be aimed
    ("aim") at a direction in the body's frame. "loc" moves a bone (metres, world axes)."""

    def __init__(self, rig):
        self.rig = rig
        self.rest = {b.name: b.matrix_local.to_3x3() for b in rig.data.bones}
        self.dir = {b.name: (b.tail_local - b.head_local).normalized() for b in rig.data.bones}

    def key(self, frame, pose):
        rig = self.rig
        acc = {}
        for pb in rig.pose.bones:
            pb.rotation_mode = 'QUATERNION'
            pb.rotation_quaternion = Quaternion()
            pb.location = Vector()
            pb.scale = Vector((1, 1, 1))
        for name, spec in pose.items():
            if name not in rig.pose.bones:
                continue
            pb = rig.pose.bones[name]
            R = self.rest[name]
            if "aim" in spec:
                parent = rig.data.bones[name].parent
                Ap = acc.get(parent.name, Matrix.Identity(3)) if parent else Matrix.Identity(3)
                carried = Ap @ self.dir[name]
                A = carried.rotation_difference(spec["aim"].normalized()).to_matrix() @ Ap
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
        for pb in rig.pose.bones:
            pb.keyframe_insert("rotation_quaternion", frame=frame)
            pb.keyframe_insert("location", frame=frame)


def make_clip(rig, poser, name, length, keys, loop):
    act = bpy.data.actions.new(name)
    act.use_fake_user = True
    rig.animation_data_create()
    rig.animation_data.action = act
    for f, pose in keys:
        poser.key(f, pose)
    return act, dict(name=name, frames=length, loop=loop, events=[]), [f for f, _ in keys]


def merge(*ds):
    out = {}
    for d in ds:
        out.update(d)
    return out


def side(s):
    return X if s == "Left" else -X


def arm_down(s, swing=0.0):
    """An arm hanging at her side, out a little to clear the wide body; swing -1..1 forward/back."""
    O = side(s)
    return {s + "Arm": dict(aim=n(D * 1.0, O * .42, F * .35 * swing)),
            s + "ForeArm": dict(aim=n(D * 1.0, O * .25, F * (.25 + .45 * swing))),
            s + "Hand": dict(aim=n(D * 1.0, O * .2, F * (.3 + .4 * swing)))}


def gun_low(swing=0.0):
    """The right arm carrying the pistol low, pointed ahead and down."""
    O = side("Right")
    return {"RightArm": dict(aim=n(D, O * .4, F * (.25 + .2 * swing))),
            "RightForeArm": dict(aim=n(D * .55, F * 1.0, O * .15)),
            "RightHand": dict(aim=n(D * .35, F * 1.0, O * .05))}


def aim_arm():
    """The right arm raised straight ahead at shoulder height, pistol up."""
    O = side("Right")
    return {"RightShoulder": dict(rot=(0, 0, -6)),
            "RightArm": dict(aim=n(F, O * .12, U * .08)),
            "RightForeArm": dict(aim=n(F, -O * .06, U * .04)),
            "RightHand": dict(aim=n(F, U * .02))}


def stance(breath=0.0):
    return merge(arm_down("Left"), gun_low(),
                 {"Spine": dict(rot=(-1.5 * breath, 0, 0)), "Spine2": dict(rot=(-1.5 * breath, 0, 0)),
                  "Head": dict(rot=(1.5 * breath, 0, 0)),
                  "Hips": dict(loc=(0, 0, -.004 * breath))})


def idle_clip():
    keys = []
    for i, f in enumerate(range(0, 61, 15)):
        b = math.sin(i * math.pi / 2)
        p = stance(b)
        p["Head"] = dict(rot=(1.5 * b, 0, 2.5 * math.cos(i * math.pi / 2)))
        keys.append((f, p))
    return keys


def legs(d, phase, stride=42, lift=62):
    """Legs running along direction d (body frame): each thigh swings along d, the knee folds back as the
    foot comes up, the foot stays roughly level."""
    out = {}
    for s, ph in (("Left", 0.0), ("Right", math.pi)):
        a = math.sin(phase + ph)
        up = max(0.0, math.cos(phase + ph))          # the leg coming forward is lifted
        th = math.radians(stride) * a
        thigh = n(D * math.cos(th), d * math.sin(th) + F * .12 * up)
        knee = math.radians(lift) * (0.25 + 0.75 * up)
        shin = n(thigh * math.cos(knee), BK * math.sin(knee))
        out[s + "UpLeg"] = dict(aim=thigh)
        out[s + "Leg"] = dict(aim=shin)
        out[s + "Foot"] = dict(aim=n(F, D * (.9 - .5 * up)))
    return out


def run_clip(d, length=16):
    """A running cycle along d (unit vector in the body frame)."""
    keys = []
    lean = 9
    for f in range(0, length + 1, 2):
        ph = f / length * 2 * math.pi
        a = math.sin(ph)
        p = merge(legs(d, ph), arm_down("Left", -a * 1.2), gun_low(a * .5))
        p["Hips"] = dict(loc=(0, 0, .022 * abs(math.cos(ph)) - .012), rot=(0, 0, 7 * a * (-d.y)))
        # lean into the run: forward/back about X, sideways about Y
        p["Spine"] = dict(rot=(lean * -d.y, lean * d.x, -5 * a * (-d.y)))
        p["Spine2"] = dict(rot=(2 * -d.y, 2 * d.x, 0))
        p["Head"] = dict(rot=(-lean * -d.y * .6, -lean * d.x * .5, 0))
        keys.append((f, p))
    return keys


def aim_clip():
    p = merge(stance(), aim_arm(), {"Spine2": dict(rot=(0, 0, -8))})
    return [(0, p), (10, p)]


RUNS = [("Run_F", F), ("Run_B", BK), ("Run_L", X), ("Run_R", -X),
        ("Run_FL", n(F, X)), ("Run_FR", n(F, -X)), ("Run_BL", n(BK, X)), ("Run_BR", n(BK, -X))]


def clips():
    out = [("Idle", 60, idle_clip(), True)]
    for name, d in RUNS:
        out.append((name, 16, run_clip(d), True))
    out.append(("Aim", 10, aim_clip(), True))
    return out

# ---------------------------------------------------------------------------------------------- export


def export_fbx(path, objs, anim):
    for o in bpy.data.objects:
        o.select_set(o in objs)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={'ARMATURE', 'MESH'},
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
        mesh_smooth_type='FACE', use_mesh_modifiers=False, add_leaf_bones=False,
        primary_bone_axis='Y', secondary_bone_axis='X', armature_nodetype='NULL',
        path_mode='STRIP', embed_textures=False,
        bake_anim=anim, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=True, bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0)


def render_preview(key, rig, mesh, acts):
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_WORKBENCH'
    sc.display.shading.light = 'STUDIO'
    sc.display.shading.color_type = 'SINGLE'
    sc.render.resolution_x, sc.render.resolution_y = 220, 280
    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam'))
    sc.collection.objects.link(cam)
    sc.camera = cam
    lo, hi = bbox(mesh)
    cam.data.type = 'ORTHO'
    cam.data.ortho_scale = (hi.z - lo.z) * 1.35
    os.makedirs(PREVIEW, exist_ok=True)
    for act, clip, ks in acts:
        rig.animation_data.action = act
        for view, d in (("front", Vector((-.35, -1, .2))), ("side", Vector((-1, -.25, .2)))):
            d = d.normalized()
            cam.location = Vector((0, 0, hi.z * .5)) + d * 10
            cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
            pick = [ks[0], ks[len(ks) // 4], ks[len(ks) // 2]] if len(ks) > 2 else ks[:1]
            for i, f in enumerate(pick):
                sc.frame_set(f)
                sc.render.filepath = os.path.join(PREVIEW, f"{key}__{clip['name']}__{view}{i}.png")
                bpy.ops.render.render(write_still=True)


def export_character(key, src):
    folder = os.path.join(OUT, key)
    os.makedirs(folder, exist_ok=True)
    arm, mesh = load_character(src)
    save_textures(key.lower(), os.path.join(folder, "Textures"), mesh)
    poser = Poser(arm)
    bpy.context.scene.render.fps = FPS
    acts = [make_clip(arm, poser, name, length, keys, loop) for name, length, keys, loop in clips()]
    arm.animation_data.action = None
    for pb in arm.pose.bones:
        pb.rotation_quaternion = Quaternion()
        pb.location = Vector()
    arm.name = key + "_rig"
    mesh.name = mesh.data.name = key.lower()
    for m in mesh.data.materials:
        if m:
            m.name = key.lower() + "_mat"
    lo, hi = bbox(mesh)
    hand = arm.data.bones["RightHand"]
    sc = bpy.context.scene
    sc.frame_start, sc.frame_end = 0, max(c["frames"] for _, c, _k in acts)
    export_fbx(os.path.join(folder, key.lower() + ".fbx"), [arm, mesh], True)
    meta = dict(name=key, source=src + ".fbx", height=round(hi.z - lo.z, 4), width=round(hi.x - lo.x, 4),
                rigged=src != "bibha" and src != "ednalva", hand=hand.name, clips=[c for _, c, _k in acts])
    with open(os.path.join(folder, key.lower() + ".clips.json"), "w") as fh:
        json.dump(meta, fh, indent=1)
    print("EXPORTED", key, "from", src, "height %.3f" % (hi.z - lo.z), "bones", len(arm.data.bones),
          "groups", len(mesh.vertex_groups), "clips", [c["name"] for _, c, _k in acts])
    if PREVIEW:
        render_preview(key, arm, mesh, acts)


def export_pistol(length=0.24):
    """The pistol, barrel along -Y (the game's forward), sized for these characters' hands, with its origin
    at the grip where the hand closes round it."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    _, mesh = import_fbx(GUN)
    bake_transforms(None, mesh)
    lo, hi = bbox(mesh)
    k = length / (hi.y - lo.y)
    # grip: the lower part at the back (+Y); the hand closes round it a little below the body of the gun
    grip = Vector(((lo.x + hi.x) / 2, hi.y - (hi.y - lo.y) * .22, lo.z + (hi.z - lo.z) * .45))
    mesh.data.transform(Matrix.Scale(k, 4) @ Matrix.Translation(-grip))
    mesh.data.update()
    lo, hi = bbox(mesh)
    folder = os.path.join(OUT, "Pistol")
    os.makedirs(folder, exist_ok=True)
    if not save_textures("pistol", os.path.join(folder, "Textures")):
        project_side_art(mesh, GUN_ART, os.path.join(folder, "Textures"), "pistol")
    mesh.name = mesh.data.name = "pistol"
    for m in mesh.data.materials:
        if m:
            m.name = "pistol_mat"
    export_fbx(os.path.join(folder, "pistol.fbx"), [mesh], False)
    # muzzle: the tip of the barrel, at the height of its middle (Blender coordinates; Unity: x -> -x, -y -> z)
    tip = Vector((0, lo.y, (lo.z + hi.z) / 2 + (hi.z - lo.z) * .2))
    with open(os.path.join(folder, "pistol.json"), "w") as fh:
        json.dump(dict(length=round(hi.y - lo.y, 4), muzzle=[round(-tip.x, 4), round(tip.z, 4), round(-tip.y, 4)]), fh, indent=1)
    print("EXPORTED pistol size", tuple(round(v, 3) for v in hi - lo), "muzzle", tuple(round(v, 3) for v in tip))


if not ONLY or "Pistol" in ONLY:
    export_pistol()
for key, src in CHARACTERS:
    if ONLY and key not in ONLY:
        continue
    export_character(key, src)
