"""
Products: one shelf level (a "stock row") of each product, plus one single unit of each ("Item_*").

Stock row frame (Unity space): products face +X, the row runs along Z from -0.47 to 0.47, depth from
x = 0.06 (back) to 0.42 (front), sitting on y = 0. The game places a row on each shelf level and turns it
180 degrees for the other side of the fixture. Deli rows ("Deli_*") use the counter's frame instead:
along X, facing -Z.

Every unit is modelled once in two versions: a detailed one for the front row (what you see) and a light
one for the rows behind it, then copied into place. That keeps a full store cheap to draw.
Items are the detailed unit on its own, centred on the origin, for baskets, carts and the checkout belt.
"""
import math
import random

import bmesh
import bpy

import kit
from kit import ball, box, cyl, jitter, prism

Z0, Z1 = -0.47, 0.47
X0, X1 = 0.06, 0.42


def along(width, gap=0.006):
    """Centre z positions for units of `width` filling the row."""
    n = max(1, int((Z1 - Z0 + gap) // (width + gap)))
    span = n * width + (n - 1) * gap
    start = -span / 2 + width / 2
    return [start + i * (width + gap) for i in range(n)]


def deep(depth, gap=0.01, cols=None):
    """Centre x positions, front first."""
    n = cols or max(1, int((X1 - X0 + gap) // (depth + gap)))
    return [X1 - depth / 2 - i * (depth + gap) for i in range(n)]


# ------------------------------------------------------------------ helpers

def tube(points, radii, color, sides=5):
    """A tapered tube through Unity-space points (bananas, croissants, chicken legs)."""
    n = len(points)
    verts, faces = [], []
    for i in range(n):
        a = points[max(0, i - 1)]
        b = points[min(n - 1, i + 1)]
        t = [b[k] - a[k] for k in range(3)]
        tl = math.sqrt(sum(c * c for c in t)) or 1.0
        t = [c / tl for c in t]
        up = (0.0, 1.0, 0.0) if abs(t[1]) < 0.9 else (1.0, 0.0, 0.0)
        u = [up[1] * t[2] - up[2] * t[1], up[2] * t[0] - up[0] * t[2], up[0] * t[1] - up[1] * t[0]]
        ul = math.sqrt(sum(c * c for c in u)) or 1.0
        u = [c / ul for c in u]
        v = [t[1] * u[2] - t[2] * u[1], t[2] * u[0] - t[0] * u[2], t[0] * u[1] - t[1] * u[0]]
        for s in range(sides):
            ang = 2 * math.pi * s / sides
            ca, sa = math.cos(ang) * radii[i], math.sin(ang) * radii[i]
            verts.append(tuple(points[i][k] + u[k] * ca + v[k] * sa for k in range(3)))
    for i in range(n - 1):
        for s in range(sides):
            j = (s + 1) % sides
            faces.append([i * sides + s, i * sides + j, (i + 1) * sides + j, (i + 1) * sides + s])
    faces.append(list(range(sides))[::-1])
    faces.append([(n - 1) * sides + s for s in range(sides)])
    o = kit.poly(verts, faces, color)
    bm = bmesh.new()
    bm.from_mesh(o.data)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.to_mesh(o.data)
    bm.free()
    return o


def slab_x(points_yz, x0, x1, color):
    """A flat convex shape in the Y-Z plane, extruded along X from x0 to x1 (cut faces, labels)."""
    n = len(points_yz)
    verts = [(x0, y, z) for y, z in points_yz] + [(x1, y, z) for y, z in points_yz]
    faces = [list(range(n)), list(range(n, 2 * n))[::-1]]
    for i in range(n):
        j = (i + 1) % n
        faces.append([i, n + i, n + j, j])
    o = kit.poly(verts, faces, color)
    bm = bmesh.new()
    bm.from_mesh(o.data)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.to_mesh(o.data)
    bm.free()
    return o


def make(fn, *args):
    """Model one unit at the origin and return it as an object."""
    kit.begin()
    fn(*args)
    return kit.finish('tmp_unit')


def place(src, loc, yaw=0.0, scale=None):
    o = src.copy()
    o.data = src.data.copy()
    bpy.context.collection.objects.link(o)
    o.location = kit.bl(loc)
    o.rotation_euler = (0.0, 0.0, math.radians(-yaw))
    if scale:
        o.scale = kit.bl(scale)
    return o


def join(objs, name):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    if len(objs) > 1:
        bpy.ops.object.join()
    o = bpy.context.active_object
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    o.name = name
    o.data.name = name
    for f in o.data.polygons:
        f.use_smooth = False
    o.select_set(False)
    return o


def remove(objs):
    for o in objs:
        if o is not None and o.name in bpy.data.objects:
            bpy.data.objects.remove(o, do_unlink=True)


ITEMS = []   # Item_* objects made while building rows


def keep_item(src, product):
    """Turn one detailed unit into Item_<product> (centred, base on y = 0)."""
    o = place(src, (0, 0, 0))
    o = join([o], 'Item_' + product.replace(' ', '_'))
    ITEMS.append(o)


def grid_row(name, product, unit, d, w, stack=1, step=0.0, cols=None, variants=1, group=1,
             gap_w=0.006, gap_d=0.01, jit=0.0, yaw_jit=0.0, top_unit=None):
    """
    Fills a shelf row with copies of unit(detail, variant). The front row and the top of each stack get
    the detailed unit (top_unit if given), the rest the light one. variant changes every `group` units.
    """
    det = [make(unit, True, v) for v in range(variants)]
    lite = [make(unit, False, v) for v in range(variants)]
    tops = [make(top_unit, v) for v in range(variants)] if top_unit else None
    xs, zs = deep(d, gap_d, cols), along(w, gap_w)
    objs = []
    for ix, x in enumerate(xs):
        for iz, z in enumerate(zs):
            v = (iz // group) % variants
            for k in range(stack):
                top = k == stack - 1
                src = det[v] if ix == 0 else (tops[v] if tops and top else lite[v])
                objs.append(place(src, (x + jitter(jit), k * step, z + jitter(jit)), jitter(yaw_jit)))
    row = join(objs, name)
    keep_item(det[0], product)
    remove(det + lite + (tops or []))
    return row


def scatter_row(name, product, units, count, r, lift=1.6, seed=1):
    """A loose pile in a produce crate: copies of a few unit variants at random spots and heights."""
    random.seed(seed)
    srcs = [make(u, i) for i, u in enumerate(units)]
    objs = []
    for i in range(count):
        x = random.uniform(X0 + r, X1 - r)
        z = random.uniform(Z0 + r, Z1 - r)
        y = random.random() * r * lift
        objs.append(place(srcs[i % len(srcs)], (x, y, z), random.uniform(0, 360)))
    row = join(objs, name)
    keep_item(srcs[0], product)
    remove(srcs)
    return row


# ------------------------------------------------------------------ grocery

def pasta_unit(detail, v):
    h = 0.2
    box((0.12, h, 0.07), (0, h / 2, 0), 'navy', bevel=0.004 if detail else 0)
    box((0.122, 0.03, 0.072), (0, h - 0.025, 0), 'white')
    if detail:
        box((0.004, 0.08, 0.048), (0.061, 0.1, 0), 'pasta')
        box((0.122, 0.012, 0.072), (0, 0.025, 0), 'yellow')
        cyl(0.011, 0.004, (0.062, 0.16, 0), 'red', verts=8, axis='x')


def pasta():
    return grid_row('Stock_Pasta', 'Pasta', pasta_unit, 0.12, 0.075)


def rice_unit(detail, v):
    box((0.14, 0.15, 0.11), (0, 0.075, 0), 'white', bevel=0.022 if detail else 0)
    box((0.142, 0.04, 0.112), (0, 0.07, 0), 'red')
    if detail:
        box((0.11, 0.025, 0.1), (0, 0.16, 0), 'white')
        box((0.004, 0.025, 0.05), (0.072, 0.115, 0), 'gold')
        for dz in (-0.02, 0.0, 0.02):
            box((0.004, 0.006, 0.006), (0.072, 0.03, dz), 'offwhite')


def rice():
    return grid_row('Stock_Rice', 'Rice', rice_unit, 0.15, 0.115)


def soup_can(y, detail):
    if detail:
        cyl(0.04, 0.094, (0, y + 0.05, 0), 'red', verts=12)
        cyl(0.0405, 0.03, (0, y + 0.048, 0), 'white', verts=12)
        cyl(0.041, 0.007, (0, y + 0.0035, 0), 'steel', verts=12)
        cyl(0.041, 0.007, (0, y + 0.0965, 0), 'steel', verts=12)
        cyl(0.034, 0.002, (0, y + 0.1005, 0), 'steel_dark', verts=12)
        box((0.004, 0.014, 0.014), (0.041, y + 0.048, 0), 'tomato')
    else:
        cyl(0.04, 0.1, (0, y + 0.05, 0), 'red', verts=8)
        cyl(0.041, 0.006, (0, y + 0.097, 0), 'steel', verts=8)


def soup_unit(detail, v):
    soup_can(0.0, detail)
    soup_can(0.102, detail)


def soup():
    return grid_row('Stock_Canned_Soup', 'Canned Soup', soup_unit, 0.085, 0.085)


CEREAL = ['orange', 'yellow', 'red', 'purple']


def cereal_unit(detail, v):
    c = CEREAL[v]
    box((0.07, 0.29, 0.19), (0, 0.145, 0), c)
    box((0.072, 0.045, 0.192), (0, 0.255, 0), 'white')
    if detail:
        cyl(0.06, 0.004, (0.036, 0.11, 0), 'white', verts=12, axis='x')
        cyl(0.05, 0.005, (0.037, 0.12, 0), 'gold', verts=10, axis='x')
        for dz, dy in ((-0.02, 0.13), (0.015, 0.14), (0.0, 0.115)):
            box((0.006, 0.014, 0.018), (0.04, dy, dz), 'cream')
        box((0.073, 0.02, 0.07), (0, 0.256, 0.05), 'brand')


def cereal():
    return grid_row('Stock_Cereal', 'Cereal', cereal_unit, 0.07, 0.19, cols=4, variants=4, group=2)


def coffee_unit(detail, v):
    box((0.085, 0.16, 0.085), (0, 0.08, 0), 'brown', bevel=0.008 if detail else 0)
    box((0.087, 0.035, 0.087), (0, 0.105, 0), 'gold')
    if detail:
        box((0.06, 0.018, 0.088), (0, 0.17, 0), 'brown')
        cyl(0.011, 0.003, (0.044, 0.14, 0), 'black', verts=8, axis='x')
        box((0.003, 0.012, 0.04), (0.0445, 0.06, 0), 'cream')


def coffee():
    return grid_row('Stock_Coffee', 'Coffee', coffee_unit, 0.09, 0.09)


SNACK = [('orange', 'chip'), ('red', 'chip'), ('yellow', 'orange'), ('green', 'chip')]


def snack_unit(detail, v):
    c, chip = SNACK[v]
    box((0.06, 0.21, 0.14), (0, 0.115, 0), c, bevel=0.022 if detail else 0)
    box((0.03, 0.022, 0.14), (0, 0.232, 0), 'lightgrey')
    if detail:
        box((0.03, 0.02, 0.14), (0, 0.011, 0), 'lightgrey')
        cyl(0.042, 0.004, (0.031, 0.105, 0), 'white', verts=10, axis='x')
        cyl(0.034, 0.005, (0.032, 0.105, 0), chip, verts=8, axis='x')
        box((0.004, 0.03, 0.11), (0.031, 0.175, 0), 'white')


def snacks():
    return grid_row('Stock_Snacks', 'Snacks', snack_unit, 0.07, 0.145, variants=4, group=2, jit=0.006, yaw_jit=6)


def bottle(label, cap, detail, band=None, wide=1.0):
    r = 0.036 * wide
    vs = 10 if detail else 6
    cyl(r, 0.15, (0, 0.075, 0), 'cola', verts=vs)
    cyl(r, 0.055, (0, 0.1775, 0), 'cola', verts=vs, radius2=r * 0.4)
    cyl(r * 0.4, 0.025, (0, 0.2175, 0), 'cola', verts=6)
    cyl(r * 0.45, 0.018, (0, 0.239, 0), cap, verts=8 if detail else 6)
    cyl(r * 1.03, 0.06, (0, 0.1, 0), label, verts=vs)
    if detail and band:
        cyl(r * 1.05, 0.009, (0, 0.104, 0), band, verts=vs)


def bubbla_unit(detail, v):
    bottle('bubbla', 'bubbla', detail, band='white')


def bubbla():
    return grid_row('Stock_Bubbla_Cola', 'Bubbla Cola', bubbla_unit, 0.075, 0.075, gap_w=0.004)


def zing_can(x, z, detail):
    if detail:
        cyl(0.033, 0.11, (x, 0.067, z), 'zing', verts=10)
        cyl(0.031, 0.008, (x, 0.008, z), 'steel', verts=10, radius2=0.033)
        cyl(0.03, 0.007, (x, 0.1255, z), 'steel', verts=10)
        cyl(0.0335, 0.012, (x, 0.07, z), 'white', verts=10)
        cyl(0.0336, 0.006, (x, 0.08, z), 'red', verts=10)
    else:
        cyl(0.033, 0.122, (x, 0.067, z), 'zing', verts=6)
        cyl(0.03, 0.006, (x, 0.128, z), 'steel', verts=6)


def zing_unit(detail, v):
    box((0.14, 0.01, 0.21), (0, 0.005, 0), 'zing_dark')
    for x in (-0.034, 0.034):
        for z in (-0.068, 0.0, 0.068):
            zing_can(x, z, detail)


def zing():
    return grid_row('Stock_Zing_Cola', 'Zing Cola', zing_unit, 0.14, 0.21, stack=2, step=0.135, gap_w=0.01)


def sliced_bread_unit(detail, v):
    """A loaf in a clear bag, lying front to back: the domed top shows through, a printed band round the middle."""
    box((0.23, 0.075, 0.1), (0, 0.0375, 0), 'bread', bevel=0.015 if detail else 0)
    cyl(0.05, 0.23, (0, 0.075, 0), 'bread', verts=8 if detail else 6, axis='x')
    box((0.09, 0.08, 0.104), (-0.03, 0.04, 0), 'white')
    cyl(0.052, 0.09, (-0.03, 0.077, 0), 'white', verts=8 if detail else 6, axis='x')
    cyl(0.053, 0.03, (-0.03, 0.077, 0), 'kraft', verts=8 if detail else 6, axis='x')
    if detail:
        # the cut end: crumb inside a crust edge (one arch-shaped slice)
        arch = [(0.008, -0.042), (0.077, -0.042)]
        for i in range(1, 6):
            a = math.pi * i / 6
            arch.append((0.077 + math.sin(a) * 0.043, -math.cos(a) * 0.043))
        arch.append((0.077, 0.042))
        arch.append((0.008, 0.042))
        slab_x(arch, 0.115, 0.119, 'crumb')
        # twist tie at the back and a little logo
        box((0.025, 0.03, 0.03), (-0.128, 0.07, 0), 'white')
        box((0.006, 0.036, 0.036), (-0.118, 0.07, 0), 'red')
        cyl(0.012, 0.003, (-0.03, 0.1, 0.0), 'red', verts=8)


def sliced_bread():
    return grid_row('Stock_Sliced_Bread', 'Sliced Bread', sliced_bread_unit, 0.26, 0.11, cols=1, stack=2, step=0.115)


# ------------------------------------------------------------------ household

def detergent_unit(detail, v):
    box((0.12, 0.24, 0.15), (0, 0.12, 0), 'orange', bevel=0.02 if detail else 0)
    cyl(0.025, 0.035, (0, 0.255, 0.03), 'navy', verts=8)
    box((0.122, 0.1, 0.12), (0, 0.12, 0), 'white')
    if detail:
        box((0.1, 0.03, 0.12), (0, 0.245, 0.0), 'orange', bevel=0.01)
        box((0.03, 0.06, 0.03), (0, 0.25, -0.055), 'orange_dark')
        cyl(0.032, 0.003, (0.0625, 0.13, 0), 'sky', verts=10, axis='x')
        cyl(0.018, 0.004, (0.063, 0.13, 0), 'white', verts=8, axis='x')


def detergent():
    return grid_row('Stock_Detergent', 'Detergent', detergent_unit, 0.13, 0.16)


def tp_unit(detail, v):
    for ix in (-0.05, 0.05):
        for iz in (-0.1, 0.0, 0.1):
            for iy in (0, 1):
                cyl(0.048, 0.11, (ix, 0.055 + iy * 0.115, iz), 'white', verts=8 if detail else 6)
            if detail:
                cyl(0.015, 0.002, (ix, 0.226, iz), 'cardboard', verts=6)
    box((0.205, 0.04, 0.305), (0, 0.13, 0), 'sky')
    if detail:
        box((0.004, 0.025, 0.1), (0.103, 0.13, 0), 'white')


def toilet_paper():
    return grid_row('Stock_Toilet_Paper', 'Toilet Paper', tp_unit, 0.2, 0.3, cols=1)


def dish_soap_unit(detail, v):
    box((0.05, 0.17, 0.07), (0, 0.085, 0), 'lime', bevel=0.012 if detail else 0)
    cyl(0.014, 0.03, (0, 0.185, 0), 'white', verts=6)
    if detail:
        box((0.04, 0.02, 0.055), (0, 0.168, 0), 'lime')
        box((0.003, 0.07, 0.05), (0.026, 0.08, 0), 'white')
        ball(0.012, (0.028, 0.09, 0), 'yellow', subdiv=1, scale=(0.4, 1, 1))
        box((0.006, 0.006, 0.02), (0, 0.203, 0.01), 'white')


def dish_soap():
    return grid_row('Stock_Dish_Soap', 'Dish Soap', dish_soap_unit, 0.06, 0.075)


def battery_unit(detail, v):
    box((0.012, 0.13, 0.085), (0, 0.065, 0), 'black')
    box((0.014, 0.03, 0.087), (0, 0.11, 0), 'gold')
    if detail:
        box((0.012, 0.02, 0.03), (0, 0.14, 0), 'black')
        for z in (-0.03, -0.01, 0.01, 0.03):
            cyl(0.007, 0.045, (0.012, 0.055, z), 'gold', verts=6)
            cyl(0.0071, 0.008, (0.012, 0.033, z), 'black', verts=6)


def batteries():
    return grid_row('Stock_Batteries', 'Batteries', battery_unit, 0.05, 0.09)


# ------------------------------------------------------------------ dairy & frozen

def milk_unit(detail, v):
    box((0.075, 0.18, 0.075), (0, 0.09, 0), 'white')
    prism([(-0.0375, 0.18), (0.0375, 0.18), (0, 0.212)], -0.0375, 0.0375, 'white')
    box((0.077, 0.05, 0.077), (0, 0.04, 0), 'milk')
    if detail:
        box((0.004, 0.01, 0.075), (0, 0.216, 0), 'white')
        box((0.002, 0.026, 0.02), (0.0385, 0.12, 0.016), 'black')
        box((0.002, 0.016, 0.014), (0.0385, 0.145, -0.02), 'black')
        cyl(0.009, 0.01, (0.018, 0.2, 0.0), 'milk', verts=6)
        box((0.002, 0.012, 0.05), (0.0385, 0.04, 0), 'white')


def milk():
    return grid_row('Stock_Milk', 'Milk', milk_unit, 0.078, 0.078, gap_w=0.004)


def egg_carton(y, detail, top):
    box((0.11, 0.04, 0.25), (0, y + 0.02, 0), 'egg')
    box((0.11, 0.022, 0.25), (0, y + 0.052, 0), 'egg', bevel=0.006 if detail else 0)
    box((0.112, 0.014, 0.12), (0, y + 0.03, 0), 'kraft')
    if top:
        for x in (-0.025, 0.025):
            for z in (-0.09, -0.03, 0.03, 0.09):
                ball(0.02, (x, y + 0.062, z), 'egg', subdiv=1, scale=(1, 0.45, 1))


def eggs_unit(detail, v):
    for k in range(3):
        egg_carton(k * 0.066, detail, detail and k == 2)


def eggs_top(v):
    for k in range(3):
        egg_carton(k * 0.066, False, k == 2)


def eggs():
    return grid_row('Stock_Eggs', 'Eggs', eggs_unit, 0.115, 0.255, top_unit=eggs_top)


def cheese_unit(detail, v):
    box((0.09, 0.07, 0.11), (0, 0.035, 0), 'cheese', bevel=0.008 if detail else 0)
    box((0.092, 0.02, 0.112), (0, 0.05, 0), 'red')
    box((0.08, 0.055, 0.1), (0, 0.098, 0), 'cheese', bevel=0.008 if detail else 0)
    if detail:
        box((0.082, 0.016, 0.102), (0, 0.11, 0), 'red')
        for dz, dy in ((-0.025, 0.02), (0.02, 0.025), (0.0, 0.085)):
            cyl(0.007, 0.003, (0.046 if dy < 0.05 else 0.041, dy, dz), 'cheese_dark', verts=6, axis='x')


def cheese():
    return grid_row('Stock_Cheese', 'Cheese', cheese_unit, 0.095, 0.115)


YOGURT = ['pink', 'purple', 'lime', 'yellow']


def yogurt_cup(y, flavour, detail):
    vs = 10 if detail else 6
    cyl(0.025, 0.075, (0, y + 0.0375, 0), 'white', verts=vs, radius2=0.031)
    cyl(0.032, 0.004, (0, y + 0.077, 0), flavour, verts=vs)
    if detail:
        cyl(0.0285, 0.022, (0, y + 0.035, 0), flavour, verts=vs, radius2=0.0298)


def yogurt_unit(detail, v):
    yogurt_cup(0.0, YOGURT[v], detail)
    yogurt_cup(0.08, YOGURT[v], detail)


def yogurt():
    return grid_row('Stock_Yogurt', 'Yogurt', yogurt_unit, 0.066, 0.066, variants=4, group=2, gap_w=0.004)


def pizza_unit(detail, v):
    for k in range(5):
        box((0.27, 0.034, 0.27), (0, 0.017 + k * 0.036, 0), 'navy' if k % 2 else 'zing_dark')
        box((0.272, 0.01, 0.272), (0, 0.02 + k * 0.036, 0), 'red')
    top = 0.178
    cyl(0.1, 0.003, (0, top + 0.0015, 0), 'pizza', verts=12)
    cyl(0.085, 0.004, (0, top + 0.002, 0), 'cheese', verts=12)
    for i in range(6):
        a = i * math.pi / 3 + 0.3
        cyl(0.014, 0.005, (math.cos(a) * 0.05, top + 0.004, math.sin(a) * 0.05), 'maroon', verts=8)
    if detail:
        for i in range(3):
            a = i * 2.1
            box((0.012, 0.004, 0.008), (math.cos(a) * 0.025, top + 0.005, math.sin(a) * 0.025), 'leaf')


def frozen_pizza():
    return grid_row('Stock_Frozen_Pizza', 'Frozen Pizza', pizza_unit, 0.28, 0.28, cols=1)


ICE = ['pink', 'chocolate', 'cream', 'mint']


def tub(y, flavour, detail):
    vs = 12 if detail else 8
    cyl(0.052, 0.085, (0, y + 0.0425, 0), 'white', verts=vs, radius2=0.056)
    cyl(0.058, 0.01, (0, y + 0.09, 0), flavour, verts=vs)
    if detail:
        cyl(0.0545, 0.025, (0, y + 0.045, 0), flavour, verts=vs)
        ball(0.016, (0, y + 0.097, 0), flavour, subdiv=1, scale=(1, 0.5, 1))


def ice_unit(detail, v):
    tub(0.0, ICE[v], detail)
    tub(0.097, ICE[v], detail)


def ice_cream():
    return grid_row('Stock_Ice_Cream', 'Ice Cream', ice_unit, 0.12, 0.12, variants=4)


# ------------------------------------------------------------------ produce (piles in crates)

def apple(v):
    ball(0.04, (0, 0.036, 0), 'apple' if v % 3 else 'apple_green', subdiv=1, scale=(1, 0.9, 1))
    box((0.004, 0.016, 0.004), (0.003, 0.074, 0), 'banana_tip')
    if v == 1:
        box((0.018, 0.003, 0.008), (0.012, 0.074, 0), 'leaf')


def apples():
    return scatter_row('Stock_Apples', 'Apples', [apple, apple, apple], 70, 0.04)


def tomato(v):
    ball(0.035, (0, 0.03, 0), 'tomato', subdiv=1, scale=(1, 0.85, 1))
    box((0.026, 0.004, 0.006), (0, 0.06, 0), 'leaf')
    box((0.006, 0.004, 0.026), (0, 0.06, 0), 'leaf')
    box((0.004, 0.008, 0.004), (0, 0.064, 0), 'leaf')


def tomatoes():
    return scatter_row('Stock_Tomatoes', 'Tomatoes', [tomato, tomato], 80, 0.035)


def potato(v):
    random.seed(40 + v)
    sx, sz = random.uniform(1.15, 1.45), random.uniform(0.85, 1.05)
    ball(0.042, (0, 0.03, 0), 'potato' if v % 2 == 0 else 'potato_dark', subdiv=1, scale=(sx, 0.72, sz))
    box((0.006, 0.003, 0.006), (0.02, 0.06, 0.01), 'potato_dark')


def potatoes():
    return scatter_row('Stock_Potatoes', 'Potatoes', [potato, potato, potato, potato], 60, 0.045)


def lettuce_head(v):
    ball(0.06, (0, 0.055, 0), 'lettuce', subdiv=1, scale=(1, 0.85, 1))
    ball(0.035, (0, 0.1, 0), 'lettuce_light', subdiv=1, scale=(1, 0.6, 1))
    for i in range(4):
        a = i * math.pi / 2 + 0.4 + v
        ball(0.045, (math.cos(a) * 0.05, 0.045, math.sin(a) * 0.05), 'leaf', subdiv=1, scale=(1.1, 0.5, 0.8))


def lettuce():
    begin_rows = []
    src = [make(lettuce_head, 0), make(lettuce_head, 1)]
    random.seed(7)
    objs = []
    for x in (0.15, 0.33):
        for i, z in enumerate(along(0.15)):
            objs.append(place(src[i % 2], (x + jitter(0.01), 0, z + jitter(0.01)), random.uniform(0, 90)))
    row = join(objs, 'Stock_Lettuce')
    keep_item(src[0], 'Lettuce')
    remove(src)
    return row


def banana_bunch(v):
    for i in range(5):
        dz = (i - 2) * 0.022
        lift = abs(i - 2) * 0.006
        pts = [(-0.07, 0.05 + lift, dz), (-0.035, 0.03 + lift, dz), (0.0, 0.025 + lift, dz * 1.1),
               (0.035, 0.03 + lift, dz * 1.2), (0.07, 0.048 + lift, dz * 1.3)]
        tube(pts, [0.008, 0.012, 0.013, 0.011, 0.006], 'banana', sides=5)
        box((0.008, 0.008, 0.008), (0.074, 0.05 + lift, dz * 1.3), 'banana_tip')
    box((0.02, 0.02, 0.05), (-0.08, 0.055, 0), 'banana_tip')


def bananas():
    src = make(banana_bunch, 0)
    objs = []
    random.seed(3)
    for x in (0.15, 0.32):
        for z in along(0.14):
            objs.append(place(src, (x + jitter(0.01), 0, z), jitter(12)))
    row = join(objs, 'Stock_Bananas')
    keep_item(src, 'Bananas')
    remove([src])
    return row


# ------------------------------------------------------------------ bakery

def loaf_unit(detail, v):
    box((0.11, 0.085, 0.19), (0, 0.0425, 0), 'bread', bevel=0.03 if detail else 0)
    box((0.06, 0.006, 0.16), (0, 0.087, 0), 'flour')
    for dz in (-0.05, 0.0, 0.05):
        box((0.07, 0.008, 0.012), (0, 0.088, dz), 'bread_dark')


def fresh_bread():
    return grid_row('Stock_Fresh_Bread', 'Fresh Bread', loaf_unit, 0.12, 0.2)


def croissant_unit(detail, v):
    pts, rad = [], []
    n = 7 if detail else 5
    for i in range(n):
        t = i / (n - 1)
        a = math.pi * (0.15 + 0.7 * t)
        pts.append((-math.sin(a) * 0.03, 0.022 - abs(t - 0.5) * 0.012, math.cos(a) * 0.045))
        rad.append(0.008 + 0.014 * math.sin(math.pi * t))
    tube(pts, rad, 'croissant', sides=6 if detail else 5)
    if detail:
        for i in (2, 4):
            box((0.012, 0.004, 0.004), (pts[i][0], pts[i][1] + rad[i] * 0.9, pts[i][2]), 'bread_dark')


def croissants():
    return grid_row('Stock_Croissants', 'Croissants', croissant_unit, 0.075, 0.1, jit=0.005, yaw_jit=20)


CAKE = ['cake_pink', 'chocolate', 'cake_cream']


def cake_unit(detail, v):
    c = CAKE[v]
    cyl(0.1, 0.012, (0, 0.006, 0), 'white', verts=12)
    cyl(0.088, 0.09, (0, 0.057, 0), c, verts=12)
    cyl(0.09, 0.016, (0, 0.11, 0), 'cake_cream' if c != 'cake_cream' else 'cake_pink', verts=12)
    ball(0.013, (0, 0.13, 0), 'red', subdiv=1)
    if detail:
        for i in range(8):
            a = i * math.pi / 4
            ball(0.012, (math.cos(a) * 0.07, 0.123, math.sin(a) * 0.07), 'cake_cream', subdiv=1)
        cyl(0.0885, 0.01, (0, 0.03, 0), 'cake_cream', verts=12)


def cakes():
    return grid_row('Stock_Cakes', 'Cakes', cake_unit, 0.2, 0.22, cols=1, variants=3)


# ------------------------------------------------------------------ deli counter trays (frame along X, facing -Z)

def tray_row(name, product, fill):
    """Three white trays across the counter; fill(x) adds the food on one tray."""
    kit.begin()
    for x in (-0.33, 0.0, 0.33):
        box((0.3, 0.02, 0.36), (x, 0.01, -0.03), 'white')
        box((0.3, 0.025, 0.012), (x, 0.0125, -0.204), 'white')
        box((0.3, 0.025, 0.012), (x, 0.0125, 0.144), 'white')
        fill(x)
    row = kit.finish(name)
    kit.begin()
    fill(0.0)
    item = kit.finish('Item_' + product.replace(' ', '_'))
    for f in item.data.polygons:
        f.use_smooth = False
    ITEMS.append(item)
    return row


def ham():
    def f(x):
        # fanned slices: overlapping pink discs, each a little higher, three rows per tray
        for j in range(3):
            z = -0.13 + j * 0.1
            for i in range(5):
                sx = x - 0.1 + i * 0.045
                y = 0.024 + i * 0.006
                cyl(0.046, 0.005, (sx, y, z), 'meat_fat', verts=10)
                cyl(0.042, 0.006, (sx, y + 0.002, z), 'ham', verts=10)
        box((0.02, 0.008, 0.03), (x + 0.12, 0.06, -0.03), 'leaf')
    return tray_row('Deli_Sliced_Ham', 'Sliced Ham', f)


def chicken():
    def f(x):
        for j, dz in enumerate((-0.11, 0.05)):
            cx = x
            ball(0.07, (cx, 0.06, dz), 'chicken', subdiv=1, scale=(1.25, 0.8, 1))
            ball(0.04, (cx + 0.02, 0.1, dz), 'chicken_dark', subdiv=1, scale=(1.2, 0.5, 1))
            for side in (-1, 1):
                tube([(cx + 0.06, 0.05, dz + side * 0.035), (cx + 0.1, 0.06, dz + side * 0.05), (cx + 0.125, 0.065, dz + side * 0.055)],
                     [0.022, 0.016, 0.006], 'chicken', sides=5)
                box((0.012, 0.012, 0.012), (cx + 0.13, 0.066, dz + side * 0.056), 'white')
    return tray_row('Deli_Chicken', 'Chicken', f)


SALADS = [('salad', 'tomato'), ('pasta', 'red'), ('crumb', 'leaf'), ('coleslaw', 'orange')]


def salads():
    def f(x):
        k = 0
        for i in range(2):
            for j in range(2):
                base, bits = SALADS[(k + int((x + 0.4) * 10)) % 4]
                k += 1
                bx, bz = x - 0.07 + i * 0.14, -0.11 + j * 0.15
                cyl(0.06, 0.05, (bx, 0.04, bz), 'white', verts=10, radius2=0.066)
                ball(0.056, (bx, 0.066, bz), base, subdiv=1, scale=(1, 0.38, 1))
                for a in range(3):
                    ang = a * 2.1 + i
                    box((0.012, 0.01, 0.012), (bx + math.cos(ang) * 0.03, 0.083, bz + math.sin(ang) * 0.03), bits)
    return tray_row('Deli_Salads', 'Salads', f)


def steak():
    def f(x):
        for i in range(2):
            for j in range(3):
                sx, sz = x - 0.06 + i * 0.12, -0.13 + j * 0.1
                box((0.11, 0.024, 0.08), (sx, 0.032, sz), 'meat', bevel=0.008)
                box((0.11, 0.025, 0.014), (sx, 0.032, sz + 0.036), 'meat_fat')
                for m in (-0.03, 0.0, 0.025):
                    box((0.05, 0.002, 0.004), (sx + m * 0.5, 0.045, sz + m * 0.5), 'meat_fat')
        box((0.03, 0.008, 0.01), (x + 0.11, 0.06, -0.03), 'leaf')
    return tray_row('Deli_Steak', 'Steak', f)


GROCERY = [pasta, rice, soup, cereal, coffee, snacks, bubbla, zing, sliced_bread]
HOUSEHOLD = [detergent, toilet_paper, dish_soap, batteries]
DAIRY = [milk, eggs, cheese, yogurt, frozen_pizza, ice_cream]
PRODUCE = [apples, bananas, potatoes, tomatoes, lettuce]
BAKERY = [fresh_bread, croissants, cakes]
DELI = [ham, chicken, salads, steak]
ALL = GROCERY + HOUSEHOLD + DAIRY + PRODUCE + BAKERY + DELI
