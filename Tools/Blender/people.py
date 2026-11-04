"""
Preview-only: assembles people from the C_ parts the same way the game's CharacterRig does, so the
renders show what the game will build. Keep the layout numbers in step with CharacterRig.cs.
"""
import math

import bpy
from mathutils import Matrix

import kit

HEAD_TOP = 1.76
C = Matrix(((1, 0, 0, 0), (0, 0, 1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))   # Unity <-> Blender (swap y/z)


def T(x, y, z):
    return Matrix.Translation((x, y, z))


def S(x, y=None, z=None):
    y = x if y is None else y
    z = x if z is None else z
    return Matrix.Diagonal((x, y, z, 1))


def R(x=0.0, y=0.0, z=0.0):
    """Unity Quaternion.Euler(x, y, z): Z first, then X, then Y."""
    return (Matrix.Rotation(math.radians(y), 4, 'Y') @ Matrix.Rotation(math.radians(x), 4, 'X')
            @ Matrix.Rotation(math.radians(z), 4, 'Z'))


def hexrgb(h):
    h = h.lstrip('#')
    return tuple(int(h[i:i + 2], 16) / 255 for i in (0, 2, 4))


_tints = {}


def tint(rgb):
    key = tuple(round(c, 3) for c in rgb)
    if key in _tints:
        return _tints[key]
    m = bpy.data.materials.new('T_%02x%02x%02x' % tuple(int(c * 255) for c in key))
    m.use_nodes = True
    b = m.node_tree.nodes['Principled BSDF']
    b.inputs['Base Color'].default_value = (*[kit.srgb_to_linear(c) for c in key], 1)
    b.inputs['Roughness'].default_value = 0.75
    _tints[key] = m
    return m


def parts(name):
    pre = 'C_' + name + '__'
    return [o for o in bpy.data.objects if o.name.startswith(pre) and '.' not in o.name]


def place(name, M, outfit):
    for o in parts(name):
        role = o.name.split('__')[1]
        if role == 'top' and name.startswith('Arm') and 'sleeve' in outfit:
            role = 'sleeve'
        c = o.copy()
        bpy.context.collection.objects.link(c)
        c.matrix_world = C @ M @ C
        if role != 'pal':
            col = outfit.get(role, (1, 0, 1))
            for slot in c.material_slots:
                slot.link = 'OBJECT'
                slot.material = tint(col)


def place_obj(name, M):
    src = bpy.data.objects.get(name)
    if src is None:
        return
    c = src.copy()
    bpy.context.collection.objects.link(c)
    c.matrix_world = C @ M @ C


def basket_items(names, M, trolley=False):
    """Same slots as CharacterRig.AddItem."""
    for n, name in enumerate(names):
        if trolley:
            layer, k = divmod(n, 9)
            pos = (-0.15 + (k % 3) * 0.15, -0.545 + layer * 0.12, 0.17 + (k // 3) * 0.2)
            sc = 0.9
        else:
            layer, k = divmod(n, 4)
            pos = (-0.055 if k % 2 == 0 else 0.055, -0.235 + layer * 0.07, -0.09 if k < 2 else 0.09)
            sc = 0.8
        place_obj(name, M @ T(*pos) @ R(y=(n * 37) % 60 - 30 + (n % 2) * 90) @ S(sc))


def person(o, pos, yaw=0.0):
    """o: outfit dict (see LINEUP). pos/yaw in Unity space."""
    s = o.get('height', 1.75) / HEAD_TOP
    w = o.get('width', 1.0)
    hunch = 12.0 if o.get('elderly') else 0.0
    root = T(*pos) @ R(y=yaw)
    rig = root @ S(s)
    hips = rig @ T(0, 0.82, 0)
    spine = hips @ R(x=hunch)
    body = spine @ S(w, 1, 0.6 + 0.4 * w)
    for p in [o['torso']] + o.get('wear', []):
        place(p, body, o)
    neck = spine @ T(0, 0.6, 0) @ R(x=-hunch * 0.7)
    for p in ['Head', 'Hair' + o.get('hair_style', 'Short')] + o.get('head', []):
        place(p, neck, o)

    hold = o.get('hold')
    arm_l = arm_r = R(z=-4)
    arm_r = R(z=4)
    if hold == 'Basket':
        arm_l = R(x=-8, z=-14)
    if hold == 'Cane':
        arm_r = R(x=-18, z=6)
    if hold == 'Phone':
        arm_r = R(x=-62, z=-14)
    if hold == 'Work':
        arm_l = R(x=-50, z=-6)
        arm_r = R(x=-42, z=6)
    trolley_reach = 0.0
    if hold == 'Trolley':
        sh = s * (0.82 + 0.56)
        c = max(0.0, min(1.0, (sh - 0.99) / (0.55 * s)))
        a = math.degrees(math.acos(c))
        arm_l = R(x=-a, z=6)
        arm_r = R(x=-a, z=-6)
        trolley_reach = 0.55 * s * math.sin(math.radians(a))
    for side, rot in ((-1, arm_l), (1, arm_r)):
        sh = spine @ T(side * 0.27 * w, 0.56, 0) @ rot
        place('Arm' + o.get('arms', 'Short'), sh, o)
        hand = sh @ T(0, -0.55, 0)
        if side < 0 and hold == 'Basket':
            place('Basket', hand @ rot.inverted(), o)
            basket_items(o.get('items', []), hand @ rot.inverted())
        if side > 0 and hold in ('Cane', 'Bag'):
            place(hold, hand @ rot.inverted() @ R(x=hunch * 0.0), o)
        if side > 0 and hold == 'Phone':
            place('Phone', sh @ T(0, -0.5, 0.05) @ rot.inverted() @ R(x=32), o)
    lw = 0.8 + 0.2 * w
    for side in (-1, 1):
        lg = hips @ T(side * 0.095 * w, 0, 0) @ S(lw, 1, lw)
        place(o.get('legs', 'Leg'), lg, o)
    if hold == 'Trolley':
        place('Trolley', root @ T(0, 0.99, trolley_reach + 0.06), o)
        basket_items(o.get('items', []), root @ T(0, 0.99, trolley_reach + 0.06), trolley=True)


SKIN = ['#f5d6b8', '#e6bd99', '#c79a73', '#9a6e4d', '#6b4a33', '#fae0cc']
HAIR = {'black': '#2a2420', 'brown': '#5a3a24', 'blond': '#d9b56a', 'red': '#a4492a', 'grey': '#b9b6b0',
        'white': '#e8e6e0', 'dark': '#3b2a20'}


def look(skin, hair, top, bottom, shoes, **kw):
    d = {'skin': hexrgb(SKIN[skin]), 'hair': hexrgb(HAIR[hair]), 'top': hexrgb(top), 'bottom': hexrgb(bottom),
         'shoes': hexrgb(shoes)}
    for k in ('accent', 'accent2', 'sleeve'):
        if k in kw:
            kw[k] = hexrgb(kw[k])
    d.update(kw)
    return d


CUSTOMERS = [
    look(1, 'brown', '#3f6fb5', '#2c3e5c', '#e8e4dc', torso='TorsoHoodie', arms='Long', hair_style='Short', hold='Basket'),
    look(0, 'blond', '#d97a8a', '#39424f', '#7a4f2c', torso='TorsoSweater', arms='Long', hair_style='Long',
         wear=['Skirt'], legs='LegBare', height=1.66, hold='Basket'),
    look(5, 'white', '#8a7a62', '#4a4d55', '#30333a', torso='TorsoCoat', arms='Long', hair_style='Bald',
         head=['Glasses'], height=1.7, elderly=True, hold='Cane'),
    look(2, 'black', '#5b7a3a', '#3b3f47', '#25272c', torso='TorsoJacket', accent='#e9e4d6', arms='Long',
         hair_style='Buzz', head=['Beard'], width=1.3, height=1.84, hold='Trolley'),
    look(3, 'black', '#f0c23c', '#2f4f7a', '#f4f1ea', torso='TorsoTee', arms='Short', hair_style='Curly',
         height=1.58, width=0.9),
    look(4, 'dark', '#7a3b6e', '#25272c', '#e8e4dc', torso='TorsoJacket', accent='#d9c39a', arms='Long',
         hair_style='Bun', height=1.7, hold='Trolley'),
    look(0, 'red', '#2a9d8f', '#6b5b4b', '#7a4f2c', torso='TorsoTee', arms='Short', hair_style='Pony',
         height=1.68, hold='Bag'),
    look(1, 'grey', '#9a3b3b', '#c9b48a', '#4a3a2a', torso='TorsoSweater', arms='Long', hair_style='Short',
         head=['Glasses'], width=1.25, height=1.74, elderly=True),
]

ROLE = {'cashier': '#3373e6', 'stocker': '#f28c26', 'cleaner': '#26b3a6', 'baker': '#f7f2e0', 'deli': '#cc3340',
        'am': '#8c4dbf'}
STAFF = [
    look(1, 'brown', ROLE['cashier'], '#2c3038', '#25272c', torso='TorsoPolo', arms='Short', hair_style='Pony',
         height=1.66),
    look(3, 'black', '#8e9096', '#2c3038', '#25272c', torso='TorsoPolo', arms='Short', hair_style='Buzz',
         wear=['Vest', 'VestStripes'], accent=ROLE['stocker'], height=1.8, hold='Work'),
    look(0, 'blond', ROLE['cleaner'], '#2c3038', '#25272c', torso='TorsoPolo', arms='Short', hair_style='Short',
         height=1.76),
    look(2, 'dark', ROLE['baker'], '#2c3038', '#25272c', torso='TorsoPolo', arms='Short', hair_style='Bald',
         head=['HatBaker'], wear=['Apron'], accent='#dcd6ca', height=1.72),
    look(5, 'red', '#f4f1ea', '#2c3038', '#25272c', torso='TorsoPolo', arms='Short', hair_style='Bald',
         head=['Cap'], wear=['Apron', 'ApronStripes'], accent='#7a1f2b', height=1.7),
    look(4, 'black', '#f4f1ea', '#2c3038', '#25272c', torso='TorsoShirt', accent2=ROLE['am'], arms='Long',
         hair_style='Bun', wear=['Lanyard'], height=1.71),
    look(0, 'brown', '#8e9096', '#2c3038', '#25272c', torso='TorsoPolo', arms='Short', hair_style='Long',
         wear=['Vest', 'VestStripes'], accent=ROLE['stocker'], height=1.64, hold='Phone'),
]
MANAGER = look(1, 'brown', '#f4f1ea', '#33363e', '#2a1d16', torso='TorsoShirt', accent2='#b8322a', arms='Long',
               hair_style='Short', wear=['Blazer'], accent='#3a3e47', sleeve='#3a3e47', height=1.78)


def lineup():
    kit.begin()
    kit.box_mm((-7, -0.02, -4), (18, 0.0, 6), 'offwhite')
    kit.finish('PeopleFloor')
    for i, o in enumerate(CUSTOMERS):
        person(o, (-4.2 + i * 1.2, 0, 0), (10 if i % 2 else -10))
    for i, o in enumerate(STAFF):
        person(o, (7.0 + i * 1.2, 0, 0), 0)
    person(MANAGER, (7.0 + len(STAFF) * 1.2 + 0.2, 0, 0), 0)


def render_lineup(out_dir):
    import os
    kit.setup_render((1800, 800), samples=40)
    lineup()
    kit.camera_look((0.0, 0.95, 0), yaw_deg=180, pitch_deg=8, dist=13, fov=40)
    kit.render(os.path.join(out_dir, 'people_customers.png'))
    kit.camera_look((11.8, 0.95, 0), yaw_deg=180, pitch_deg=8, dist=12, fov=40)
    kit.render(os.path.join(out_dir, 'people_staff.png'))
    kit.camera_look((-1.2, 1.0, 0), yaw_deg=200, pitch_deg=14, dist=5.2, fov=40)
    kit.render(os.path.join(out_dir, 'people_close.png'))
    kit.camera_look((0.0, 0.6, 0), yaw_deg=160, pitch_deg=55, dist=14, fov=34)
    kit.render(os.path.join(out_dir, 'people_game_view.png'))


def render_in_store(out_dir, scene_fn):
    """A few people in the preview store corner, seen from the game's camera angle."""
    import os
    import copy
    kit.setup_render((1600, 1000), samples=40)
    scene_fn()
    c0 = dict(CUSTOMERS[1], items=['Item_Pasta', 'Item_Milk', 'Item_Apples', 'Item_Coffee', 'Item_Cheese'])
    c1 = dict(CUSTOMERS[3], items=['Item_Cereal', 'Item_Bubbla_Cola', 'Item_Zing_Cola', 'Item_Toilet_Paper',
                                   'Item_Sliced_Bread', 'Item_Eggs', 'Item_Frozen_Pizza', 'Item_Bananas', 'Item_Detergent'])
    person(c0, (1.5, 0, 1.4), -90)
    person(c1, (1.5, 0, 3.2), 180)
    person(CUSTOMERS[2], (7.6, 0, 1.6), 0)
    person(STAFF[1], (4.15, 0, 1.0), -90)
    person(CUSTOMERS[6], (-1.6, 0, -1.2), 200)
    person(MANAGER, (1.6, 0, -0.6), 20)
    person(STAFF[4], (10.0, 0, 7.9), 180)
    kit.camera_look((3.5, 0.6, 2.2), yaw_deg=-15, pitch_deg=50, dist=11, fov=36)
    kit.render(os.path.join(out_dir, 'people_store.png'))
    kit.camera_look((2.0, 0.8, 1.8), yaw_deg=35, pitch_deg=30, dist=5.5, fov=40)
    kit.render(os.path.join(out_dir, 'people_aisle.png'))
