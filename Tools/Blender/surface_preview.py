"""Render the actual repeating albedo maps at their authored metre scale."""
import os
import sys
from pathlib import Path
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
import kit
import palette  # noqa

ROOT = Path(__file__).resolve().parents[2]
PREVIEW = Path(__file__).parent / 'previews'


def plane(name, corners, uv, texture, roughness=.7):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata([kit.bl(v) for v in corners], [], [(0, 1, 2, 3)])
    mesh.uv_layers.new()
    for i, p in enumerate(uv):
        mesh.uv_layers.active.data[i].uv = p
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes['Principled BSDF']
    bsdf.inputs['Roughness'].default_value = roughness
    tex = mat.node_tree.nodes.new('ShaderNodeTexImage')
    tex.image = bpy.data.images.load(str(ROOT / 'Assets/StoreManager/Resources/Textures' / (texture + '.png')))
    tex.extension = 'REPEAT'
    mat.node_tree.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
    mesh.materials.append(mat)
    return obj


def render():
    kit.reset()
    kit.setup_render((1600, 1000), samples=40)
    for i, (floor, wall, label) in enumerate((('floor_store', 'wall_plaster', 'SHOP / PORCELAIN'),
                                             ('floor_back', 'wall_block', 'STOCKROOM / CONCRETE'),
                                             ('floor_office', 'wall_plaster', 'OFFICE / CARPET'))):
        x = i * 3.6
        plane(floor, [(x,0,0),(x+3,0,0),(x+3,0,3),(x,0,3)],
              [(0,0),(3,0),(3,3),(0,3)], floor)
        plane(wall, [(x,0,3),(x+3,0,3),(x+3,1.85,3),(x,1.85,3)],
              [(0,0),(3,0),(3,1.85),(0,1.85)], wall)
        kit.begin()
        kit.box_mm((x-.015,-.08,-.015),(x+3.015,-.002,3.02),'trim')
        kit.box_mm((x,0,2.98),(x+3,.10,3.02),'trim')
        kit.text_mesh(label, .16, (x+1.5,.15,-.45),'navy',depth=.003)
        kit.finish('SurfaceLabel'+str(i))
    kit.camera_look((5.1,.45,1.2), yaw_deg=-12, pitch_deg=38, dist=16, ortho=12.8)
    PREVIEW.mkdir(exist_ok=True)
    kit.render(str(PREVIEW / 'surfaces.png'))
    kit.camera_look((4.9,.02,1.2), yaw_deg=0, pitch_deg=90, dist=12, ortho=11.7)
    kit.render(str(PREVIEW / 'surfaces_top.png'))


if __name__ == '__main__':
    render()
