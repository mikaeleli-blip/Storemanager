"""
Low-poly modelling helpers for Storemanager (run inside Blender / bpy).

Everything is authored in UNITY coordinates (x right, y up, z forward) and
converted to Blender's (x, z, y) here, so the FBX export (forward -Z, up Y,
baked space transform) lands in Unity with no rotation.

Colours come from one shared palette texture: each face is UV-mapped to the
centre of its colour's cell, so the whole store uses a single material
(plus an optional transparent "Glass" slot).
"""
import math
import random

import bmesh
import bpy
from mathutils import Matrix, Vector

# ---------------------------------------------------------------- palette

PALETTE = {}          # name -> (r, g, b) in 0..1 (sRGB)
EMISSIVE = set()      # palette names that glow
GRID = 16             # 16 x 16 cells
CELL_PX = 16


def hexc(h):
    h = h.lstrip('#')
    return tuple(int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4))


def add_colors(d, emissive=False):
    for k, v in d.items():
        PALETTE[k] = hexc(v)
        if emissive:
            EMISSIVE.add(k)


def palette_index(name):
    return list(PALETTE.keys()).index(name)


def cell_uv(name):
    i = palette_index(name)
    cx, cy = i % GRID, i // GRID
    return ((cx + 0.5) / GRID, 1.0 - (cy + 0.5) / GRID)


def write_palette(path, emission_path):
    from PIL import Image
    size = GRID * CELL_PX
    img = Image.new('RGB', (size, size), (255, 0, 255))
    emi = Image.new('RGB', (size, size), (0, 0, 0))
    for i, (name, rgb) in enumerate(PALETTE.items()):
        cx, cy = i % GRID, i // GRID
        col = tuple(int(round(c * 255)) for c in rgb)
        for y in range(CELL_PX):
            for x in range(CELL_PX):
                img.putpixel((cx * CELL_PX + x, cy * CELL_PX + y), col)
                if name in EMISSIVE:
                    emi.putpixel((cx * CELL_PX + x, cy * CELL_PX + y), col)
    img.save(path)
    emi.save(emission_path)


# ---------------------------------------------------------------- scene

def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    for m in list(bpy.data.materials):
        bpy.data.materials.remove(m)


_mats = {}


def srgb_to_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def mat(name):
    """Preview material for a palette colour (also used to tag faces)."""
    key = 'P_' + name
    m = bpy.data.materials.get(key)
    if m is None:
        if name == 'glass':
            m = bpy.data.materials.new(key)
            m.use_nodes = True
            b = m.node_tree.nodes['Principled BSDF']
            b.inputs['Base Color'].default_value = (0.75, 0.9, 1.0, 1)
            b.inputs['Alpha'].default_value = 0.25
            b.inputs['Roughness'].default_value = 0.05
            return m
        rgb = PALETTE[name]
        m = bpy.data.materials.new(key)
        m.use_nodes = True
        b = m.node_tree.nodes['Principled BSDF']
        lin = tuple(srgb_to_linear(c) for c in rgb)
        b.inputs['Base Color'].default_value = (*lin, 1)
        b.inputs['Roughness'].default_value = 0.7
        if name in EMISSIVE:
            b.inputs['Emission Color'].default_value = (*lin, 1)
            b.inputs['Emission Strength'].default_value = 2.0
    return m


def bl(v):
    """Unity (x, y, z) -> Blender (x, z, y)."""
    return Vector((v[0], v[2], v[1]))


_parts = []


def _new(obj, color):
    obj.data.materials.append(mat(color))
    _parts.append(obj)
    return obj


def _apply_transform(o):
    bpy.context.view_layer.objects.active = o
    o.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    o.select_set(False)


def box(size, center, color, bevel=0.0):
    """Axis-aligned box. size/center in Unity space."""
    bpy.ops.mesh.primitive_cube_add(size=1, location=bl(center))
    o = bpy.context.active_object
    o.scale = bl(size)
    _apply_transform(o)
    if bevel > 0:
        md = o.modifiers.new('bevel', 'BEVEL')
        md.width = bevel
        md.segments = 1
        md.limit_method = 'ANGLE'
    return _new(o, color)


def box_mm(mn, mx, color, bevel=0.0):
    """Box from min/max corners (Unity space)."""
    size = [mx[i] - mn[i] for i in range(3)]
    center = [(mx[i] + mn[i]) / 2 for i in range(3)]
    return box(size, center, color, bevel)


def cyl(radius, height, center, color, verts=8, axis='y', bevel=0.0, radius2=None):
    """Cylinder (or cone/frustum with radius2) along a Unity axis, centred at center."""
    if radius2 is None:
        bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=radius, depth=height, location=(0, 0, 0))
    else:
        bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=radius, radius2=radius2, depth=height, location=(0, 0, 0))
    o = bpy.context.active_object
    # Blender cylinder runs along Blender Z == Unity Y.
    if axis == 'x':
        o.rotation_euler = (0, math.radians(90), 0)
    elif axis == 'z':
        o.rotation_euler = (math.radians(90), 0, 0)
    _apply_transform(o)
    o.location = bl(center)
    bpy.ops.object.select_all(action='DESELECT')
    o.select_set(True)
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.transform_apply(location=True, rotation=False, scale=False)
    o.select_set(False)
    if bevel > 0:
        md = o.modifiers.new('bevel', 'BEVEL')
        md.width = bevel
        md.segments = 1
        md.limit_method = 'ANGLE'
    return _new(o, color)


def ball(radius, center, color, subdiv=1, scale=(1, 1, 1)):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdiv, radius=radius, location=(0, 0, 0))
    o = bpy.context.active_object
    o.scale = bl(scale)
    _apply_transform(o)
    o.location = bl(center)
    bpy.ops.object.select_all(action='DESELECT')
    o.select_set(True)
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.transform_apply(location=True, rotation=False, scale=False)
    o.select_set(False)
    return _new(o, color)


def poly(verts, faces, color):
    """Raw mesh from Unity-space vertices."""
    me = bpy.data.meshes.new('poly')
    me.from_pydata([bl(v) for v in verts], [], faces)
    me.update()
    # Swapping y/z mirrors the winding; flip so normals face out.
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.reverse_faces(bm, faces=bm.faces[:])
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new('poly', me)
    bpy.context.collection.objects.link(o)
    return _new(o, color)


def prism(points_xy, z0, z1, color):
    """Extrude a 2D outline (Unity x, y) along Unity z from z0 to z1 (convex outline, CCW seen from -z)."""
    n = len(points_xy)
    verts = [(x, y, z0) for x, y in points_xy] + [(x, y, z1) for x, y in points_xy]
    faces = [list(range(n))[::-1], list(range(n, 2 * n))]
    for i in range(n):
        j = (i + 1) % n
        faces.append([i, j, n + j, n + i])
    o = poly(verts, faces, color)
    me = o.data
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.to_mesh(me)
    bm.free()
    return o


def text_mesh(text, size, center, color, depth=0.04, facing='-z'):
    """Extruded text, readable from the -Z side (towards the camera/customers)."""
    bpy.ops.object.text_add(location=(0, 0, 0))
    t = bpy.context.active_object
    t.data.body = text
    t.data.size = size
    t.data.extrude = depth / 2
    t.data.align_x = 'CENTER'
    t.data.align_y = 'CENTER'
    bpy.ops.object.convert(target='MESH')
    o = bpy.context.active_object
    # Text lies in Blender XY (reads along +X, up +Y). Stand it up so it reads from Unity -Z (Blender -Y).
    o.rotation_euler = (math.radians(90), 0, 0)
    if facing == '+z':
        o.rotation_euler = (math.radians(90), 0, math.radians(180))
    _apply_transform(o)
    o.location = bl(center)
    bpy.ops.object.select_all(action='DESELECT')
    o.select_set(True)
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.transform_apply(location=True, rotation=False, scale=False)
    o.select_set(False)
    o.data.materials.clear()
    return _new(o, color)


def begin():
    _parts.clear()


def finish(name):
    """Join the parts made since begin() into one object named `name`."""
    parts = [p for p in _parts if p.name in bpy.data.objects]
    bpy.ops.object.select_all(action='DESELECT')
    for p in parts:
        p.select_set(True)
        bpy.context.view_layer.objects.active = p
        for md in list(p.modifiers):
            bpy.ops.object.modifier_apply(modifier=md.name)
        p.select_set(False)
    for p in parts:
        p.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    if len(parts) > 1:
        bpy.ops.object.join()
    o = bpy.context.active_object
    # Origin back at (0, 0, 0) so the mesh keeps the coordinates it was modelled in (base on y = 0).
    # Without this the origin sits at the first part's centre and the game drops the model by that much.
    o.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=False, scale=False)
    o.name = name
    o.data.name = name
    # flat shading
    for poly_ in o.data.polygons:
        poly_.use_smooth = False
    o.select_set(False)
    _parts.clear()
    return o


def jitter(r):
    return random.uniform(-r, r)


# ---------------------------------------------------------------- export

def bake_palette_uvs(o, palette_mat, glass_mat):
    """Map every face to its palette cell and collapse materials to [Palette, (Glass)]."""
    me = o.data
    names = [s.material.name if s.material else 'P_white' for s in o.material_slots]
    if not me.uv_layers:
        me.uv_layers.new(name='UV')
    if me.uv_layers.active is None:
        me.uv_layers.active = me.uv_layers[0]
    uv = me.uv_layers.active.data
    uses_glass = any(n == 'P_glass' for n in names)
    for p in me.polygons:
        n = names[p.material_index]
        if n == 'P_glass':
            u, v = cell_uv('white')
            idx = 1
        else:
            u, v = cell_uv(n[2:])
            idx = 0
        for li in p.loop_indices:
            uv[li].uv = (u, v)
        p.material_index = idx
    me.materials.clear()
    me.materials.append(palette_mat)
    if uses_glass:
        me.materials.append(glass_mat)


def export_fbx(objects, path, palette_mat, glass_mat):
    for o in objects:
        bake_palette_uvs(o, palette_mat, glass_mat)
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:
        o.select_set(True)
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={'MESH'},
        apply_scale_options='FBX_SCALE_ALL',
        axis_forward='-Z',
        axis_up='Y',
        bake_space_transform=True,
        mesh_smooth_type='FACE',
        use_mesh_modifiers=True,
        add_leaf_bones=False,
        bake_anim=False,
        path_mode='STRIP',
    )


# ---------------------------------------------------------------- preview rendering

def setup_render(res=(1280, 800), samples=24):
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.device = 'CPU'
    sc.cycles.samples = samples
    sc.cycles.use_denoising = True
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.film_transparent = False
    sc.view_settings.view_transform = 'AgX'
    sc.view_settings.look = 'AgX - Punchy'
    world = bpy.data.worlds.new('W')
    world.use_nodes = True
    bg = world.node_tree.nodes['Background']
    bg.inputs['Color'].default_value = (0.55, 0.62, 0.72, 1)
    bg.inputs['Strength'].default_value = 0.9
    sc.world = world
    sun = bpy.data.lights.new('sun', 'SUN')
    sun.energy = 3.5
    sun.angle = math.radians(8)
    so = bpy.data.objects.new('sun', sun)
    so.rotation_euler = (math.radians(50), math.radians(-25), math.radians(30))
    sc.collection.objects.link(so)


def camera_look(target_u, yaw_deg, pitch_deg, dist, fov=30, ortho=None):
    sc = bpy.context.scene
    cam = bpy.data.cameras.new('cam')
    cam.lens_unit = 'FOV'
    cam.angle = math.radians(fov)
    if ortho:
        cam.type = 'ORTHO'
        cam.ortho_scale = ortho
    co = bpy.data.objects.new('cam', cam)
    sc.collection.objects.link(co)
    sc.camera = co
    t = bl(target_u)
    yaw, pitch = math.radians(yaw_deg), math.radians(pitch_deg)
    # Unity-style: camera behind target along -forward, forward = (sin yaw, 0, cos yaw) in Unity xz
    fwd_u = Vector((math.sin(yaw) * math.cos(pitch), -math.sin(pitch), math.cos(yaw) * math.cos(pitch)))
    pos = t - bl(fwd_u) * dist
    co.location = pos
    direction = t - pos
    co.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()
    return co


def render(path):
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def instance(obj, loc_u, yaw_deg=0.0, tilt_deg=0.0):
    """Place a linked copy of an asset for previews: like Unity's Euler(0, yaw, tilt) at loc_u."""
    c = obj.copy()
    bpy.context.collection.objects.link(c)
    c.location = bl(loc_u)
    # Unity Y == Blender Z and Unity Z == Blender Y, with the handedness flip negating angles.
    c.rotation_mode = 'XYZ'
    c.rotation_euler = (0, -math.radians(tilt_deg), -math.radians(yaw_deg))
    return c
