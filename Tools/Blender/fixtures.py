"""
Store fixtures and decor. Unity space, origin on the floor.

Shelf-type fixtures are 1 m segments running along Z with customers on both
X sides; the game tiles them along a 10 m aisle. Stock rows sit on the
levels listed in FixtureSpec (Assets/.../World/ModelLibrary.cs).
"""
import math

from kit import ball, begin, box, box_mm, cyl, finish, poly, prism, text_mesh

LEVELS = (0.12, 0.52, 0.92, 1.32)   # gondola / fridge shelf tops


def tbox(mn, mx, color, tilt_deg, pivot):
    """Box defined in a frame tilted about Unity Z by tilt_deg around pivot (x, y)."""
    t = math.radians(tilt_deg)
    c, s = math.cos(t), math.sin(t)
    px, py = pivot
    vs = []
    for x in (mn[0], mx[0]):
        for y in (mn[1], mx[1]):
            for z in (mn[2], mx[2]):
                vs.append((px + x * c - y * s, py + x * s + y * c, z))
    # index = ix*4 + iy*2 + iz
    def i(ix, iy, iz):
        return ix * 4 + iy * 2 + iz
    faces = [
        [i(0, 0, 0), i(0, 0, 1), i(0, 1, 1), i(0, 1, 0)],
        [i(1, 0, 0), i(1, 1, 0), i(1, 1, 1), i(1, 0, 1)],
        [i(0, 0, 0), i(1, 0, 0), i(1, 0, 1), i(0, 0, 1)],
        [i(0, 1, 0), i(0, 1, 1), i(1, 1, 1), i(1, 1, 0)],
        [i(0, 0, 0), i(0, 1, 0), i(1, 1, 0), i(1, 0, 0)],
        [i(0, 0, 1), i(1, 0, 1), i(1, 1, 1), i(0, 1, 1)],
    ]
    o = poly(vs, faces, color)
    import bmesh
    bm = bmesh.new()
    bm.from_mesh(o.data)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.to_mesh(o.data)
    bm.free()
    return o


def both_sides(fn):
    """Call fn(sign) for the +X side (1) and the -X side (-1)."""
    fn(1)
    fn(-1)


# ------------------------------------------------------------------ shelf fixtures

def gondola():
    begin()
    box_mm((-0.44, 0, -0.5), (0.44, 0.12, 0.5), 'darkgrey')
    box_mm((-0.04, 0.12, -0.5), (0.04, 1.75, 0.5), 'shelf')
    box_mm((-0.06, 0, -0.5), (0.06, 1.8, -0.46), 'steel_dark')
    box_mm((-0.07, 1.75, -0.5), (0.07, 1.86, 0.5), 'brand')

    def side(sg):
        for y in LEVELS[1:]:
            box_mm((min(0.04 * sg, 0.45 * sg), y - 0.03, -0.5), (max(0.04 * sg, 0.45 * sg), y, 0.5), 'shelf')
            box_mm((min(0.43 * sg, 0.46 * sg), y - 0.05, -0.5), (max(0.43 * sg, 0.46 * sg), y, 0.5), 'price')
        box_mm((min(0.43 * sg, 0.45 * sg), 0.0, -0.5), (max(0.43 * sg, 0.45 * sg), 0.12, 0.5), 'brand')
    both_sides(side)
    return finish('Gondola')


def gondola_end():
    """End cap for a gondola run, facing -Z. 0.9 wide, sits in front of the first segment."""
    begin()
    box_mm((-0.45, 0, -0.08), (0.45, 1.8, 0.0), 'brand')
    box_mm((-0.42, 0.12, -0.3), (0.42, 0.16, -0.08), 'shelf')
    box_mm((-0.42, 0.6, -0.25), (0.42, 0.63, -0.08), 'shelf')
    box_mm((-0.42, 1.05, -0.2), (0.42, 1.08, -0.08), 'shelf')
    box_mm((-0.45, 0, -0.32), (0.45, 0.12, -0.08), 'darkgrey')
    box_mm((-0.3, 1.45, -0.1), (0.3, 1.7, -0.08), 'white')
    box_mm((-0.25, 1.52, -0.11), (0.25, 1.63, -0.1), 'red')
    # a few promo boxes
    for i, x in enumerate((-0.28, 0.0, 0.28)):
        box((0.22, 0.25, 0.18), (x, 0.29, -0.2), ('orange', 'yellow', 'red')[i], bevel=0.01)
        box((0.18, 0.2, 0.14), (x, 0.73, -0.17), ('teal', 'pink', 'lime')[i], bevel=0.01)
    return finish('GondolaEnd')


def fridge():
    """
    Double-sided glass-door fridge, one 1 m section. Two doors per side with frames and handles, an
    insulated canopy with a light and a blue band, shelves and price strips behind the glass.
    """
    begin()
    box_mm((-0.46, 0, -0.5), (0.46, 0.12, 0.5), 'darkgrey')
    box_mm((-0.06, 0.12, -0.5), (0.06, 1.75, 0.5), 'offwhite')
    # canopy
    box_mm((-0.47, 1.75, -0.5), (0.47, 2.0, 0.5), 'white')
    box_mm((-0.475, 1.84, -0.5), (0.475, 1.94, 0.5), 'milk')

    def side(sg):
        lo = (lambda a, b: (min(a * sg, b * sg), max(a * sg, b * sg)))
        # inside: shelves, price strips, lights
        for y in LEVELS[1:]:
            x0, x1 = lo(0.06, 0.43)
            box_mm((x0, y - 0.025, -0.5), (x1, y, 0.5), 'steel')
            x0, x1 = lo(0.41, 0.435)
            box_mm((x0, y - 0.04, -0.5), (x1, y, 0.5), 'price')
        x0, x1 = lo(0.06, 0.075)
        box_mm((x0, 0.15, -0.5), (x1, 1.7, 0.5), 'light_cool')
        x0, x1 = lo(0.38, 0.43)
        box_mm((x0, 1.7, -0.5), (x1, 1.74, 0.5), 'light_cool')
        # kick plate
        x0, x1 = lo(0.44, 0.465)
        box_mm((x0, 0.0, -0.5), (x1, 0.12, 0.5), 'milk')
        # door frames: posts at the section ends, rails top and bottom, a split in the middle
        x0, x1 = lo(0.445, 0.475)
        box_mm((x0, 0.12, -0.5), (x1, 1.75, -0.47), 'steel_dark')
        box_mm((x0, 0.12, 0.47), (x1, 1.75, 0.5), 'steel_dark')
        box_mm((x0, 0.12, -0.5), (x1, 0.17, 0.5), 'steel_dark')
        box_mm((x0, 1.69, -0.5), (x1, 1.75, 0.5), 'steel_dark')
        box_mm((x0, 0.12, -0.012), (x1, 1.75, 0.012), 'steel_dark')
        # glass doors
        g0, g1 = lo(0.452, 0.462)
        box_mm((g0, 0.17, -0.47), (g1, 1.69, -0.012), 'glass')
        box_mm((g0, 0.17, 0.012), (g1, 1.69, 0.47), 'glass')
        # handles by the middle split
        h0, h1 = lo(0.475, 0.49)
        box_mm((h0, 0.72, -0.05), (h1, 1.18, -0.035), 'steel')
        box_mm((h0, 0.72, 0.035), (h1, 1.18, 0.05), 'steel')
        # canopy light strip
        l0, l1 = lo(0.47, 0.476)
        box_mm((l0, 1.78, -0.48), (l1, 1.82, 0.48), 'light')
    both_sides(side)
    return finish('Fridge')


FREEZER_FLOOR = 0.45


def freezer():
    """
    Island chest freezer, one 1 m section, open to both sides under sliding glass lids. Products sit on the
    floor of the tub (y = FREEZER_FLOOR) either side of a middle divider; the tub is lined frosty blue.
    """
    begin()
    box_mm((-0.46, 0, -0.5), (0.46, 0.1, 0.5), 'darkgrey')
    box_mm((-0.45, 0.1, -0.5), (0.45, FREEZER_FLOOR - 0.02, 0.5), 'white')
    box_mm((-0.43, FREEZER_FLOOR - 0.02, -0.5), (0.43, FREEZER_FLOOR, 0.5), 'ice')
    # middle divider with price strips
    box_mm((-0.03, FREEZER_FLOOR, -0.5), (0.03, 0.82, 0.5), 'white')
    box_mm((-0.035, 0.82, -0.5), (0.035, 0.86, 0.5), 'steel')
    # end walls of the section
    box_mm((-0.45, FREEZER_FLOOR, -0.5), (0.45, 0.86, -0.485), 'white')
    box_mm((-0.45, FREEZER_FLOOR, 0.485), (0.45, 0.86, 0.5), 'white')

    def side(sg):
        lo = (lambda a, b: (min(a * sg, b * sg), max(a * sg, b * sg)))
        # outer wall with a frosty lining, a blue bumper rail and a steel rim
        x0, x1 = lo(0.43, 0.46)
        box_mm((x0, 0.1, -0.5), (x1, 0.86, 0.5), 'white')
        x0, x1 = lo(0.42, 0.43)
        box_mm((x0, FREEZER_FLOOR, -0.485), (x1, 0.84, 0.485), 'ice')
        x0, x1 = lo(0.03, 0.04)
        box_mm((x0, FREEZER_FLOOR, -0.485), (x1, 0.8, 0.485), 'ice')
        x0, x1 = lo(0.46, 0.475)
        box_mm((x0, 0.5, -0.5), (x1, 0.6, 0.5), 'zing')
        box_mm((x0, 0.1, -0.5), (x1, 0.14, 0.5), 'milk')
        x0, x1 = lo(0.425, 0.465)
        box_mm((x0, 0.86, -0.5), (x1, 0.885, 0.5), 'steel')
        p0, p1 = lo(0.04, 0.05)
        box_mm((p0, 0.78, -0.48), (p1, 0.82, 0.48), 'price')
        # two sliding glass lids per side, slightly overlapping, with frames and finger pulls
        for k, (z0, z1, y) in enumerate(((-0.49, 0.02, 0.868), (-0.02, 0.49, 0.878))):
            g0, g1 = lo(0.045, 0.425)
            box_mm((g0, y, z0), (g1, y + 0.006, z1), 'glass')
            f0, f1 = lo(0.04, 0.43)
            box_mm((f0, y - 0.002, z0), (f1, y + 0.008, z0 + 0.018), 'steel')
            box_mm((f0, y - 0.002, z1 - 0.018), (f1, y + 0.008, z1), 'steel')
            q0, q1 = lo(0.2, 0.27)
            zz = z1 - 0.03 if k == 0 else z0 + 0.03
            box_mm((q0, y + 0.006, zz - 0.012), (q1, y + 0.012, zz + 0.012), 'steel_dark')
    both_sides(side)
    return finish('Freezer')


PRODUCE_PIVOT = (0.0, 0.8)
PRODUCE_TILT = -14.0


def produce_table():
    begin()
    box_mm((-0.4, 0, -0.48), (0.4, 0.5, 0.48), 'wood_dark')
    box_mm((-0.04, 0.5, -0.5), (0.04, 1.05, 0.5), 'wood')
    box_mm((-0.44, 0, -0.5), (0.44, 0.08, 0.5), 'brand_dark')

    def side(sg):
        tilt = PRODUCE_TILT * sg
        piv = (PRODUCE_PIVOT[0], PRODUCE_PIVOT[1])
        lo, hi = (0.02, 0.47) if sg > 0 else (-0.47, -0.02)
        tbox((lo, -0.04, -0.49), (hi, 0.0, 0.49), 'wood_light', tilt, piv)
        flo, fhi = (0.43, 0.47) if sg > 0 else (-0.47, -0.43)
        tbox((flo, 0.0, -0.49), (fhi, 0.07, 0.49), 'wood', tilt, piv)
        for z in (-0.49, 0.47):
            tbox((lo, 0.0, z), (hi, 0.05, z + 0.02), 'wood', tilt, piv)
        # supports under the crate
        box_mm((min(0.38 * sg, 0.42 * sg), 0.45, -0.45), (max(0.38 * sg, 0.42 * sg), 0.72, -0.4), 'wood_dark')
        box_mm((min(0.38 * sg, 0.42 * sg), 0.45, 0.4), (max(0.38 * sg, 0.42 * sg), 0.72, 0.45), 'wood_dark')
    both_sides(side)
    return finish('ProduceTable')


BAKERY_LEVELS = (0.12, 0.62, 1.12)


def bakery_shelf():
    begin()
    box_mm((-0.44, 0, -0.5), (0.44, 0.12, 0.5), 'wood_dark')
    box_mm((-0.04, 0.12, -0.5), (0.04, 1.5, 0.5), 'wood')
    box_mm((-0.06, 0, -0.5), (0.06, 1.55, -0.46), 'wood_dark')
    box_mm((-0.08, 1.5, -0.5), (0.08, 1.6, 0.5), 'bread_dark')

    def side(sg):
        for y in BAKERY_LEVELS[1:]:
            box_mm((min(0.04 * sg, 0.45 * sg), y - 0.03, -0.5), (max(0.04 * sg, 0.45 * sg), y, 0.5), 'wood_light')
        for y in BAKERY_LEVELS:
            # basket lip
            box_mm((min(0.42 * sg, 0.45 * sg), y, -0.5), (max(0.42 * sg, 0.45 * sg), y + 0.05, 0.5), 'wood')
    both_sides(side)
    return finish('BakeryShelf')


def oven():
    """Bakery ovens, 1.9 x 2.9 m, doors facing -X (towards the baker)."""
    begin()
    box_mm((-0.95, 0, -1.45), (0.95, 1.7, 1.45), 'steel')
    box_mm((-0.97, 0, -1.47), (0.97, 0.12, 1.47), 'darkgrey')
    for i, z in enumerate((-0.95, 0.0, 0.95)):
        box_mm((-0.99, 0.35, z - 0.4), (-0.95, 0.95, z + 0.4), 'darkgrey')
        box_mm((-1.0, 0.45, z - 0.3), (-0.98, 0.85, z + 0.3), 'glow_amber')
        box_mm((-1.04, 1.0, z - 0.3), (-0.99, 1.04, z + 0.3), 'steel_dark')
        box_mm((-0.99, 1.15, z - 0.35), (-0.96, 1.5, z + 0.35), 'black')
        for k in range(3):
            box_mm((-1.0, 1.25, z - 0.25 + k * 0.2), (-0.98, 1.33, z - 0.17 + k * 0.2), 'glow_green' if k == 0 else 'red')
    box_mm((-0.8, 1.7, -1.3), (0.8, 2.0, 1.3), 'steel_dark')
    cyl(0.15, 0.6, (0.5, 2.3, 1.0), 'steel', verts=8)
    return finish('Oven')


def deli_counter():
    """1 m of serve-over counter, running along X; customers on the -Z side."""
    begin()
    box_mm((-0.5, 0, -0.42), (0.5, 0.75, 0.4), 'offwhite')
    box_mm((-0.5, 0.1, -0.44), (0.5, 0.7, -0.42), 'red')
    box_mm((-0.5, 0, -0.44), (0.5, 0.1, -0.42), 'darkgrey')
    box_mm((-0.5, 0.72, -0.42), (0.5, 0.76, 0.4), 'steel')
    # glass: sloped front and flat top
    prism([(-0.5, 0.76), (0.5, 0.76), (0.5, 0.78), (-0.5, 0.78)], -0.42, -0.4, 'steel')
    poly([(-0.5, 0.76, -0.42), (0.5, 0.76, -0.42), (0.5, 1.25, -0.18), (-0.5, 1.25, -0.18)],
         [[0, 1, 2, 3]], 'glass')
    poly([(-0.5, 0.76, -0.42), (-0.5, 1.25, -0.18), (0.5, 1.25, -0.18), (0.5, 0.76, -0.42)],
         [[0, 1, 2, 3]], 'glass')
    box_mm((-0.5, 1.24, -0.18), (0.5, 1.27, 0.12), 'glass')
    box_mm((-0.5, 1.2, 0.08), (0.5, 1.24, 0.12), 'light')
    box_mm((-0.5, 0.95, 0.3), (0.5, 1.0, 0.4), 'steel')
    box_mm((-0.02, 0.76, -0.42), (0.02, 1.25, -0.18), 'steel')
    return finish('DeliCounter')


def deli_back():
    """Cold room / back fridge behind the deli clerk, 1 m along X, facing -Z."""
    begin()
    box_mm((-0.5, 0, -0.45), (0.5, 2.0, 0.45), 'white')
    box_mm((-0.48, 0.1, -0.47), (-0.02, 1.9, -0.45), 'offwhite')
    box_mm((0.02, 0.1, -0.47), (0.48, 1.9, -0.45), 'offwhite')
    box_mm((-0.08, 0.9, -0.5), (-0.05, 1.3, -0.47), 'steel')
    box_mm((0.05, 0.9, -0.5), (0.08, 1.3, -0.47), 'steel')
    box_mm((-0.5, 1.9, -0.47), (0.5, 2.0, 0.45), 'red')
    return finish('DeliBack')


def scale_and_slicer():
    begin()
    box_mm((-0.15, 1.0, -0.1), (0.15, 1.08, 0.1), 'white')
    box_mm((-0.12, 1.08, -0.08), (0.12, 1.1, 0.08), 'steel')
    box_mm((-0.08, 1.08, 0.08), (0.08, 1.25, 0.1), 'darkgrey')
    box_mm((-0.06, 1.15, 0.07), (0.06, 1.22, 0.08), 'screen')
    return finish('DeliScale')


# ------------------------------------------------------------------ checkout

def checkout():
    """Checkout counter, 0.8 x 2 m along Z. Cashier on -X, customer on +X, pay end at -Z."""
    begin()
    box_mm((-0.4, 0, -1.0), (0.4, 0.82, 1.0), 'offwhite')
    box_mm((0.4, 0.1, -1.0), (0.42, 0.7, 1.0), 'brand')
    box_mm((-0.42, 0.0, -1.0), (0.42, 0.1, 1.0), 'darkgrey')
    box_mm((-0.06, 0.82, -0.15), (0.34, 0.87, 0.98), 'belt')
    box_mm((0.34, 0.82, -0.15), (0.4, 0.92, 0.98), 'steel')
    box_mm((-0.1, 0.82, -0.15), (-0.06, 0.92, 0.98), 'steel')
    box_mm((-0.06, 0.87, 0.92), (0.34, 0.89, 0.97), 'steel')
    # scanner bed
    box_mm((-0.06, 0.82, -0.45), (0.34, 0.86, -0.15), 'black')
    box_mm((0.02, 0.86, -0.4), (0.26, 0.87, -0.2), 'glow_red')
    # register facing the cashier (-X)
    box_mm((-0.38, 0.82, -0.85), (-0.12, 0.95, -0.5), 'darkgrey')
    box_mm((-0.28, 0.95, -0.8), (-0.22, 1.05, -0.55), 'darkgrey')
    box_mm((-0.32, 1.05, -0.82), (-0.24, 1.32, -0.52), 'black')
    box_mm((-0.33, 1.08, -0.79), (-0.32, 1.29, -0.55), 'screen')
    # card terminal for the customer (+X)
    box_mm((0.3, 0.9, -0.62), (0.38, 0.92, -0.5), 'darkgrey')
    box_mm((0.32, 0.92, -0.6), (0.36, 1.04, -0.52), 'black')
    box_mm((0.36, 0.95, -0.59), (0.365, 1.02, -0.53), 'screen')
    # bagging shelf
    box_mm((0.0, 0.78, -1.0), (0.4, 0.82, -0.86), 'steel')
    # impulse rack at the queue end
    box_mm((0.43, 0.0, 0.3), (0.6, 1.2, 0.95), 'brand_dark')
    for k, y in enumerate((0.25, 0.55, 0.85)):
        for j, z in enumerate((0.42, 0.6, 0.8)):
            box((0.1, 0.18, 0.14), (0.55, y + 0.09, z), ('red', 'yellow', 'purple', 'orange')[(k + j) % 4], bevel=0.01)
    return finish('Checkout')


def lane_pole():
    begin()
    cyl(0.035, 2.0, (0, 1.0, 0), 'steel', verts=8)
    box((0.32, 0.32, 0.06), (0, 1.9, 0), 'brand')
    box((0.22, 0.22, 0.065), (0, 1.9, 0), 'white')
    return finish('LanePole')


# ------------------------------------------------------------------ building

WALL_H = 1.5


def wall():
    """1 m of interior wall along X, 0.25 thick (z -0.125..0.125)."""
    begin()
    box_mm((-0.5, 0, -0.12), (0.5, WALL_H, 0.12), 'wall')
    box_mm((-0.5, 0, -0.14), (0.5, 0.14, 0.14), 'wall_dark')
    box_mm((-0.5, 1.12, -0.135), (0.5, 1.27, 0.135), 'brand')
    box_mm((-0.52, WALL_H, -0.15), (0.52, WALL_H + 0.08, 0.15), 'trim')
    return finish('Wall')


def wall_window():
    """1 m of shop front: low wall and a glazed panel."""
    begin()
    box_mm((-0.5, 0, -0.12), (0.5, 0.45, 0.12), 'wall_dark')
    box_mm((-0.5, 0.45, -0.06), (0.5, 1.75, 0.06), 'glass_tint')
    box_mm((-0.5, 0.45, -0.1), (-0.45, 1.8, 0.1), 'trim')
    box_mm((-0.52, 1.75, -0.15), (0.52, 1.85, 0.15), 'trim')
    box_mm((-0.5, 0.42, -0.14), (0.5, 0.48, 0.14), 'trim')
    return finish('WallWindow')


def wall_post():
    begin()
    box_mm((-0.18, 0, -0.18), (0.18, 1.9, 0.18), 'trim')
    return finish('WallPost')


def entrance():
    """4 m automatic entrance, centred on x = 0, in a wall along X."""
    begin()
    for x in (-2.0, 2.0):
        box_mm((x - 0.15, 0, -0.2), (x + 0.15, 2.4, 0.2), 'trim')
    box_mm((-2.15, 2.2, -0.25), (2.15, 2.75, 0.25), 'brand')
    box_mm((-1.9, 2.3, -0.27), (1.9, 2.65, -0.25), 'white')
    for x in (-1.85, 1.15):
        box_mm((x, 0, -0.04), (x + 0.7, 2.2, 0.04), 'glass')
        box_mm((x, 0, -0.05), (x + 0.7, 0.06, 0.05), 'trim')
        box_mm((x, 2.14, -0.05), (x + 0.7, 2.2, 0.05), 'trim')
    box_mm((-1.6, 0.0, -1.2), (1.6, 0.02, -0.25), 'mat_blue')
    box_mm((-1.6, 0.0, 0.25), (1.6, 0.02, 1.2), 'mat_blue')
    text_mesh('WELCOME', 0.22, (0, 2.47, -0.28), 'brand_dark', depth=0.03)
    return finish('Entrance')


def store_sign():
    """Big rooftop sign letters, readable from the street (-Z)."""
    begin()
    box_mm((-4.6, 0, -0.15), (4.6, 0.25, 0.15), 'trim')
    box_mm((-4.5, 0.25, -0.05), (4.5, 1.25, 0.05), 'brand')
    text_mesh('SUPERMARKET', 0.75, (0, 0.72, -0.1), 'white', depth=0.08)
    return finish('StoreSign')


def dept_sign(name, label, color):
    begin()
    box_mm((-1.6, 0, -0.06), (1.6, 0.62, 0.0), color)
    box_mm((-1.66, -0.04, -0.03), (1.66, 0.66, 0.02), 'white')
    for x in (-1.2, 1.2):
        box_mm((x - 0.03, -0.6, -0.02), (x + 0.03, 0.0, 0.02), 'steel')
    text_mesh(label, 0.32, (0, 0.3, -0.08), 'white', depth=0.03)
    return finish(name)


def dock_door():
    """Roll-up loading door frame, 4 m wide, open."""
    begin()
    for x in (-2.05, 2.05):
        box_mm((x - 0.12, 0, -0.2), (x + 0.12, 2.9, 0.2), 'steel_dark')
    box_mm((-2.2, 2.6, -0.3), (2.2, 3.0, 0.3), 'steel_dark')
    cyl(0.25, 4.2, (0, 2.75, 0.0), 'steel', verts=10, axis='x')
    for x in (-2.0, 2.0):
        for k in range(4):
            box_mm((x - 0.1, 0.1 + k * 0.6, -0.25), (x + 0.1, 0.4 + k * 0.6, -0.2), 'yellow' if k % 2 == 0 else 'black')
    return finish('DockDoor')


# ------------------------------------------------------------------ back room

def rack():
    """1 m of pallet racking along X, 0.8 deep, with boxes."""
    begin()
    for z in (-0.4, 0.36):
        box_mm((-0.5, 0, z), (-0.45, 2.3, z + 0.04), 'navy')
    for y in (0.08, 0.85, 1.6):
        for z in (-0.4, 0.36):
            box_mm((-0.5, y, z), (0.5, y + 0.1, z + 0.04), 'orange')
        box_mm((-0.5, y + 0.08, -0.38), (0.5, y + 0.1, 0.38), 'steel_dark')
    boxes = [(-0.25, 0.3), (0.2, 0.25), (0.05, 0.32)]
    for li, y in enumerate((0.18, 0.95, 1.7)):
        for j, (x, h) in enumerate(boxes):
            hh = h - 0.04 * ((li + j) % 2)
            box((0.38, hh, 0.6), (x, y + hh / 2, 0.0), 'cardboard' if (li + j) % 3 else 'cardboard_dark', bevel=0.01)
            box((0.39, 0.03, 0.61), (x, y + hh * 0.6, 0.0), 'cream')
    return finish('Rack')


def pallet():
    begin()
    box_mm((-0.6, 0, -0.5), (0.6, 0.04, 0.5), 'pallet')
    for x in (-0.55, -0.02, 0.5):
        box_mm((x, 0.04, -0.5), (x + 0.08, 0.12, 0.5), 'pallet')
    box_mm((-0.6, 0.12, -0.5), (0.6, 0.15, 0.5), 'pallet')
    for i in range(2):
        for j in range(2):
            for k in range(3):
                box((0.55, 0.32, 0.46), (-0.29 + i * 0.58, 0.32 + k * 0.33, -0.24 + j * 0.48),
                    'cardboard' if (i + j + k) % 2 else 'cardboard_dark', bevel=0.01)
    return finish('Pallet')


def truck():
    """Delivery truck, rear at z = 0 facing -Z (backs onto the dock), 2.4 wide."""
    begin()
    box_mm((-1.2, 0.7, 0.0), (1.2, 3.2, 5.0), 'truck', bevel=0.03)
    box_mm((-1.22, 1.6, 0.5), (1.22, 2.3, 4.5), 'brand')
    text_mesh('FRESH', 0.45, (0, 2.45, -0.04), 'brand', depth=0.03)
    box_mm((-1.15, 0.8, -0.02), (1.15, 3.1, 0.02), 'lightgrey')
    box_mm((-0.02, 0.8, -0.03), (0.02, 3.1, 0.0), 'steel_dark')
    box_mm((-1.2, 0.4, 0.0), (1.2, 0.75, 7.1), 'darkgrey')
    box_mm((-1.2, 0.75, 5.1), (1.2, 2.6, 7.0), 'truck_cab', bevel=0.05)
    box_mm((-1.1, 1.7, 6.95), (1.1, 2.4, 7.02), 'glass_tint')
    for x in (-1.21, 1.21):
        box_mm((x - 0.01, 1.7, 5.4), (x + 0.01, 2.4, 6.4), 'glass_tint')
    for z in (0.8, 2.0, 6.2):
        for x in (-1.05, 1.05):
            cyl(0.42, 0.32, (x, 0.42, z), 'tyre', verts=10, axis='x')
            cyl(0.22, 0.34, (x, 0.42, z), 'steel', verts=8, axis='x')
    box_mm((-1.1, 0.5, 7.0), (1.1, 0.8, 7.15), 'steel')
    for x in (-0.8, 0.8):
        box_mm((x - 0.15, 0.85, 7.0), (x + 0.15, 1.0, 7.05), 'light')
    return finish('Truck')


# ------------------------------------------------------------------ office

def desk():
    """Manager's desk, 3 x 1 m. The manager sits on the -Z side."""
    begin()
    box_mm((-1.5, 0.72, -0.5), (1.5, 0.78, 0.5), 'wood')
    for x in (-1.42, 1.32):
        box_mm((x, 0, -0.45), (x + 0.1, 0.72, 0.45), 'wood_dark')
    box_mm((0.6, 0.0, -0.4), (1.3, 0.72, 0.4), 'wood_dark')
    for k in range(3):
        box_mm((0.65, 0.08 + k * 0.22, -0.41), (1.25, 0.26 + k * 0.22, -0.4), 'wood_light')
    # monitor facing -Z
    box_mm((-0.1, 0.78, 0.05), (0.1, 0.82, 0.2), 'black')
    box_mm((-0.03, 0.82, 0.12), (0.03, 0.95, 0.16), 'black')
    box_mm((-0.38, 0.95, 0.1), (0.38, 1.4, 0.15), 'black')
    box_mm((-0.35, 0.98, 0.09), (0.35, 1.37, 0.1), 'screen')
    box_mm((-0.25, 0.78, -0.3), (0.25, 0.8, -0.15), 'darkgrey')
    box_mm((0.32, 0.78, -0.28), (0.4, 0.8, -0.18), 'darkgrey')
    # papers, mug, phone
    box_mm((-1.2, 0.78, -0.3), (-0.8, 0.79, 0.0), 'white')
    box_mm((-1.15, 0.79, -0.25), (-0.75, 0.8, 0.05), 'offwhite')
    cyl(0.04, 0.1, (-0.6, 0.83, -0.25), 'red', verts=8)
    box_mm((0.6, 0.78, -0.1), (0.85, 0.83, 0.1), 'darkgrey')
    ball(0.12, (1.2, 0.95, 0.25), 'leaf', subdiv=1)
    cyl(0.08, 0.15, (1.2, 0.85, 0.25), 'brown', verts=8)
    return finish('Desk')


def office_chair():
    begin()
    for a in range(5):
        t = math.radians(a * 72)
        box((0.05, 0.04, 0.28), (math.sin(t) * 0.14, 0.06, math.cos(t) * 0.14), 'black')
    cyl(0.03, 0.4, (0, 0.28, 0), 'steel', verts=6)
    box((0.5, 0.08, 0.48), (0, 0.5, 0), 'black', bevel=0.02)
    box((0.48, 0.55, 0.08), (0, 0.82, -0.24), 'black', bevel=0.02)
    return finish('OfficeChair')


def filing_cabinet():
    begin()
    box_mm((-0.25, 0, -0.3), (0.25, 1.3, 0.3), 'steel')
    for k in range(4):
        box_mm((-0.23, 0.05 + k * 0.32, -0.31), (0.23, 0.33 + k * 0.32, -0.3), 'lightgrey')
        box_mm((-0.06, 0.25 + k * 0.32, -0.33), (0.06, 0.28 + k * 0.32, -0.31), 'darkgrey')
    return finish('FilingCabinet')


def plant():
    begin()
    cyl(0.22, 0.4, (0, 0.2, 0), 'brown', verts=8, radius2=0.18)
    cyl(0.2, 0.02, (0, 0.4, 0), 'wood_dark', verts=8)
    ball(0.3, (0, 0.8, 0), 'tree', subdiv=1)
    ball(0.22, (0.12, 1.1, 0.05), 'leaf', subdiv=1)
    ball(0.2, (-0.12, 1.0, -0.08), 'tree', subdiv=1)
    return finish('Plant')


def whiteboard():
    """Wall board facing -Z."""
    begin()
    box_mm((-0.9, 1.0, -0.04), (0.9, 1.9, 0.0), 'white')
    box_mm((-0.93, 0.97, -0.03), (0.93, 1.93, 0.01), 'steel')
    for k in range(4):
        box_mm((-0.7, 1.7 - k * 0.15, -0.045), (-0.7 + 0.25 + k * 0.25, 1.73 - k * 0.15, -0.04), ('red', 'navy', 'green', 'navy')[k])
    box_mm((-0.5, 0.97, -0.08), (0.5, 1.0, 0.0), 'steel')
    return finish('Whiteboard')


def sofa():
    begin()
    box_mm((-0.9, 0.0, -0.4), (0.9, 0.45, 0.4), 'mat_blue')
    box_mm((-0.9, 0.45, 0.2), (0.9, 0.85, 0.4), 'mat_blue')
    for x in (-0.95, 0.75):
        box_mm((x, 0, -0.4), (x + 0.2, 0.6, 0.4), 'navy')
    return finish('Sofa')


# ------------------------------------------------------------------ lobby / outside

def cart(x0, z0):
    box_mm((x0 - 0.28, 0.45, z0 - 0.4), (x0 + 0.28, 0.95, z0 + 0.45), 'steel')
    box_mm((x0 - 0.25, 0.5, z0 - 0.37), (x0 + 0.25, 0.96, z0 + 0.42), 'lightgrey')
    box_mm((x0 - 0.3, 1.0, z0 + 0.5), (x0 + 0.3, 1.06, z0 + 0.56), 'brand')
    box_mm((x0 - 0.22, 0.1, z0 - 0.35), (x0 + 0.22, 0.14, z0 + 0.45), 'steel_dark')
    for xx in (-0.2, 0.2):
        for zz in (-0.32, 0.42):
            cyl(0.06, 0.04, (x0 + xx, 0.06, z0 + zz), 'black', verts=6, axis='x')


def cart_corral():
    """Row of nested trolleys along Z (front at -Z)."""
    begin()
    for i in range(5):
        cart(0.0, i * 0.3)
    box_mm((-0.4, 0, -0.6), (-0.36, 0.9, 1.8), 'brand_dark')
    box_mm((0.36, 0, -0.6), (0.4, 0.9, 1.8), 'brand_dark')
    return finish('CartCorral')


def basket_stack():
    begin()
    for k in range(6):
        y = k * 0.06
        box_mm((-0.25, y, -0.18), (0.25, y + 0.2, 0.18), 'red' if k < 5 else 'red')
    box_mm((-0.22, 0.32, -0.15), (0.22, 0.52, 0.15), 'red')
    box_mm((-0.04, 0.5, -0.17), (0.04, 0.58, 0.17), 'black')
    return finish('BasketStack')


def tree():
    begin()
    cyl(0.12, 1.4, (0, 0.7, 0), 'trunk', verts=6)
    ball(0.9, (0, 2.0, 0), 'tree', subdiv=1)
    ball(0.6, (0.4, 2.5, 0.2), 'leaf', subdiv=1)
    ball(0.55, (-0.4, 2.4, -0.2), 'tree', subdiv=1)
    return finish('Tree')


def bench():
    begin()
    box_mm((-0.9, 0.42, -0.25), (0.9, 0.48, 0.25), 'wood')
    box_mm((-0.9, 0.5, 0.18), (0.9, 0.85, 0.24), 'wood')
    for x in (-0.75, 0.75):
        box_mm((x - 0.04, 0, -0.22), (x + 0.04, 0.45, 0.22), 'darkgrey')
    return finish('Bench')


def car(color):
    begin()
    box_mm((-0.85, 0.3, -2.0), (0.85, 0.85, 2.0), color, bevel=0.06)
    box_mm((-0.75, 0.85, -1.0), (0.75, 1.35, 0.9), color, bevel=0.06)
    box_mm((-0.72, 0.9, -1.02), (0.72, 1.3, -0.98), 'glass_tint')
    box_mm((-0.72, 0.9, 0.88), (0.72, 1.3, 0.92), 'glass_tint')
    for z in (-1.3, 1.3):
        for x in (-0.82, 0.82):
            cyl(0.32, 0.22, (x, 0.32, z), 'tyre', verts=10, axis='x')
    box_mm((-0.7, 0.55, -2.03), (-0.45, 0.7, -2.0), 'light')
    box_mm((0.45, 0.55, -2.03), (0.7, 0.7, -2.0), 'light')
    return finish('Car_' + color)


FIXTURES = [gondola, gondola_end, fridge, freezer, produce_table, bakery_shelf, oven, deli_counter, deli_back,
            scale_and_slicer, checkout, lane_pole]
BUILDING = [wall, wall_window, wall_post, entrance, store_sign, dock_door]
DECOR = [rack, pallet, truck, desk, office_chair, filing_cabinet, plant, whiteboard, sofa, cart_corral,
         basket_stack, tree, bench]
SIGNS = [('Sign_grocery', 'GROCERY', 'gold'), ('Sign_produce', 'PRODUCE', 'green'),
         ('Sign_dairy', 'DAIRY', 'milk'), ('Sign_frozen', 'FROZEN', 'sky'), ('Sign_household', 'HOUSEHOLD', 'purple'),
         ('Sign_bakery', 'BAKERY', 'bread_dark'), ('Sign_deli', 'DELI & MEAT', 'red')]
CAR_COLORS = ['red', 'sky', 'yellow', 'white', 'darkgrey']
