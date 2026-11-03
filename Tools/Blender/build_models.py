"""
Builds every Storemanager model, renders preview images and exports FBX files.

    python3 build_models.py [--preview] [--export] [--only NAME,NAME]

Exports go to Assets/StoreManager/Resources/Models/:
    Fixtures.fbx  shelves, fridges, counters, checkouts, walls, signs
    Products.fbx  stock rows (one shelf level of one product)
    Decor.fbx     back room, office, lobby and outdoor props
    palette.png / palette_e.png  colour + emission palette
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy  # noqa: E402

import kit  # noqa: E402
import palette  # noqa: E402,F401  (fills the palette)
import products  # noqa: E402
import fixtures  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, '..', '..'))
OUT = os.path.join(REPO, 'Assets', 'StoreManager', 'Resources', 'Models')
PREVIEW = os.path.join(HERE, 'previews')


def build_all(only=None):
    kit.reset()
    made = {'products': [], 'fixtures': [], 'decor': []}

    def run(group, fn, *args):
        o = fn(*args)
        made[group].append(o)
        o.location = (0, 0, -100)   # park out of the preview scene
        return o

    for fn in products.ALL:
        run('products', fn)
    for fn in fixtures.FIXTURES + fixtures.BUILDING:
        run('fixtures', fn)
    for name, label, color in fixtures.SIGNS:
        run('fixtures', fixtures.dept_sign, name, label, color)
    for fn in fixtures.DECOR:
        run('decor', fn)
    for c in fixtures.CAR_COLORS:
        run('decor', fixtures.car, c)
    return made


def get(name):
    return bpy.data.objects[name]


def stock_on(fixture, seg_center, levels, stock_names, tilt=0.0, pivot_y=None):
    x, z = seg_center
    for side, yaw in ((1, 0.0), (-1, 180.0)):
        for li, y in enumerate(levels):
            name = stock_names[(li + (0 if side > 0 else 1)) % len(stock_names)]
            yy = pivot_y if pivot_y is not None else y
            kit.instance(get(name), (x, yy, z), yaw, tilt)


def preview_scene():
    """A small corner of the store using the same placement rules as the game."""
    floor = kit.box_mm  # noqa
    kit.begin()
    kit.box_mm((-6, -0.05, -4), (14, 0.0, 12), 'offwhite')
    kit.finish('PreviewFloor')

    # grocery aisle: two gondolas, 4 segments each
    g = get('Gondola')
    for gx, names in ((0.0, ['Stock_Pasta', 'Stock_Cereal', 'Stock_Canned_Soup', 'Stock_Rice']),
                      (3.0, ['Stock_Snacks', 'Stock_Soft_Drinks', 'Stock_Coffee', 'Stock_Pasta'])):
        for i in range(4):
            kit.instance(g, (gx, 0, i + 0.5))
            stock_on(g, (gx, i + 0.5), fixtures.LEVELS, names[i:] + names[:i])
        kit.instance(get('GondolaEnd'), (gx, 0, 0.0))
    # fridge run
    for i in range(4):
        kit.instance(get('Fridge'), (6.0, 0, i + 0.5))
        stock_on(None, (6.0, i + 0.5), fixtures.LEVELS,
                 ['Stock_Milk', 'Stock_Yogurt', 'Stock_Cheese', 'Stock_Eggs', 'Stock_Ice_Cream', 'Stock_Frozen_Pizza'][i:] + ['Stock_Milk'])
    # produce table
    for i in range(4):
        kit.instance(get('ProduceTable'), (9.0, 0, i + 0.5))
        names = ['Stock_Apples', 'Stock_Bananas', 'Stock_Tomatoes', 'Stock_Lettuce', 'Stock_Potatoes']
        for side, yaw in ((1, 0.0), (-1, 180.0)):
            kit.instance(get(names[(i + (side < 0)) % 5]), (9.0, fixtures.PRODUCE_PIVOT[1], i + 0.5), yaw, fixtures.PRODUCE_TILT)
    # bakery shelf
    for i in range(2):
        kit.instance(get('BakeryShelf'), (12.0, 0, i + 0.5))
        stock_on(None, (12.0, i + 0.5), fixtures.BAKERY_LEVELS, ['Stock_Bread', 'Stock_Croissants', 'Stock_Cakes'])
    kit.instance(get('Oven'), (12.5, 0, 4.5))
    # deli counter along X at z = 7
    for i in range(4):
        kit.instance(get('DeliCounter'), (8.5 + i, 0, 7.0))
        kit.instance(get(['Deli_Steak', 'Deli_Sliced_Ham', 'Deli_Chicken', 'Deli_Salads'][i]), (8.5 + i, 0.76, 7.0))
        kit.instance(get('DeliBack'), (8.5 + i, 0, 9.5))
    kit.instance(get('DeliScale'), (10.0, 0, 7.3))
    # checkouts
    for i in range(3):
        cx = -3.5 + i * 3
        kit.instance(get('Checkout'), (cx + 0.5, 0, -2.0))
        kit.instance(get('LanePole'), (cx + 0.8, 0, -0.6))
    # walls and signs
    for i in range(6):
        kit.instance(get('Wall'), (-5.5 + i, 0, 11.0))
    kit.instance(get('Sign_grocery'), (1.5, 1.6, 10.85))
    kit.instance(get('Sign_produce'), (9.0, 1.6, 10.85))
    kit.instance(get('Entrance'), (-3, 0, -4))
    kit.instance(get('CartCorral'), (-5.2, 0, -3.0))
    kit.instance(get('Plant'), (0.5, 0, -3.5))


def preview_props():
    kit.begin()
    kit.box_mm((-2, -0.05, -3), (24, 0.0, 12), 'lightgrey')
    kit.finish('PreviewFloor2')
    row = ['Rack', 'Pallet', 'Desk', 'OfficeChair', 'FilingCabinet', 'Plant', 'Whiteboard', 'Sofa', 'BasketStack', 'Tree', 'Bench']
    for i, n in enumerate(row):
        kit.instance(get(n), (i * 2.0, 0, 0))
    kit.instance(get('Truck'), (2, 0, 4))
    kit.instance(get('Car_red'), (8, 0, 5))
    kit.instance(get('Car_sky'), (11, 0, 5))
    kit.instance(get('DockDoor'), (16, 0, 5))
    kit.instance(get('StoreSign'), (6, 0, 10))
    for i, (n, _, _) in enumerate(fixtures.SIGNS):
        kit.instance(get(n), (17 + (i % 2) * 3.5, 0.2 + (i // 2) * 0.9, 9))


def render_previews():
    os.makedirs(PREVIEW, exist_ok=True)
    kit.setup_render((1400, 900), samples=32)
    preview_scene()
    kit.camera_look((4.5, 0.5, 3.5), yaw_deg=-20, pitch_deg=52, dist=20, fov=34)
    kit.render(os.path.join(PREVIEW, 'store_corner.png'))
    kit.camera_look((1.5, 0.8, 1.5), yaw_deg=35, pitch_deg=35, dist=6.5, fov=40)
    kit.render(os.path.join(PREVIEW, 'aisle_close.png'))
    kit.camera_look((9.5, 0.8, 5.0), yaw_deg=-10, pitch_deg=40, dist=8.5, fov=40)
    kit.render(os.path.join(PREVIEW, 'fresh_close.png'))
    kit.camera_look((-2, 0.6, -2.5), yaw_deg=10, pitch_deg=40, dist=8, fov=40)
    kit.render(os.path.join(PREVIEW, 'checkouts.png'))


def export_all(made):
    os.makedirs(OUT, exist_ok=True)
    kit.write_palette(os.path.join(OUT, 'palette.png'), os.path.join(OUT, 'palette_e.png'))
    pal = bpy.data.materials.new('Palette')
    glass = bpy.data.materials.new('Glass')
    for group, fname in (('products', 'Products.fbx'), ('fixtures', 'Fixtures.fbx'), ('decor', 'Decor.fbx')):
        objs = made[group]
        for o in objs:
            o.location = (0, 0, 0)
            o.rotation_euler = (0, 0, 0)
        kit.export_fbx(objs, os.path.join(OUT, fname), pal, glass)
        print('exported', fname, len(objs), 'objects')


if __name__ == '__main__':
    args = sys.argv[1:]
    made = build_all()
    counts = {k: sum(len(o.data.polygons) for o in v) for k, v in made.items()}
    print('built', {k: len(v) for k, v in made.items()}, 'faces', counts)
    if '--preview' in args:
        render_previews()
    if '--export' in args:
        # previews add instances; rebuild cleanly before exporting
        if '--preview' in args:
            made = build_all()
        export_all(made)
