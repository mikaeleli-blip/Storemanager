"""Deterministic seamless albedo surfaces. One repeat = 1 x 1 metre.
Run: python Tools/Blender/textures.py (NumPy + Pillow).
No baked lighting; compatible with the game's existing Lit material.
"""
import json
from pathlib import Path
import numpy as np
from PIL import Image

OUT = Path(__file__).resolve().parents[2] / 'Assets/StoreManager/Resources/Textures'
S = 512
T = np.linspace(0, 1, S)
X, Y = np.meshgrid(T, T)


def periodic(seed, amplitude=1, count=24, max_frequency=16):
    rng = np.random.default_rng(seed)
    a = np.zeros((S, S))
    for _ in range(count):
        fx, fy = rng.integers(-max_frequency, max_frequency + 1, size=2)
        if fx == fy == 0:
            fx = 1
        a += np.sin(2 * np.pi * (fx * X + fy * Y) + rng.uniform(0, 2 * np.pi))
    return a * amplitude / np.sqrt(count / 2)


def colour(base, field):
    return np.asarray(base)[None, None, :] + field[:, :, None]


def tile_grout(n, width):
    dx = np.minimum((X * n) % 1, 1 - (X * n) % 1) / n
    dy = np.minimum((Y * n) % 1, 1 - (Y * n) % 1) / n
    return np.minimum(dx, dy) < width / 2


def store():
    # 500 mm honed porcelain with fine, low-contrast aggregate.
    a = colour((218, 215, 202), periodic(11, 1.1, max_frequency=8))
    speckle = periodic(12, 1.8, count=48, max_frequency=210)
    a += speckle[:, :, None]
    a += np.where(speckle > 2.6, -7, 0)[:, :, None]
    a[tile_grout(2, .0035)] = (166, 168, 162)
    return a


def concrete():
    a = colour((157, 162, 164), periodic(21, 1.1, count=80, max_frequency=6))
    a += periodic(22, .9, max_frequency=40)[:, :, None]
    grain = periodic(23, 1.2, count=48, max_frequency=230)
    a += grain[:, :, None]
    a += np.where(grain < -2.5, -5, 0)[:, :, None]
    return a


def carpet():
    # 500 mm blue-grey carpet tiles with alternating pile direction.
    checker = (np.floor((X % 1) * 2) + np.floor((Y % 1) * 2)) % 2
    warp = np.where(checker == 0, np.sin(X * 2 * np.pi * 190), np.sin(Y * 2 * np.pi * 190))
    a = colour((79, 94, 103), periodic(31, 1.4, max_frequency=70) + warp * 2)
    a += (checker * 3)[:, :, None]
    a[tile_grout(2, .002)] = (63, 77, 86)
    return a


def plaster():
    return colour((229, 222, 207), periodic(41, 1.1, max_frequency=70) + periodic(42, .8, max_frequency=5))


def blockwork():
    a = colour((182, 186, 184), periodic(51, 1.4, max_frequency=100))
    row = np.floor((Y % 1) * 4)
    xx = (X * 2 + (row % 2) * .5) % 1
    yy = (Y * 4) % 1
    grout = (np.minimum(xx, 1 - xx) < .009) | (np.minimum(yy, 1 - yy) < .018)
    a[grout] = (145, 152, 151)
    return a


def asphalt():
    grain = periodic(61, 2.3, count=48, max_frequency=220)
    a = colour((66, 72, 78), periodic(62, 1, max_frequency=6) + grain)
    a += np.where(grain > 4, 6, 0)[:, :, None]
    return a


def sidewalk():
    a = colour((184, 184, 174), periodic(71, 1.6, max_frequency=80) + periodic(72, 1.2, max_frequency=8))
    a[tile_grout(2, .006)] = (138, 145, 143)
    return a


SURFACES = {
    'floor_store': (store, '500 mm warm porcelain tiles', .45),
    'floor_back': (concrete, 'sealed concrete, no baked slab joints', .15),
    'floor_office': (carpet, '500 mm blue-grey carpet tiles', .05),
    'wall_plaster': (plaster, 'warm painted plaster', .12),
    'wall_block': (blockwork, '500 x 250 mm painted concrete blocks', .10),
    'asphalt': (asphalt, 'fine aggregate asphalt', .08),
    'sidewalk': (sidewalk, '500 mm concrete paving slabs', .15),
}


def build():
    OUT.mkdir(parents=True, exist_ok=True)
    manifest = {}
    for name, (fn, description, smoothness) in SURFACES.items():
        a = np.clip(fn(), 0, 255).astype(np.uint8)
        # First and last samples represent the same periodic point.
        a[-1, :, :] = a[0, :, :]
        a[:, -1, :] = a[:, 0, :]
        Image.fromarray(a).save(OUT / (name + '.png'))
        manifest[name] = dict(description=description, size_px=S, repeat_metres=[1, 1],
                              colour_space='sRGB', wrap='Repeat', smoothness=smoothness)
        assert np.array_equal(a[0], a[-1]) and np.array_equal(a[:, 0], a[:, -1])
        print('PASS seamless RGB', name, S, 'x', S)
    (OUT / 'surfaces.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')


if __name__ == '__main__':
    build()
