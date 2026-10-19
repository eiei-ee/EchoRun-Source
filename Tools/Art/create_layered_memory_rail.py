"""Offline authored segmental-arch railway; matches the existing 24m deck.

The Unity installer follows the unchanged CityTransitLoop path and batches
these formal meshes. Relative low point -4.35m keeps the global 13m deck
above the gameplay/camera envelope. No runtime generator or new lighting.
"""
import importlib.util
import json
import math
from pathlib import Path

import bpy
from mathutils import Vector

spec=importlib.util.spec_from_file_location("quarter",Path(__file__).with_name("create_layered_memory_quarter.py"))
quarter=importlib.util.module_from_spec(spec)
spec.loader.exec_module(quarter)
kit=quarter.kit
box=quarter.box


def prism(root,name,section,x0,x1,material):
    # A side elevation in (longitudinal y, height z), extruded across x.
    vertices=[(x,y,z) for x in (x0,x1) for y,z in section]
    n=len(section)
    faces=[tuple(reversed(range(n))),tuple(range(n,2*n))]
    faces += [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    signed_area=sum(section[i][0]*section[(i+1)%n][1]-section[(i+1)%n][0]*section[i][1] for i in range(n))
    if signed_area < 0:
        faces=[tuple(reversed(face)) for face in faces]
    return kit.mesh(root,name,vertices,faces,material)


def deck(root,length):
    box(root,"Continuous stone deck",(0,0,-.25),(7,length,.50),"LM_Mineral")
    box(root,"Ballasted track bed",(0,0,.017),(2.65,length,.034),"LM_Ink")
    for x in (-.85,.85):
        box(root,"Unchanged rail running surface",(x,0,.135),(.075,length,.050),"LM_Ink")
    for side in (-1,1):
        x=side*3.31
        box(root,"Solid parapet",(x,0,.22),(.28,length,.44),"LM_Mineral")
        box(root,"Stone coping",(x,0,.48),(.36,length,.09),"LM_Stone")
        box(root,"Maintenance walkway",(side*2.40,0,.041),(1.24,length,.082),"LM_Stone")
        box(root,"Continuous handrail",(x,0,1.04),(.06,length,.07),"LM_Ink")


def span():
    root=kit.group("LayeredArchViaduct")
    deck(root,24)
    for side in (-1,1):
        center=side*3.20
        # Shallow, load-bearing segmental arches below a level train deck.
        # The full opening remains above y=8.6 after placement at y=13.
        for i in range(20):
            a=i*math.pi/20
            b=(i+1)*math.pi/20
            inner=lambda t:(10.8*math.cos(t),-4.2+3.55*math.sin(t))
            outer=lambda t:(11.22*math.cos(t),-4.2+3.92*math.sin(t))
            ia,ib,oa,ob=inner(a),inner(b),outer(a),outer(b)
            prism(root,"Arch spandrel",[ia,ib,(ib[0],-.25),(ia[0],-.25)],center-.24,center+.24,"LM_Mineral")
            # A narrow joint is backed by masonry, not a see-through crack.
            trim=.004
            prism(root,"Arch cut stone",[inner(a+trim),inner(b-trim),outer(b-trim),outer(a+trim)],
                  center-.32,center+.32,"LM_Stone")
        for y in (-11.61,11.61):
            box(root,"Arch springing pier",(center,y,-2.30),(.72,.78,4.10),"LM_Mineral")
            box(root,"Pier capstone",(center,y,-.45),(.94,.78,.25),"LM_Stone")
            box(root,"Pier foot course",(center,y,-4.22),(.86,.78,.20),"LM_Stone")
        for y in range(-11,12,2):
            box(root,"Railing upright",(side*3.31,y,.77),(.055,.06,.58),"LM_Ink")
        # The small lamps are shared emissive material, never real point lights.
        for y in (-9,9):
            box(root,"Bridge lantern pedestal",(side*3.31,y,.77),(.18,.18,.60),"LM_Ink")
            box(root,"Bridge lantern glazing",(side*3.31,y,1.12),(.20,.20,.26),"LM_Window")
            box(root,"Bridge lantern cap",(side*3.31,y,1.30),(.28,.28,.07),"LM_Ink")
    for y in range(-11,12):
        box(root,"Concrete sleeper",(0,y,.068),(2.18,.21,.105),"LM_Stone")
    for y in (-8,0,8):
        box(root,"Under deck cross tie",(0,y,-.60),(6.2,.28,.30),"LM_Mineral")
    return root


def curve_link():
    # One metre source. Only this visual longitudinal axis is sized to the
    # existing rail path chords; authored running height and width stay fixed.
    root=kit.group("LayeredRailCurve")
    deck(root,1)
    for side in (-1,1):
        box(root,"Curve girder",(side*2.8,0,-.85),(.4,1,1.20),"LM_Mineral")
        box(root,"Curve upright",(side*3.31,0,.77),(.055,.04,.58),"LM_Ink")
    return root


def main():
    quarter.initialize()
    roots=[span(),curve_link()]
    report=[]
    for root in roots:
        quarter.prepare_uvs(root)
        entry=kit.export(root)
        assert entry["triangles"] < 5000,entry
        report.append(entry)
    source=quarter.SOURCE
    (source/"rail-manifest.json").write_text(json.dumps(report,indent=2),encoding="utf-8")
    roots[1].location.x=14
    scene=bpy.context.scene
    bpy.ops.object.camera_add(location=(31,-37,15))
    camera=bpy.context.object
    camera.rotation_euler=(Vector((0,0,-1))-camera.location).to_track_quat("-Z","Y").to_euler()
    camera.data.type="ORTHO"
    camera.data.ortho_scale=33
    scene.camera=camera
    bpy.ops.object.light_add(type="SUN",location=(0,-15,30))
    bpy.context.object.rotation_euler=(.6,-.45,-.65)
    bpy.context.object.data.energy=2
    scene.world.color=(.28,.32,.42)
    scene.render.engine="BLENDER_EEVEE"
    scene.render.resolution_x=1400
    scene.render.resolution_y=900
    scene.render.resolution_percentage=100
    scene.render.image_settings.file_format="PNG"
    scene.render.filepath=str(source/"rail-preview.png")
    scene.view_settings.view_transform="AgX"
    bpy.ops.wm.save_as_mainfile(filepath=str(source/"LayeredArchViaduct.blend"))
    bpy.ops.render.render(write_still=True)


if __name__=="__main__": main()
