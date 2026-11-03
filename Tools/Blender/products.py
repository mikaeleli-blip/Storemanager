"""
Stock rows: one shelf level (1 m long, one side) full of one product.

Frame (Unity space): products face +X, row runs along Z from -0.48 to 0.48,
depth from x = 0.06 to 0.42, sitting on y = 0. The game places a row on each
shelf level and rotates it 180 degrees for the other side of the fixture.

Deli rows ("Deli_*") use the counter's frame instead: along X, facing -Z.
"""
import math
import random

from kit import ball, begin, box, box_mm, cyl, finish, jitter, poly, prism

Z0, Z1 = -0.47, 0.47
X0, X1 = 0.06, 0.42


def along(width, gap=0.006):
    """Centre z positions for units of `width` filling the row."""
    n = max(1, int((Z1 - Z0 + gap) // (width + gap)))
    span = n * width + (n - 1) * gap
    start = -span / 2 + width / 2
    return [start + i * (width + gap) for i in range(n)]


def deep(depth, gap=0.01, cols=None):
    n = cols or max(1, int((X1 - X0 + gap) // (depth + gap)))
    return [X1 - depth / 2 - i * (depth + gap) for i in range(n)]


# ------------------------------------------------------------------ grocery

def pasta():
    begin()
    for x in deep(0.13):
        for z in along(0.075):
            h = 0.2 + jitter(0.005)
            box((0.12, h, 0.07), (x, h / 2, z), 'navy')
            box((0.122, 0.07, 0.072), (x, h * 0.55, z), 'yellow')
            box((0.122, 0.03, 0.072), (x, h - 0.03, z), 'white')
    return finish('Stock_Pasta')


def rice():
    begin()
    for x in deep(0.16):
        for z in along(0.12):
            h = 0.17 + jitter(0.01)
            box((0.15, h, 0.11), (x, h / 2, z), 'white', bevel=0.02)
            box((0.152, 0.04, 0.112), (x, h * 0.45, z), 'red')
    return finish('Stock_Rice')


def soup():
    begin()
    for x in deep(0.085):
        for z in along(0.085):
            for k in range(2):
                y = k * 0.105
                cyl(0.04, 0.1, (x, y + 0.05, z), 'red', verts=10)
                cyl(0.041, 0.04, (x, y + 0.035, z), 'white', verts=10)
    return finish('Stock_Canned_Soup')


def cereal():
    begin()
    cols = ['orange', 'yellow', 'red', 'purple']
    for x in deep(0.2, cols=1):
        for i, z in enumerate(along(0.08)):
            c = cols[(i // 3) % len(cols)]
            box((0.2, 0.3, 0.075), (x, 0.15, z), c)
            box((0.202, 0.08, 0.06), (x, 0.17, z), 'white')
            box((0.202, 0.04, 0.077), (x, 0.27, z), 'brand')
    return finish('Stock_Cereal')


def coffee():
    begin()
    for x in deep(0.09):
        for z in along(0.09):
            box((0.085, 0.17, 0.085), (x, 0.085, z), 'brown', bevel=0.008)
            box((0.087, 0.035, 0.087), (x, 0.11, z), 'gold')
            box((0.06, 0.02, 0.06), (x, 0.18, z), 'black')
    return finish('Stock_Coffee')


def snacks():
    begin()
    cols = ['orange', 'yellow', 'red']
    for x in deep(0.08):
        for i, z in enumerate(along(0.15)):
            c = cols[i % 3]
            h = 0.23 + jitter(0.01)
            box((0.06, h, 0.14), (x + jitter(0.01), h / 2, z), c, bevel=0.025)
            box((0.062, 0.05, 0.1), (x, h * 0.5, z), 'white', bevel=0.01)
    return finish('Stock_Snacks')


def bottle(x, z, body, cap, h=0.22, r=0.035):
    cyl(r, h * 0.7, (x, h * 0.35, z), body, verts=8)
    cyl(r, h * 0.2, (x, h * 0.8, z), body, verts=8, radius2=r * 0.4)
    cyl(r * 0.45, 0.03, (x, h * 0.93, z), cap, verts=6)
    cyl(r * 1.02, 0.06, (x, h * 0.4, z), 'white', verts=8)


def soft_drinks():
    begin()
    cols = [('green', 'red'), ('red', 'white'), ('orange', 'black')]
    for x in deep(0.075):
        for i, z in enumerate(along(0.075)):
            b, c = cols[(i // 4) % 3]
            bottle(x, z, b, c, h=0.26)
    return finish('Stock_Soft_Drinks')


# ------------------------------------------------------------------ household

def detergent():
    begin()
    for x in deep(0.13):
        for z in along(0.16):
            box((0.12, 0.28, 0.15), (x, 0.14, z), 'orange', bevel=0.02)
            box((0.122, 0.1, 0.152), (x, 0.13, z), 'white', bevel=0.01)
            cyl(0.03, 0.04, (x, 0.3, z + 0.03), 'navy', verts=8)
            box((0.03, 0.06, 0.05), (x, 0.26, z - 0.04), 'orange')
    return finish('Stock_Detergent')


def toilet_paper():
    begin()
    for x in deep(0.2, cols=1):
        for z in along(0.3):
            for ix in (-0.05, 0.05):
                for iz in (-0.1, 0.0, 0.1):
                    for iy in (0, 1):
                        cyl(0.048, 0.11, (x + ix, 0.055 + iy * 0.115, z + iz), 'white', verts=8)
            box((0.205, 0.04, 0.305), (x, 0.13, z), 'sky')
    return finish('Stock_Toilet_Paper')


def dish_soap():
    begin()
    for x in deep(0.07):
        for z in along(0.075):
            box((0.05, 0.2, 0.07), (x, 0.1, z), 'lime', bevel=0.012)
            cyl(0.012, 0.04, (x, 0.22, z), 'white', verts=6)
    return finish('Stock_Dish_Soap')


def batteries():
    begin()
    for x in deep(0.05):
        for z in along(0.09):
            box((0.03, 0.13, 0.085), (x, 0.065, z), 'black')
            box((0.032, 0.04, 0.087), (x, 0.11, z), 'gold')
    return finish('Stock_Batteries')


# ------------------------------------------------------------------ dairy & frozen

def milk():
    begin()
    for x in deep(0.08):
        for z in along(0.08):
            box((0.075, 0.18, 0.075), (x, 0.09, z), 'white')
            prism([(x - 0.0375, 0.18), (x + 0.0375, 0.18), (x, 0.215)], z - 0.0375, z + 0.0375, 'milk')
            box((0.077, 0.05, 0.077), (x, 0.12, z), 'milk')
    return finish('Stock_Milk')


def eggs():
    begin()
    for x in deep(0.12):
        for z in along(0.26):
            for k in range(3):
                box((0.11, 0.06, 0.25), (x, 0.03 + k * 0.065, z), 'egg', bevel=0.01)
                box((0.112, 0.015, 0.252), (x, 0.05 + k * 0.065, z), 'cardboard')
    return finish('Stock_Eggs')


def cheese():
    begin()
    for x in deep(0.1):
        for z in along(0.12):
            box((0.09, 0.07, 0.11), (x, 0.035, z), 'cheese', bevel=0.01)
            box((0.092, 0.02, 0.112), (x, 0.06, z), 'red')
            box((0.08, 0.06, 0.1), (x, 0.11, z), 'cheese', bevel=0.01)
    return finish('Stock_Cheese')


def yogurt():
    begin()
    cols = ['pink', 'purple', 'lime', 'yellow']
    for x in deep(0.065):
        for i, z in enumerate(along(0.065)):
            for k in range(2):
                cyl(0.03, 0.075, (x, 0.0375 + k * 0.08, z), 'white', verts=8, radius2=0.025)
                cyl(0.031, 0.008, (x, 0.079 + k * 0.08, z), cols[(i // 2) % 4], verts=8)
    return finish('Stock_Yogurt')


def frozen_pizza():
    begin()
    for x in deep(0.28, cols=1):
        for z in along(0.28):
            for k in range(5):
                box((0.27, 0.035, 0.27), (x, 0.018 + k * 0.037, z), 'pizza' if k % 2 == 0 else 'red')
                box((0.12, 0.002, 0.12), (x + 0.03, 0.037 + k * 0.037, z), 'yellow')
    return finish('Stock_Frozen_Pizza')


def ice_cream():
    begin()
    cols = ['pink', 'chocolate', 'cream', 'mint']
    for x in deep(0.12):
        for i, z in enumerate(along(0.12)):
            for k in range(2):
                cyl(0.055, 0.09, (x, 0.045 + k * 0.095, z), 'white', verts=10)
                cyl(0.056, 0.015, (x, 0.085 + k * 0.095, z), cols[i % 4], verts=10)
    return finish('Stock_Ice_Cream')


# ------------------------------------------------------------------ produce (in crates)

def pile(name, color, r, count, sub=1, squash=(1, 1, 1), second=None):
    begin()
    random.seed(hash(name) & 0xffff)
    for i in range(count):
        x = random.uniform(X0 + r, X1 - r)
        z = random.uniform(Z0 + r, Z1 - r)
        layer = random.random()
        y = r * squash[1] + layer * r * 1.6
        c = second if (second and random.random() < 0.3) else color
        ball(r, (x, y, z), c, subdiv=sub, scale=squash)
    return finish(name)


def apples():
    o = pile('Stock_Apples', 'apple', 0.04, 70, second='apple_green')
    return o


def potatoes():
    return pile('Stock_Potatoes', 'potato', 0.045, 60, squash=(1.2, 0.8, 1.0))


def tomatoes():
    return pile('Stock_Tomatoes', 'tomato', 0.035, 85)


def lettuce():
    begin()
    random.seed(7)
    for x in (0.15, 0.32):
        for z in along(0.15):
            ball(0.07, (x, 0.06, z + jitter(0.01)), 'lettuce', subdiv=1, scale=(1, 0.85, 1))
            ball(0.05, (x, 0.1, z), 'leaf', subdiv=1, scale=(1, 0.6, 1))
    return finish('Stock_Lettuce')


def bananas():
    begin()
    random.seed(3)
    for x in (0.14, 0.32):
        for z in along(0.16):
            for k in range(5):
                a = (k - 2) * 0.025
                box((0.15, 0.03, 0.03), (x, 0.03 + abs(k - 2) * 0.008, z + a), 'banana', bevel=0.01)
            box((0.03, 0.04, 0.05), (x - 0.08, 0.04, z), 'banana_tip')
    return finish('Stock_Bananas')


# ------------------------------------------------------------------ bakery

def bread():
    begin()
    for x in deep(0.12):
        for z in along(0.2):
            box((0.11, 0.09, 0.19), (x, 0.045, z), 'bread', bevel=0.03)
            box((0.06, 0.012, 0.17), (x, 0.092, z), 'bread_dark')
    return finish('Stock_Bread')


def croissants():
    begin()
    for x in deep(0.09):
        for z in along(0.11):
            for k, dz in enumerate((-0.035, 0.0, 0.035)):
                ball(0.028 if k != 1 else 0.035, (x + (0.012 if k != 1 else 0), 0.03, z + dz), 'croissant', scale=(1, 0.75, 1.1))
    return finish('Stock_Croissants')


def cakes():
    begin()
    cols = ['cake_pink', 'chocolate', 'cake_cream']
    for x in deep(0.2, cols=1):
        for i, z in enumerate(along(0.22)):
            cyl(0.1, 0.02, (x, 0.01, z), 'white', verts=12)
            cyl(0.09, 0.1, (x, 0.07, z), cols[i % 3], verts=12)
            cyl(0.092, 0.02, (x, 0.12, z), 'cake_cream', verts=12)
            ball(0.015, (x, 0.135, z), 'red')
    return finish('Stock_Cakes')


# ------------------------------------------------------------------ deli counter trays (frame along X, facing -Z)

def tray_row(name, fn):
    begin()
    for x in (-0.33, 0.0, 0.33):
        box((0.3, 0.02, 0.36), (x, 0.01, -0.03), 'white')
        fn(x)
    return finish(name)


def ham():
    def f(x):
        for i in range(4):
            for j in range(3):
                cyl(0.03, 0.08, (x - 0.1 + i * 0.065, 0.045, -0.13 + j * 0.1), 'ham', verts=8, axis='x')
    return tray_row('Deli_Sliced_Ham', f)


def chicken():
    def f(x):
        for i in range(3):
            for j in range(2):
                ball(0.06, (x - 0.08 + i * 0.08, 0.06, -0.1 + j * 0.13), 'chicken', scale=(1.2, 0.85, 1))
    return tray_row('Deli_Chicken', f)


def salads():
    def f(x):
        for i in range(2):
            for j in range(2):
                cyl(0.06, 0.05, (x - 0.07 + i * 0.14, 0.04, -0.11 + j * 0.15), 'white', verts=10)
                ball(0.05, (x - 0.07 + i * 0.14, 0.07, -0.11 + j * 0.15), 'salad', scale=(1, 0.5, 1))
    return tray_row('Deli_Salads', f)


def steak():
    def f(x):
        for i in range(2):
            for j in range(3):
                box((0.11, 0.025, 0.08), (x - 0.06 + i * 0.12, 0.033, -0.13 + j * 0.1), 'meat', bevel=0.01)
                box((0.11, 0.026, 0.015), (x - 0.06 + i * 0.12, 0.033, -0.095 + j * 0.1), 'meat_fat')
    return tray_row('Deli_Steak', f)


GROCERY = [pasta, rice, soup, cereal, coffee, snacks, soft_drinks]
HOUSEHOLD = [detergent, toilet_paper, dish_soap, batteries]
DAIRY = [milk, eggs, cheese, yogurt, frozen_pizza, ice_cream]
PRODUCE = [apples, bananas, potatoes, tomatoes, lettuce]
BAKERY = [bread, croissants, cakes]
DELI = [ham, chicken, salads, steak]
ALL = GROCERY + HOUSEHOLD + DAIRY + PRODUCE + BAKERY + DELI
