# Models the monsters' projectiles as balls of smoke, in the same style as the monsters: lumpy spheres drawn
# out along the way they fly, a crescent blade for the boomerang, a cracked smoke bomb. The game's Roguelike/GhostBolt shader makes them
# breathe, wobble and fade; the vertex colours tell it where: R = glow (eyes, cracks), G = 0 at the head to 1
# at the tip of the tail.
# usage: blender -b -P make_projectiles.py -- <out_dir> [preview_dir]
# Outputs: <out_dir>/<shape>.fbx for the shapes below, sized in metres, facing -Y (Unity +Z).
import bpy, bmesh, math, os, sys
from mathutils import Vector, noise

args = sys.argv[sys.argv.index('--') + 1:]
OUT = args[0]
PREVIEW = args[1] if len(args) > 1 else None

def lumpy(v, amount, scale, seed):
    return v.normalized() * (1 + amount * noise.noise(v * scale + Vector((seed, seed * 1.7, seed * .3))))

def sphere(bm, segments, rings, radius):
    bmesh.ops.create_uvsphere(bm, u_segments=segments, v_segments=rings, radius=radius)

def blade(bm, radius=.22, thick=.045, sweep=125, seed=2.0):
    """A crescent moon blade for the boomerang: a tube bent round an arc, tapering to sharp horns."""
    segs, ring = 40, 10
    rows = []
    for i in range(segs + 1):
        a = math.radians(-sweep + 2 * sweep * i / segs)
        k = 1 - abs(i / segs * 2 - 1)
        w = thick * (k ** .6) + .002
        c = Vector((math.sin(a) * radius, -math.cos(a) * radius, 0))
        out = Vector((math.sin(a), -math.cos(a), 0))
        row = []
        for j in range(ring):
            b = 2 * math.pi * j / ring
            # wider in the plane of the arc than across it, a keen inner edge
            p = c + out * (math.cos(b) * w * (1.6 if math.cos(b) < 0 else 1.0)) + Vector((0, 0, math.sin(b) * w * .55))
            row.append(bm.verts.new(p))
        rows.append(row)
    for i in range(segs):
        for j in range(ring):
            bm.faces.new((rows[i][j], rows[i][(j + 1) % ring], rows[i + 1][(j + 1) % ring], rows[i + 1][j]))
    bmesh.ops.contextual_create(bm, geom=rows[0]); bmesh.ops.contextual_create(bm, geom=rows[-1])
    glow, tail = {}, {}
    for v in bm.verts:
        d = math.hypot(v.co.x, v.co.y)
        glow[v] = max(0.0, min(1.0, (radius - d) / (thick * .9))) * .8      # the inner, cutting edge glows
        tail[v] = abs(math.atan2(v.co.x, -v.co.y)) / math.radians(sweep)     # fades towards the horns
    return glow, tail

def bomb(bm, radius=.2, seed=3.0):
    """A lumpy ball of packed smoke, glowing through cracks, with a little twisted wisp on top."""
    sphere(bm, 32, 24, 1.0)
    glow, tail = {}, {}
    for v in bm.verts:
        p = v.co.copy()
        q = lumpy(p, .14, 2.5, seed) * radius
        top = max(0.0, p.z - .85) / .15
        q.z += top * radius * .7
        q.x += math.sin(top * 3) * radius * .2 * top
        v.co = q
        crack = abs(noise.noise(p * 3.2 + Vector((seed, 0, 0))))
        glow[v] = max(0.0, 1 - crack / .08) if top == 0 else 0.0
        tail[v] = top
    return glow, tail

def orb(bm, radius=.1, stretch=1.8, lump=.04, seed=5.0):
    """A ball of smoke drawn out along the way it flies (-Y): an ellipsoid, a little lumpy, the back half
    fading (G) so it reads as moving."""
    sphere(bm, 32, 22, 1.0)
    glow, tail = {}, {}
    for v in bm.verts:
        p = v.co.copy()
        q = lumpy(p, lump, 2.4, seed)
        v.co = Vector((q.x * radius, q.y * radius * stretch, q.z * radius))
        tail[v] = max(0.0, p.y) * .8
        glow[v] = max(0.0, -p.y - .75) * 2.0          # a hotter tip in front
    return glow, tail

SHAPES = {
    "Orb": lambda bm: orb(bm, radius=.09, stretch=2.8),
    "Blade": lambda bm: blade(bm),
    "Bomb": lambda bm: bomb(bm),
    "Glob": lambda bm: orb(bm, radius=.11, stretch=2.2, lump=.16, seed=4.0),     # lumpier, for spit
}

def build(name, fn):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    me = bpy.data.meshes.new(name)
    bm = bmesh.new()
    glow, tail = fn(bm)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    for f in bm.faces:
        f.smooth = True
    col = bm.loops.layers.color.new("Col")
    for f in bm.faces:
        for l in f.loops:
            l[col] = (glow.get(l.vert, 0.0), tail.get(l.vert, 0.0), 0.0, 1.0)
    bm.to_mesh(me)
    bm.free()
    me.color_attributes.active_color = me.color_attributes["Col"]
    me.color_attributes.render_color_index = 0
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    os.makedirs(OUT, exist_ok=True)
    for o in bpy.data.objects:
        o.select_set(o == ob)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(OUT, name + ".fbx"), use_selection=True, object_types={'MESH'},
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
        mesh_smooth_type='FACE', colors_type='LINEAR', bake_anim=False, path_mode='STRIP')
    lo = Vector(map(min, *[v.co for v in me.vertices])); hi = Vector(map(max, *[v.co for v in me.vertices]))
    print("EXPORTED", name, len(me.vertices), "verts, size", tuple(round(x, 3) for x in hi - lo),
          "glow max %.2f" % max(glow.values()), "glowing verts", sum(1 for g in glow.values() if g > .5))
    if PREVIEW:
        sc = bpy.context.scene
        sc.render.engine = 'BLENDER_WORKBENCH'
        sc.display.shading.light = 'STUDIO'
        sc.display.shading.color_type = 'VERTEX'
        sc.render.resolution_x = sc.render.resolution_y = 300
        cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam'))
        sc.collection.objects.link(cam); sc.camera = cam
        cam.data.type = 'ORTHO'; cam.data.ortho_scale = max(hi - lo) * 1.4
        d = Vector((-.7, -1, .5)).normalized()
        cam.location = (lo + hi) / 2 + d * 5
        cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
        os.makedirs(PREVIEW, exist_ok=True)
        sc.render.filepath = os.path.join(PREVIEW, name + ".png")
        bpy.ops.render.render(write_still=True)

for name, fn in SHAPES.items():
    build(name, fn)
