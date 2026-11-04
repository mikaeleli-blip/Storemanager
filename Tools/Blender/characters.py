"""
People: chunky low-poly body parts that the game assembles and animates.

Every mesh is named C_<Part>__<role>. The game spawns all meshes of a part together on one joint and colours
each by its role: skin, hair, top, bottom, shoes, accent, accent2 get a plain material in that person's
colour; pal uses the shared palette (eyes, buttons, badges, stripes).

Frames (Unity space, the person faces +Z, right is +X; 1.75 m adult, the game scales by height):
  Torso*, Skirt, Apron, Vest, Blazer, Lanyard: origin at the hips (0.82 m up), torso top at y = 0.6
  Head, Hair*, Beard, Glasses, Hat*: origin at the neck (top of the torso)
  Arm*: origin at the shoulder, hanging to the hand bottom at y = -0.6
  Leg*: origin at the hip joint, the sole at y = -0.82
  Basket, Bag, Cane, Phone: origin at the grip in the hand
  Trolley: origin at the middle of the handle (held about 1 m above the floor)
"""
import math

import bmesh
import bpy

import kit
from kit import ball, begin, box, box_mm, cyl, finish

TINT = 'white'   # tinted parts are modelled white; the game recolours them

# joint layout shared with the game (CharacterRig.cs)
HIP_Y = 0.82
TORSO_H = 0.6
SHOULDER = (0.27, 0.56)
HIP_X = 0.095
ARM_LEN = 0.6


def taper(y0, y1, w0, w1, d0, d1, color, z=0.0, x=0.0):
    """A box narrowing (or widening) from bottom (w0 x d0) at y0 to top (w1 x d1) at y1."""
    vs = []
    for (yy, w, d) in ((y0, w0, d0), (y1, w1, d1)):
        for sx, sz in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
            vs.append((x + sx * w / 2, yy, z + sz * d / 2))
    faces = [[0, 1, 2, 3], [4, 5, 6, 7], [0, 1, 5, 4], [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7]]
    o = kit.poly(vs, faces, color)
    bm = bmesh.new()
    bm.from_mesh(o.data)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.to_mesh(o.data)
    bm.free()
    return o


def part(name, fn):
    """One mesh whose vertex coordinates are exactly the frame above (origin at the joint)."""
    begin()
    fn()
    o = finish(name)
    bpy.ops.object.select_all(action='DESELECT')
    o.select_set(True)
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.transform_apply(location=True, rotation=False, scale=False)
    o.select_set(False)
    return o


# ------------------------------------------------------------------ torsos (hips frame)

def torso_body(color=TINT):
    taper(0.07, 0.58, 0.36, 0.44, 0.22, 0.25, color)
    box_mm((-0.23, 0.54, -0.125), (0.23, 0.6, 0.125), color, bevel=0.02)


def waist(color=TINT):
    taper(0.0, 0.085, 0.36, 0.365, 0.225, 0.228, color)


def tee():
    return [part('C_TorsoTee__top', torso_body),
            part('C_TorsoTee__bottom', waist),
            part('C_TorsoTee__skin', lambda: box((0.1, 0.05, 0.012), (0, 0.575, 0.126), TINT))]


def sweater():
    def top():
        torso_body()
        taper(0.06, 0.12, 0.37, 0.37, 0.235, 0.235, TINT)
        cyl(0.075, 0.04, (0, 0.6, 0), TINT, verts=8)
    return [part('C_TorsoSweater__top', top), part('C_TorsoSweater__bottom', waist)]


def hoodie():
    def top():
        torso_body()
        box_mm((-0.15, 0.5, -0.17), (0.15, 0.66, -0.06), TINT, bevel=0.03)
        box_mm((-0.13, 0.14, 0.11), (0.13, 0.27, 0.135), TINT, bevel=0.01)
    def strings():
        for x in (-0.04, 0.04):
            box((0.012, 0.11, 0.01), (x, 0.5, 0.13), 'white')
    return [part('C_TorsoHoodie__top', top), part('C_TorsoHoodie__bottom', waist),
            part('C_TorsoHoodie__pal', strings)]


def jacket():
    def shell():
        torso_body()
        box_mm((-0.17, 0.56, -0.13), (0.17, 0.65, -0.05), TINT)
        for sx in (-1, 1):
            box_mm((min(sx * 0.06, sx * 0.19), 0.55, 0.06), (max(sx * 0.06, sx * 0.19), 0.64, 0.13), TINT)
    def inner():
        box((0.1, 0.48, 0.012), (0, 0.33, 0.127), TINT)
    def zips():
        for x in (-0.055, 0.055):
            box((0.008, 0.48, 0.008), (x, 0.33, 0.131), 'steel')
    return [part('C_TorsoJacket__top', shell), part('C_TorsoJacket__accent', inner),
            part('C_TorsoJacket__pal', zips), part('C_TorsoJacket__bottom', waist)]


def coat():
    def shell():
        torso_body()
        taper(-0.34, 0.08, 0.44, 0.38, 0.27, 0.24, TINT)
        box_mm((-0.17, 0.56, -0.13), (0.17, 0.65, -0.05), TINT)
    def buttons():
        for y in (0.42, 0.3, 0.18, 0.04, -0.1):
            box((0.025, 0.025, 0.01), (0.03, y, 0.132), 'darkgrey')
    return [part('C_TorsoCoat__top', shell), part('C_TorsoCoat__pal', buttons)]


def polo():
    """Staff polo: role-coloured, a collar, buttons, the store logo and a name badge."""
    def top():
        torso_body()
        for sx in (-1, 1):
            box_mm((min(sx * 0.02, sx * 0.13), 0.57, 0.05), (max(sx * 0.02, sx * 0.13), 0.63, 0.135), TINT)
        box_mm((-0.16, 0.57, -0.13), (0.16, 0.63, -0.04), TINT)
    def details():
        for y in (0.52, 0.46):
            box((0.018, 0.018, 0.01), (0, y, 0.13), 'white')
        box((0.07, 0.055, 0.01), (-0.11, 0.44, 0.128), 'brand')
        box((0.03, 0.03, 0.012), (-0.11, 0.445, 0.131), 'white')
        box((0.08, 0.035, 0.01), (0.11, 0.44, 0.128), 'white')
        box((0.08, 0.01, 0.011), (0.11, 0.455, 0.13), 'brand')
    return [part('C_TorsoPolo__top', top), part('C_TorsoPolo__pal', details), part('C_TorsoPolo__bottom', waist)]


def shirt():
    """White shirt with a collar and a tie (accent2). The manager wears a blazer over it."""
    def top():
        torso_body()
        for sx in (-1, 1):
            box_mm((min(sx * 0.015, sx * 0.1), 0.56, 0.06), (max(sx * 0.015, sx * 0.1), 0.63, 0.134), TINT)
    def tie():
        box((0.05, 0.05, 0.02), (0, 0.55, 0.135), TINT)
        taper(0.12, 0.53, 0.07, 0.04, 0.012, 0.012, TINT, z=0.133)
    def buttons():
        for y in (0.4, 0.28, 0.16):
            box((0.014, 0.014, 0.008), (0.045, y, 0.128), 'lightgrey')
    return [part('C_TorsoShirt__top', top), part('C_TorsoShirt__accent2', tie),
            part('C_TorsoShirt__pal', buttons), part('C_TorsoShirt__bottom', waist)]


# ------------------------------------------------------------------ accessories (hips frame)

def skirt():
    return [part('C_Skirt__bottom', lambda: taper(-0.3, 0.09, 0.44, 0.37, 0.3, 0.235, TINT))]


def apron():
    def cloth():
        box_mm((-0.17, -0.38, 0.128), (0.17, 0.3, 0.138), TINT)
        box_mm((-0.12, 0.3, 0.128), (0.12, 0.52, 0.138), TINT)
        for sx in (-1, 1):
            box_mm((min(sx * 0.1, sx * 0.13), 0.52, -0.13), (max(sx * 0.1, sx * 0.13), 0.607, 0.138), TINT)
        box_mm((-0.19, 0.08, -0.13), (0.19, 0.11, 0.138), TINT)
    def stripes():
        for x in (-0.12, -0.06, 0.0, 0.06, 0.12):
            box_mm((x - 0.008, -0.37, 0.139), (x + 0.008, 0.29, 0.141), 'white')
    return [part('C_Apron__accent', cloth), part('C_ApronStripes__pal', stripes)]


def vest():
    def shell():
        for sx in (-1, 1):
            box_mm((min(sx * 0.05, sx * 0.235), 0.08, 0.12), (max(sx * 0.05, sx * 0.235), 0.58, 0.14), TINT)
            box_mm((min(sx * 0.22, sx * 0.235), 0.08, -0.13), (max(sx * 0.22, sx * 0.235), 0.58, 0.14), TINT)
        box_mm((-0.235, 0.08, -0.14), (0.235, 0.58, -0.12), TINT)
    def stripes():
        for y in (0.2, 0.32):
            for sx in (-1, 1):
                box_mm((min(sx * 0.05, sx * 0.236), y, 0.141), (max(sx * 0.05, sx * 0.236), y + 0.035, 0.145), 'lightgrey')
            box_mm((-0.236, y, -0.145), (0.236, y + 0.035, -0.141), 'lightgrey')
    return [part('C_Vest__accent', shell), part('C_VestStripes__pal', stripes)]


def blazer():
    def shell():
        box_mm((-0.235, 0.05, -0.135), (0.235, 0.62, -0.1), TINT)
        for sx in (-1, 1):
            box_mm((min(sx * 0.2, sx * 0.24), 0.05, -0.13), (max(sx * 0.2, sx * 0.24), 0.62, 0.13), TINT)
            box_mm((min(sx * 0.07, sx * 0.235), 0.05, 0.12), (max(sx * 0.07, sx * 0.235), 0.62, 0.142), TINT)
            box_mm((min(sx * 0.055, sx * 0.11), 0.4, 0.14), (max(sx * 0.055, sx * 0.11), 0.6, 0.15), TINT)
        box_mm((-0.235, -0.12, -0.135), (0.235, 0.05, 0.142), TINT)
    def details():
        box((0.05, 0.025, 0.01), (-0.15, 0.46, 0.146), 'white')
        for y in (0.22, 0.1):
            box((0.022, 0.022, 0.01), (0.085, y, 0.146), 'black')
    return [part('C_Blazer__accent', shell), part('C_Blazer__pal', details)]


def lanyard():
    def strap():
        for sx in (-1, 1):
            box_mm((min(sx * 0.05, sx * 0.065), 0.36, 0.128), (max(sx * 0.05, sx * 0.065), 0.6, 0.134), 'brand')
        box((0.08, 0.1, 0.012), (0, 0.31, 0.134), 'white')
        box((0.08, 0.02, 0.013), (0, 0.345, 0.135), 'brand')
    return [part('C_Lanyard__pal', strap)]


# ------------------------------------------------------------------ head, face, hair (neck frame)

def head():
    def skin():
        box_mm((-0.055, 0.0, -0.055), (0.055, 0.07, 0.055), TINT)
        box_mm((-0.135, 0.05, -0.13), (0.135, 0.34, 0.13), TINT, bevel=0.035)
        box((0.05, 0.06, 0.04), (0, 0.17, 0.145), TINT)
        for sx in (-1, 1):
            box((0.03, 0.07, 0.05), (sx * 0.14, 0.18, 0.0), TINT)
    def face():
        for sx in (-1, 1):
            box((0.036, 0.05, 0.01), (sx * 0.06, 0.205, 0.131), 'black')
            box((0.012, 0.014, 0.004), (sx * 0.06 + 0.008, 0.218, 0.137), 'white')
            box((0.06, 0.013, 0.01), (sx * 0.062, 0.252, 0.131), 'darkgrey')
        box((0.075, 0.013, 0.01), (0, 0.105, 0.131), 'maroon')
    return [part('C_Head__skin', skin), part('C_Head__pal', face)]


def hair_cap():
    box_mm((-0.145, 0.3, -0.14), (0.145, 0.375, 0.14), TINT, bevel=0.03)
    box_mm((-0.145, 0.15, -0.155), (0.145, 0.34, -0.12), TINT, bevel=0.015)
    box_mm((-0.12, 0.29, 0.12), (0.12, 0.33, 0.145), TINT)
    for sx in (-1, 1):
        box_mm((min(sx * 0.13, sx * 0.148), 0.2, -0.13), (max(sx * 0.13, sx * 0.148), 0.33, 0.06), TINT)


def hairstyles():
    def short():
        hair_cap()
    def long_():
        hair_cap()
        box_mm((-0.155, -0.04, -0.17), (0.155, 0.34, -0.11), TINT)
        for sx in (-1, 1):
            box_mm((min(sx * 0.13, sx * 0.16), 0.02, -0.14), (max(sx * 0.13, sx * 0.16), 0.33, 0.05), TINT)
    def bun():
        hair_cap()
        ball(0.07, (0, 0.38, -0.12), TINT, subdiv=1)
    def pony():
        hair_cap()
        from products import tube
        tube([(0, 0.32, -0.15), (0, 0.24, -0.2), (0, 0.12, -0.21), (0, 0.03, -0.19)], [0.04, 0.045, 0.035, 0.015], TINT, sides=6)
    def curly():
        for i in range(9):
            a = i * 2 * math.pi / 9
            ball(0.07, (math.sin(a) * 0.11, 0.33 + (i % 2) * 0.02, math.cos(a) * 0.1 - 0.02), TINT, subdiv=1)
        ball(0.09, (0, 0.37, -0.02), TINT, subdiv=1)
        box_mm((-0.145, 0.15, -0.155), (0.145, 0.32, -0.11), TINT, bevel=0.02)
    def buzz():
        box_mm((-0.14, 0.32, -0.135), (0.14, 0.355, 0.13), TINT)
        box_mm((-0.14, 0.18, -0.14), (0.14, 0.33, -0.125), TINT)
    def bald():
        box_mm((-0.145, 0.12, -0.15), (0.145, 0.25, -0.12), TINT)
        for sx in (-1, 1):
            box_mm((min(sx * 0.13, sx * 0.15), 0.15, -0.13), (max(sx * 0.13, sx * 0.15), 0.26, -0.02), TINT)
    def beard():
        box_mm((-0.12, 0.04, 0.09), (0.12, 0.14, 0.142), TINT, bevel=0.015)
        box_mm((-0.05, 0.12, 0.13), (0.05, 0.145, 0.15), TINT)
    out = []
    for name, fn in (('Short', short), ('Long', long_), ('Bun', bun), ('Pony', pony), ('Curly', curly),
                     ('Buzz', buzz), ('Bald', bald)):
        out.append(part('C_Hair' + name + '__hair', fn))
    out.append(part('C_Beard__hair', beard))
    return out


def glasses():
    def frames():
        for sx in (-1, 1):
            cx = sx * 0.06
            box_mm((cx - 0.036, 0.228, 0.134), (cx + 0.036, 0.238, 0.142), 'black')
            box_mm((cx - 0.036, 0.172, 0.134), (cx + 0.036, 0.181, 0.142), 'black')
            box_mm((cx - 0.036, 0.172, 0.134), (cx - 0.028, 0.238, 0.142), 'black')
            box_mm((cx + 0.028, 0.172, 0.134), (cx + 0.036, 0.238, 0.142), 'black')
            box_mm((min(sx * 0.094, sx * 0.142), 0.226, 0.13), (max(sx * 0.094, sx * 0.142), 0.236, 0.14), 'black')
            box_mm((sx * 0.142 - 0.006, 0.226, -0.03), (sx * 0.142 + 0.006, 0.236, 0.14), 'black')
        box((0.05, 0.01, 0.008), (0, 0.222, 0.138), 'black')
    return [part('C_Glasses__pal', frames)]


def hats():
    def baker():
        cyl(0.158, 0.09, (0, 0.335, 0), 'white', verts=10)
        ball(0.16, (0, 0.44, 0), 'white', subdiv=1, scale=(1.05, 0.65, 1.05))
    def cap():
        box_mm((-0.145, 0.3, -0.14), (0.145, 0.4, 0.14), TINT, bevel=0.03)
        box_mm((-0.12, 0.3, 0.12), (0.12, 0.32, 0.26), TINT)
    return [part('C_HatBaker__pal', baker), part('C_Cap__accent', cap)]


# ------------------------------------------------------------------ arms (shoulder frame) and legs (hip frame)

def arms():
    def short_sleeve():
        box_mm((-0.068, -0.19, -0.07), (0.068, 0.03, 0.07), TINT, bevel=0.02)
    def short_skin():
        taper(-0.48, -0.19, 0.09, 0.1, 0.09, 0.1, TINT)
        box_mm((-0.058, -0.6, -0.058), (0.058, -0.47, 0.058), TINT, bevel=0.02)
    def long_sleeve():
        box_mm((-0.068, -0.2, -0.07), (0.068, 0.03, 0.07), TINT, bevel=0.02)
        taper(-0.48, -0.19, 0.105, 0.12, 0.105, 0.12, TINT)
    def hand():
        box_mm((-0.058, -0.6, -0.058), (0.058, -0.47, 0.058), TINT, bevel=0.02)
    return [part('C_ArmShort__top', short_sleeve), part('C_ArmShort__skin', short_skin),
            part('C_ArmLong__top', long_sleeve), part('C_ArmLong__skin', hand)]


def legs():
    def trousers():
        taper(-0.74, 0.02, 0.14, 0.17, 0.15, 0.19, TINT)
    def bare():
        taper(-0.74, 0.02, 0.11, 0.15, 0.12, 0.16, TINT)
    def shoe():
        box_mm((-0.075, -0.82, -0.08), (0.075, -0.73, 0.17), TINT, bevel=0.025)
    def sole():
        box_mm((-0.077, -0.825, -0.082), (0.077, -0.8, 0.172), 'offwhite')
    return [part('C_Leg__bottom', trousers), part('C_Leg__shoes', shoe), part('C_Leg__pal', sole),
            part('C_LegBare__skin', bare), part('C_LegBare__shoes', shoe), part('C_LegBare__pal', sole)]


# ------------------------------------------------------------------ held things

def basket():
    def b():
        top, bot = -0.07, -0.25
        box_mm((-0.13, bot, -0.19), (0.13, bot + 0.015, 0.19), 'red')
        for sx in (-1, 1):
            box_mm((min(sx * 0.12, sx * 0.13), bot, -0.19), (max(sx * 0.12, sx * 0.13), top, 0.19), 'red')
        for sz in (-1, 1):
            box_mm((-0.13, bot, min(sz * 0.18, sz * 0.19)), (0.13, top, max(sz * 0.18, sz * 0.19)), 'red')
        for y in (bot + 0.05, bot + 0.11):
            box_mm((-0.132, y, -0.192), (0.132, y + 0.012, 0.192), 'maroon')
        for sz in (-1, 1):
            box_mm((-0.012, top, sz * 0.08 - 0.01), (0.012, 0.0, sz * 0.08 + 0.01), 'black')
        box_mm((-0.012, -0.012, -0.09), (0.012, 0.012, 0.09), 'black')
    return [part('C_Basket__pal', b)]


def trolley():
    def t():
        z0, z1 = 0.06, 0.76            # wire basket front/back
        y0, y1 = -0.56, -0.13          # basket floor / rim
        box_mm((-0.26, -0.02, -0.02), (0.26, 0.02, 0.02), 'red')                 # handle
        box_mm((-0.12, -0.07, -0.005), (0.12, -0.02, 0.05), 'red')              # handle plate
        for sx in (-1, 1):
            box_mm((sx * 0.25 - 0.012, -0.95, -0.01), (sx * 0.25 + 0.012, 0.0, 0.02), 'steel')   # back posts
            box_mm((sx * 0.22 - 0.012, -0.95, 0.0), (sx * 0.22 + 0.012, -0.925, z1), 'steel')    # base rails
        box_mm((-0.25, y0, z0), (0.25, y0 + 0.012, z1), 'steel')
        for x in (-0.25, 0.25):
            for z in (z0, z1):
                box_mm((x - 0.01, y0, z - 0.01), (x + 0.01, y1, z + 0.01), 'steel')
        for y in (y1, (y0 + y1) / 2):
            box_mm((-0.26, y - 0.008, z0 - 0.01), (-0.24, y + 0.008, z1 + 0.01), 'steel')
            box_mm((0.24, y - 0.008, z0 - 0.01), (0.26, y + 0.008, z1 + 0.01), 'steel')
            box_mm((-0.26, y - 0.008, z1 - 0.01), (0.26, y + 0.008, z1 + 0.01), 'steel')
            box_mm((-0.26, y - 0.008, z0 - 0.01), (0.26, y + 0.008, z0 + 0.01), 'steel')
        for k in range(1, 5):
            z = z0 + k * (z1 - z0) / 5
            box_mm((-0.255, y0, z - 0.004), (-0.245, y1, z + 0.004), 'steel_dark')
            box_mm((0.245, y0, z - 0.004), (0.255, y1, z + 0.004), 'steel_dark')
        for k in range(1, 4):
            x = -0.25 + k * 0.5 / 4
            box_mm((x - 0.004, y0, z1 - 0.005), (x + 0.004, y1, z1 + 0.005), 'steel_dark')
        box_mm((-0.2, -0.85, 0.12), (0.2, -0.836, 0.68), 'steel_dark')           # bottom tray
        for x in (-0.22, 0.22):
            for z in (0.04, z1 - 0.04):
                cyl(0.04, 0.03, (x, -0.95, z), 'black', verts=8, axis='x')
    return [part('C_Trolley__pal', t)]


def bag():
    def b():
        box_mm((-0.13, -0.38, -0.08), (0.13, -0.06, 0.08), 'kraft')
        box_mm((-0.132, -0.2, -0.082), (0.132, -0.17, 0.082), 'brand')
        for sx in (-1, 1):
            box_mm((sx * 0.06 - 0.008, -0.07, -0.005), (sx * 0.06 + 0.008, 0.0, 0.005), 'cardboard_dark')
        box_mm((-0.06, -0.012, -0.005), (0.06, 0.0, 0.005), 'cardboard_dark')
        box_mm((-0.05, -0.08, -0.02), (0.06, -0.02, 0.05), 'bread')
        box_mm((0.02, -0.07, -0.06), (0.09, -0.03, -0.01), 'leaf')
    return [part('C_Bag__pal', b)]


def cane():
    def c():
        box_mm((-0.012, -0.8, -0.012), (0.012, 0.0, 0.012), 'wood_dark')
        box_mm((-0.012, -0.012, -0.012), (0.012, 0.012, 0.09), 'wood_dark')
        box_mm((-0.016, -0.82, -0.016), (0.016, -0.79, 0.016), 'black')
    return [part('C_Cane__pal', c)]


def phone():
    def p():
        box_mm((-0.042, -0.02, -0.007), (0.042, 0.15, 0.007), 'black')
        box_mm((-0.036, -0.01, -0.009), (0.036, 0.14, -0.007), 'screen')
    return [part('C_Phone__pal', p)]


ALL = [tee, sweater, hoodie, jacket, coat, polo, shirt, skirt, apron, vest, blazer, lanyard,
       head, hairstyles, glasses, hats, arms, legs, basket, trolley, bag, cane, phone]


def build():
    out = []
    for fn in ALL:
        out.extend(fn())
    return out
