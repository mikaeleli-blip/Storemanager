"""
Builds every Storemanager model, renders preview images and exports FBX files.

    python3 build_models.py [--preview] [--export] [--only NAME,NAME]

Exports go to Assets/StoreManager/Resources/Models/:
    Fixtures.fbx  shelves, fridges, counters, checkouts, walls, signs
    Products.fbx  unit meshes (U_*) the game builds shelf rows from, deli trays (Deli_*), single items (Item_*)
    StockLayout.txt  where each unit sits on a shelf row; Scripts/Sim/ShelfRows.cs  units per row (capacity)
    Decor.fbx     back room, office, lobby and outdoor props
    Characters.fbx  body parts, hair, uniforms and carried things for customers, staff and you
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
import characters  # noqa: E402
import people  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, '..', '..'))
OUT = os.path.join(REPO, 'Assets', 'StoreManager', 'Resources', 'Models')
PREVIEW = os.path.join(HERE, 'previews')


def build_all(only=None):
    kit.reset()
    made = {'products': [], 'fixtures': [], 'decor': [], 'characters': []}

    def run(group, fn, *args):
        o = fn(*args)
        made[group].append(o)
        o.location = (0, 0, -100)   # park out of the preview scene
        return o

    for o in characters.build():
        o.location = (0, 0, -100)
        made['characters'].append(o)
    if ONLY_CHARACTERS:
        return made
    products.ITEMS.clear()
    products.UNITS.clear()
    products.LAYOUT.clear()
    for fn in products.ALL:
        run('products', fn)
    for o in list(products.ITEMS) + list(products.UNITS):
        o.location = (0, 0, -100)
        made['products'].append(o)
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
                      (3.0, ['Stock_Snacks', 'Stock_Bubbla_Cola', 'Stock_Zing_Cola', 'Stock_Sliced_Bread'])):
        for i in range(4):
            kit.instance(g, (gx, 0, i + 0.5))
            stock_on(g, (gx, i + 0.5), fixtures.LEVELS, names[i:] + names[:i])
        kit.instance(get('GondolaEnd'), (gx, 0, 0.0))
    # fridge run (dairy) and a chest freezer (frozen)
    for i in range(2):
        kit.instance(get('Fridge'), (6.0, 0, i + 0.5))
        stock_on(None, (6.0, i + 0.5), fixtures.LEVELS,
                 ['Stock_Milk', 'Stock_Yogurt', 'Stock_Cheese', 'Stock_Eggs'][i:] + ['Stock_Milk', 'Stock_Yogurt'])
    for i in range(2):
        kit.instance(get('Freezer'), (6.0, 0, i + 2.5))
        stock_on(None, (6.0, i + 2.5), (fixtures.FREEZER_FLOOR,), ['Stock_Frozen_Pizza', 'Stock_Ice_Cream'][i:] + ['Stock_Frozen_Pizza'])
    # produce table
    for i in range(4):
        kit.instance(get('ProduceTable'), (9.0, 0, i + 0.5))
        names = ['Stock_Apples', 'Stock_Bananas', 'Stock_Tomatoes', 'Stock_Lettuce', 'Stock_Potatoes']
        for side, yaw in ((1, 0.0), (-1, 180.0)):
            kit.instance(get(names[(i + (side < 0)) % 5]), (9.0, fixtures.PRODUCE_PIVOT[1], i + 0.5), yaw, fixtures.PRODUCE_TILT)
    # bakery shelf
    for i in range(2):
        kit.instance(get('BakeryShelf'), (12.0, 0, i + 0.5))
        stock_on(None, (12.0, i + 0.5), fixtures.BAKERY_LEVELS, ['Stock_Fresh_Bread', 'Stock_Croissants', 'Stock_Cakes'])
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


def preview_items(names, origin_x=0.0):
    """A group of products on their own, facing the camera in two rows, like a catalogue page."""
    per_row = (len(names) + 1) // 2
    for i, n in enumerate(names):
        r, c = divmod(i, per_row)
        kit.instance(get(n), (origin_x - r * 0.42, 0, c * 0.34))
    return per_row


ITEM_PAGES_FIX = [('items_fixes', ['Item_Sliced_Bread', 'Item_Sliced_Ham', 'Item_Bubbla_Cola', 'Item_Zing_Cola'])]

ITEM_PAGES = [
    ('items_grocery', ['Item_Pasta', 'Item_Rice', 'Item_Canned_Soup', 'Item_Cereal', 'Item_Coffee', 'Item_Snacks',
                       'Item_Bubbla_Cola', 'Item_Zing_Cola', 'Item_Sliced_Bread', 'Item_Detergent', 'Item_Toilet_Paper', 'Item_Dish_Soap']),
    ('items_fresh', ['Item_Milk', 'Item_Eggs', 'Item_Cheese', 'Item_Yogurt', 'Item_Frozen_Pizza', 'Item_Ice_Cream', 'Item_Batteries',
                     'Item_Fresh_Bread', 'Item_Croissants', 'Item_Cakes', 'Item_Apples', 'Item_Tomatoes']),
    ('items_produce_deli', ['Item_Bananas', 'Item_Potatoes', 'Item_Lettuce', 'Item_Sliced_Ham', 'Item_Chicken', 'Item_Salads', 'Item_Steak']),
]


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
    kit.camera_look((3.5, 0.9, 2.0), yaw_deg=30, pitch_deg=32, dist=4.2, fov=40)
    kit.render(os.path.join(PREVIEW, 'shelves_close.png'))


def render_cold():
    """Close-ups of the fridge and the chest freezer with stock, plus the game's camera angle."""
    kit.setup_render((1400, 900), samples=48)
    preview_scene()
    kit.camera_look((6.0, 0.9, 2.0), yaw_deg=-60, pitch_deg=30, dist=4.6, fov=42)
    kit.render(os.path.join(PREVIEW, 'cold_close.png'))
    kit.camera_look((6.0, 0.5, 2.5), yaw_deg=-35, pitch_deg=55, dist=9.0, fov=34)
    kit.render(os.path.join(PREVIEW, 'cold_game_view.png'))


def render_items(pages=None):
    pages = pages or ITEM_PAGES
    kit.setup_render((1600, 900), samples=48)
    kit.begin()
    kit.box_mm((-3.0, -0.02, -1.0), (1.5, 0.0, 30.0), 'offwhite')
    kit.finish('PreviewFloor3')
    for page, (fname, names) in enumerate(pages):
        z0 = page * 8.0
        per_row = 0
        placed = []
        for i, n in enumerate(names):
            pr = (len(names) + 1) // 2
            r, c = divmod(i, pr)
            kit.instance(get(n), (-r * 0.45, 0, z0 + c * 0.34))
            per_row = pr
        centre_z = z0 + (per_row - 1) * 0.34 / 2
        kit.camera_look((-0.22, 0.07, centre_z), yaw_deg=-90, pitch_deg=30, dist=2.0 + per_row * 0.18, fov=38)
        kit.render(os.path.join(PREVIEW, fname + '.png'))


def export_all(made):
    os.makedirs(OUT, exist_ok=True)
    kit.write_palette(os.path.join(OUT, 'palette.png'), os.path.join(OUT, 'palette_e.png'))
    pal = bpy.data.materials.new('Palette')
    glass = bpy.data.materials.new('Glass')
    groups = (('products', 'Products.fbx'), ('fixtures', 'Fixtures.fbx'), ('decor', 'Decor.fbx'),
              ('characters', 'Characters.fbx'))
    if ONLY_CHARACTERS:
        groups = groups[3:]
    if ONLY_PRODUCTS:
        groups = groups[:1]
    if ONLY_FIXTURES:
        groups = groups[1:2]
    if not ONLY_CHARACTERS and not ONLY_FIXTURES:
        write_layout()
    for group, fname in groups:
        # Full Stock_ rows are only for the Blender previews; the game builds rows from the units.
        objs = [o for o in made[group] if not o.name.startswith('Stock_')]
        for o in objs:
            o.location = (0, 0, 0)
            o.rotation_euler = (0, 0, 0)
        kit.export_fbx(objs, os.path.join(OUT, fname), pal, glass)
        print('exported', fname, len(objs), 'objects')


def write_layout():
    """StockLayout.txt (unit slots per product row) and the C# capacity table that must match it."""
    lines = ['# Generated by Tools/Blender/build_models.py --export. One block per product:',
             '# P <product>|<unit meshes>|<units per row>, then one C line per column (front to back,',
             '# bottom to top): "unit x y z yaw" entries separated by ";". Row frame: see products.py.']
    counts = {}
    for product in sorted(products.LAYOUT):
        names, columns = products.LAYOUT[product]
        n = sum(len(c) for c in columns)
        counts[product] = n
        lines.append('P %s|%s|%d' % (product, ','.join(names), n))
        for col in columns:
            lines.append('C ' + ';'.join('%d %.4f %.4f %.4f %.1f' % e for e in col))
    with open(os.path.join(OUT, 'StockLayout.txt'), 'w') as f:
        f.write('\n'.join(lines) + '\n')
    cs = ['// Generated by Tools/Blender/build_models.py --export from the shelf layouts. Do not edit by hand.',
          'namespace StoreManager',
          '{',
          '    /// <summary>How many units of a product fit on one shelf row (one level of one side of one segment).</summary>',
          '    public static class ShelfRows',
          '    {',
          '        public static int For(string product)',
          '        {',
          '            switch (product)',
          '            {']
    for product in sorted(counts):
        cs.append('                case "%s": return %d;' % (product, counts[product]))
    cs += ['                default: return 0;', '            }', '        }', '    }', '}']
    path = os.path.join(REPO, 'Assets', 'StoreManager', 'Scripts', 'Sim', 'ShelfRows.cs')
    with open(path, 'w') as f:
        f.write('\n'.join(cs) + '\n')
    print('layout', counts)


ONLY_PRODUCTS = False
ONLY_FIXTURES = False
ONLY_CHARACTERS = False

if __name__ == '__main__':
    args = sys.argv[1:]
    ONLY_PRODUCTS = '--products-only' in args
    ONLY_FIXTURES = '--fixtures-only' in args
    ONLY_CHARACTERS = '--characters-only' in args
    made = build_all()
    counts = {k: sum(len(o.data.polygons) for o in v) for k, v in made.items()}
    print('built', {k: len(v) for k, v in made.items()}, 'faces', counts)
    if '--people' in args:
        os.makedirs(PREVIEW, exist_ok=True)
        people.render_lineup(PREVIEW)
        made = build_all()
    if '--people-store' in args:
        os.makedirs(PREVIEW, exist_ok=True)
        people.render_in_store(PREVIEW, preview_scene)
        made = build_all()
    if '--cold' in args:
        render_cold()
        made = build_all()
    if '--fixes' in args:
        render_items(ITEM_PAGES_FIX)
        made = build_all()
    if '--items' in args:
        render_items()
        made = build_all()
    if '--preview' in args:
        render_previews()
    if '--export' in args:
        # previews add instances; rebuild cleanly before exporting
        if '--preview' in args:
            made = build_all()
        export_all(made)
