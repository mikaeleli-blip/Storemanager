"""Geometry checks for modular building seams; run inside Blender/bpy.

Checks source meshes and the wall endpoints needed by the placement handoff.
No Unity or simulation code is modified.
"""
import os
import sys
from math import isclose

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import kit
import palette  # noqa: F401
import fixtures


def bounds(obj):
    # Local mesh data is Blender x,z,y; origin must remain at the modelled zero.
    vertices = [(v.co.x, v.co.z, v.co.y) for v in obj.data.vertices]
    return tuple(min(v[i] for v in vertices) for i in range(3)), tuple(
        max(v[i] for v in vertices) for i in range(3))


def near(actual, expected):
    assert isclose(actual, expected, abs_tol=0.00001), (actual, expected)


def check():
    kit.reset()
    for fn, width in ((fixtures.wall, 1.0), (fixtures.wall_half, 0.5),
                      (fixtures.wall_window, 1.0)):
        obj = fn()
        lo, hi = bounds(obj)
        near(lo[0], -width / 2)
        near(hi[0], width / 2)
        near(lo[1], 0)
        near(hi[1], fixtures.WALL_TOP)
        assert all(abs(v) < 0.00001 for v in obj.location), obj.name
        # Consecutive modules must touch, without the old 40 mm cap overlap.
        near(hi[0], width + lo[0])
        assert len(obj.data.polygons) > 0
        print('PASS', obj.name, lo, hi)

    lo, hi = bounds(fixtures.wall_post())
    near(hi[1], fixtures.WALL_TOP + 0.02)
    lo, hi = bounds(fixtures.entrance())
    near(lo[0], -2)
    near(hi[0], 2)
    near(lo[1], 0)
    # Existing runtime calibration and shelf contract must still hold.
    lo, hi = bounds(fixtures.gondola())
    near(hi[1] - lo[1], 1.86)
    lo, hi = bounds(fixtures.checkout())
    assert hi[0] > -lo[0], 'Checkout must protrude toward +X'
    assert fixtures.LEVELS == (0.12, 0.52, 0.92, 1.32)

    # Proposed half segments meet the current full segments and cross-wall centres.
    for start, stop, centres in ((3.5, 34.5, [3.75] + [z + 0.5 for z in range(4, 34)] + [34.25]),
                                  (26.5, 30.0, [26.75, 27.5, 28.5, 29.5]),
                                  (31.0, 34.5, [31.5, 32.5, 33.5, 34.25])):
        cursor = start
        for c in centres:
            half_length = 0.25 if c in (3.75, 26.75, 34.25) else 0.5
            near(c - half_length, cursor)
            cursor = c + half_length
        near(cursor, stop)
    print('PASS building seams, doorway clearances, origins and fixture calibration')


if __name__ == '__main__':
    check()
