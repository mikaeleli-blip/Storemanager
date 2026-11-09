"""Building previews using StoreBuilder's placement coordinates (Unity metres).

Run with Blender --background --python Tools/Blender/building_preview.py.
The ordinary asset build also exposes these views through --walls.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy
import kit
import palette  # noqa: F401
import fixtures


def render_building(directory, suffix='', aligned=False):
    os.makedirs(directory, exist_ok=True)
    kit.setup_render((1500, 900), samples=32)
    kit.begin()
    kit.box_mm((-2, -0.10, 0), (14, -0.04, 11), 'offwhite')
    kit.box_mm((0, -0.04, 3), (12, 0, 10), 'wall')
    kit.finish('BuildingPreviewFloor')

    def put(name, x, z, yaw=0):
        return kit.instance(bpy.data.objects[name], (x, 0, z), yaw)

    # Same frontage and corner centres as StoreBuilder.BuildStaticModels.
    for x in range(12):
        if not 2 <= x <= 5:
            put('WallWindow', x + 0.5, 3.5)
    put('Entrance', 4, 3.5)
    for z in range(4 if aligned else 3, 10):
        put('Wall', -0.15, z + 0.5, 90)
        put('Wall', 12.15, z + 0.5, 90)
    if aligned:
        # Proposed StoreBuilder change, NOT yet used by the game. See HANDOFF.md.
        put('WallHalf', -0.15, 3.75, 90)
        put('WallHalf', 12.15, 3.75, 90)
    put('WallPost', -0.15, 3.5)
    put('WallPost', 12.15, 3.5)

    kit.camera_look((5.9, 0.9, 4.5), yaw_deg=-18, pitch_deg=28,
                    dist=19, ortho=16)
    kit.render(os.path.join(directory, 'building_front' + suffix + '.png'))
    kit.camera_look((0.35, 0.8, 4.0), yaw_deg=-40, pitch_deg=42,
                    dist=7, ortho=4.5)
    kit.render(os.path.join(directory, 'building_corner' + suffix + '.png'))


if __name__ == '__main__':
    kit.reset()
    for fn in fixtures.BUILDING:
        obj = fn()
        obj.location = (0, 0, -100)
    suffix = '_before' if '--before' in sys.argv else '_aligned' if '--aligned' in sys.argv else ''
    render_building(os.path.join(os.path.dirname(__file__), 'previews'), suffix,
                    aligned='--aligned' in sys.argv)
