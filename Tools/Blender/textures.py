"""Floor textures for Storemanager (1 texture = 1 x 1 m). Run: python3 textures.py"""
import os
import random

from PIL import Image, ImageDraw, ImageFilter

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Assets', 'StoreManager', 'Resources', 'Textures')
S = 256


def jitter(c, r, rng):
    return tuple(max(0, min(255, v + rng.randint(-r, r))) for v in c)


def noise(img, amount, rng, blur=0.6):
    px = img.load()
    for y in range(S):
        for x in range(S):
            px[x, y] = jitter(px[x, y], amount, rng)
    return img.filter(ImageFilter.GaussianBlur(blur))


def tiles(name, base, grout, n, amount=6, var=6, seed=1):
    rng = random.Random(seed)
    img = Image.new('RGB', (S, S), grout)
    d = ImageDraw.Draw(img)
    step = S // n
    g = 2
    for i in range(n):
        for j in range(n):
            c = jitter(base, var, rng)
            d.rectangle([i * step + g, j * step + g, (i + 1) * step - g - 1, (j + 1) * step - g - 1], fill=c)
    img = noise(img, amount, rng)
    img.save(os.path.join(OUT, name + '.png'))


def concrete(name, base, seed=2, joints=True):
    rng = random.Random(seed)
    img = Image.new('RGB', (S, S), base)
    img = noise(img, 10, rng, blur=1.2)
    d = ImageDraw.Draw(img)
    for _ in range(40):
        x, y = rng.randrange(S), rng.randrange(S)
        r = rng.randint(4, 14)
        c = jitter(base, 8, rng)
        d.ellipse([x - r, y - r, x + r, y + r], fill=c)
    img = img.filter(ImageFilter.GaussianBlur(2.0))
    if joints:
        d = ImageDraw.Draw(img)
        dark = tuple(int(v * 0.82) for v in base)
        d.line([(0, 0), (S, 0)], fill=dark, width=2)
        d.line([(0, 0), (0, S)], fill=dark, width=2)
    img.save(os.path.join(OUT, name + '.png'))


def planks(name, base, seed=3):
    rng = random.Random(seed)
    img = Image.new('RGB', (S, S), base)
    d = ImageDraw.Draw(img)
    n = 5
    h = S // n
    for i in range(n):
        c = jitter(base, 14, rng)
        off = rng.randrange(S)
        d.rectangle([0, i * h, S, (i + 1) * h - 2], fill=c)
        d.line([(off, i * h), (off, (i + 1) * h - 2)], fill=tuple(int(v * 0.7) for v in base), width=2)
        for _ in range(6):
            y = i * h + rng.randrange(2, h - 3)
            d.line([(rng.randrange(S), y), (rng.randrange(S), y)], fill=jitter(c, 10, rng), width=1)
    img = noise(img, 4, rng, blur=0.5)
    img.save(os.path.join(OUT, name + '.png'))


if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    tiles('floor_store', (238, 232, 218), (200, 194, 182), 2, seed=11)
    tiles('sidewalk', (196, 192, 184), (160, 156, 150), 4, amount=8, var=8, seed=12)
    concrete('floor_back', (168, 170, 172), seed=13)
    concrete('asphalt', (88, 91, 97), seed=14, joints=False)
    planks('floor_office', (176, 128, 84), seed=15)
    print('textures written to', os.path.abspath(OUT))
