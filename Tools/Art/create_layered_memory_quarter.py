"""Authored street-front quarter for the approved Layered Memory direction.

Blender +Z is up, -Y faces the road. Exports metres to Unity (+Y up, +Z front).
This script runs offline in Blender; it never generates geometry in the player.
Only the LayeredMemory source/model family is written. No colliders or scripts.
"""
import importlib.util
import json
import math
from pathlib import Path

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "ArtSource/Blender/LayeredMemory"
MODELS = ROOT / "Assets/Art/LayeredMemory/Models"
spec = importlib.util.spec_from_file_location("city_kit", Path(__file__).with_name("create_stacked_city_kit.py"))
kit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(kit)
kit.MODEL_DIR = MODELS


def box(root, name, center, size, mat="LM_Mineral", bevel=0):
    return kit.box(root, name, center, size, mat, bevel)


def railing(root, x1, x2, y, level):
    for height in (.52, 1.06):
        box(root, "Terrace handrail", ((x1+x2)/2, y, level+height), (x2-x1, .065, .065), "LM_Ink")
    count = max(1, math.ceil((x2-x1)/1.8))
    for i in range(count+1):
        box(root, "Balustrade post", (x1+(x2-x1)*i/count, y, level+.53), (.065, .065, 1.06), "LM_Ink")


def window(root, x, y, level, width, lit=False):
    box(root, "Recessed glazing", (x, y+.23, level+1.95), (width, .05, 1.96), "LM_Window" if lit else "LM_Glass")
    for dx in (-width/2, width/2):
        box(root, "Deep window reveal", (x+dx, y+.10, level+1.96), (.095, .32, 2.12), "LM_Stone")
    box(root, "Projecting sill", (x, y-.12, level+.94), (width+.24, .48, .13), "LM_Stone")
    box(root, "Window lintel", (x, y+.07, level+3.04), (width+.22, .30, .16), "LM_Stone")
    box(root, "Window transom", (x, y+.17, level+2.45), (width, .06, .055), "LM_Ink")
    box(root, "Window mullion", (x, y+.17, level+1.95), (.065, .06, 1.96), "LM_Ink")


def house(root, name, cx, cy, width, depth, bottom, stories, mat):
    height = stories * 3.6
    # The core sits behind the recess. Front strips form actual deep openings.
    box(root, name+" core", (cx, cy+.30, bottom+height/2), (width, depth-.6, height), mat, .035)
    front = cy-depth/2
    bays = max(2, round(width/3.6))
    pitch = width/bays
    opening = min(1.8, pitch-.9)
    for story in range(stories):
        level = bottom+story*3.6
        box(root, name+" sill wall", (cx, front+.16, level+.47), (width, .32, .94), mat)
        box(root, name+" head wall", (cx, front+.16, level+3.3), (width, .32, .6), mat)
        box(root, name+" floor course", (cx, front-.06, level+.08), (width+.12, .44, .16), "LM_Stone")
        for i in range(bays):
            x = cx-width/2+pitch*(i+.5)
            window(root, x, front, level, opening, (i+story)%4 == 0)
            if i == bays-1 and story == 1:
                balcony(root, x, front, level+.82, opening+0.42)
            elif i % 2 == 0:
                canopy(root, x, front, level+3.20, opening+.34, .74)
            if (i+story) % 3 == 1:
                shutter = "LM_Pomegranate" if mat == "LM_Mineral" else "LM_Mineral"
                for side in (-1, 1):
                    box(root, "Folded timber shutter", (x+side*(opening/2+.23),front-.14,level+1.94),
                        (.32,.12,1.78), shutter)
                    for z in (level+1.28,level+2.48):
                        box(root,"Shutter strap",(x+side*(opening/2+.23),front-.212,z),(.29,.045,.045),"LM_Ink")
            # Wall piers on both sides keep the perimeter closed.
            pier = (pitch-opening)/2
            for side in (-1, 1):
                box(root, name+" window pier", (x+side*(opening/2+pier/2), front+.16, level+1.97),
                    (pier, .32, 2.06), mat)
        # Return windows are visible when passing the quarter and in turns.
        for side in (-1, 1):
            for j in range(max(2, round(depth/4))):
                yy = cy-depth/2+1.8+j*(depth-3.6)/max(1, round(depth/4)-1)
                box(root, name+" return window", (cx+side*(width/2+.012), yy, level+1.95),
                    (.024, 1.42, 1.92), "LM_Glass")
                box(root, name+" return sill", (cx+side*(width/2+.12), yy, level+.94),
                    (.34, 1.65, .14), "LM_Stone")
    top = bottom+height
    box(root, name+" roof slab", (cx, cy, top+.12), (width+.55, depth+.55, .24), "LM_Stone", .035)
    box(root, name+" roof rear parapet", (cx, cy+depth/2+.10, top+.55), (width+.4, .22, .75), mat)
    for side in (-1, 1):
        box(root, name+" roof side parapet", (cx+side*(width/2+.10), cy, top+.55), (.22, depth+.4, .75), mat)
    railing(root, cx-width/2, cx+width/2, front-.14, top+.24)
    # Pipes connect the roof to the terrace. Sparse hardware reads at run speed.
    for side in (-1,1):
        x=cx+side*(width/2-.28)
        box(root,"Rainwater downpipe",(x,front-.25,bottom+height/2),(.12,.12,height+.28),"LM_Ink")
        for z in (bottom+1.1,top-1.1):
            box(root,"Downpipe fixing",(x,front-.24,z),(.21,.22,.06),"LM_Stone")
    box(root,"Roof gutter",(cx,front-.25,top+.10),(width+.2,.22,.18),"LM_Ink")


def canopy(root,x,front,z,width,depth):
    box(root,"Shallow metal rain canopy",(x,front-depth/2,z),(width,depth,.10),"LM_Mineral")
    # The folded flashing wraps past the sheet ends. Coincident end caps on
    # different materials would shimmer when the quarter passes the camera.
    box(root,"Folded canopy edge",(x,front-depth,z-.06),(width+.05,.10,.20),"LM_Ink")
    for side in (-1,1):
        kit.beam(root,"Canopy wall bracket",(x+side*width*.35,front-.03,z-.48),
                 (x+side*width*.35,front-depth*.82,z-.08),.035,"LM_Ink")
    for i in range(max(2,round(width/.3))):
        xx=x-width/2+.16+i*.30
        box(root,"Canopy standing seam",(xx,front-depth/2,z+.057),(.024,depth,.024),"LM_Ink")


def balcony(root,x,front,z,width):
    box(root,"Balcony stone cantilever",(x,front-.68,z),(width+.25,1.55,.20),"LM_Stone")
    railing(root,x-width/2,x+width/2,front-1.37,z+.10)
    for side in (-1,1):
        box(root,"Balcony return rail",(x+side*width/2,front-.72,z+1.12),(.065,1.30,.065),"LM_Ink")
        box(root,"Balcony support corbel",(x+side*width*.32,front-.35,z-.27),(.22,.67,.37),"LM_Stone")


def lantern(root,x,y,z):
    box(root,"Lantern wall plate",(x,y+.18,z+.22),(.18,.10,.35),"LM_Ink")
    box(root,"Lantern bracket",(x,y-.06,z+.38),(.06,.52,.06),"LM_Ink")
    box(root,"Warm lantern glazing",(x,y-.26,z),(.21,.20,.32),"LM_Window")
    for side in (-1,1):
        box(root,"Lantern corner",(x+side*.13,y-.38,z),(.034,.034,.38),"LM_Ink")
    for zz in (z-.21,z+.21):
        box(root,"Lantern metal cap",(x,y-.26,zz),(.34,.31,.08),"LM_Ink")


def stair(root):
    # A two-flight external stair connects the street terrace to the first roof.
    # All solid geometry stays beyond the playable road and camera corridor.
    for flight in range(2):
        bottom = flight*3.6
        y = -8.15+flight*1.8
        direction = 1 if flight == 0 else -1
        for i in range(18):
            x = -7.55+i*.34 if direction == 1 else -1.77-i*.34
            top = bottom+(i+1)*.2
            box(root, "Stone stair tread", (x, y, top-.10), (.36, 1.65, .20), "LM_Stone")
            if i%3 == 0:
                box(root, "Stair baluster", (x, y-.78, top+.5), (.065, .065, 1), "LM_Ink")
        start = (-7.55, y-.78, bottom+.98) if direction == 1 else (-1.77, y-.78, bottom+.98)
        end = (-1.77, y-.78, bottom+4.38) if direction == 1 else (-7.55, y-.78, bottom+4.38)
        kit.beam(root, "Continuous stair handrail", start, end, .035, "LM_Ink")
        # Closed support stringer under treads, never a floating zigzag.
        a, b = (-7.75, -1.58)
        verts = [(a,y-.70,bottom-.25),(b,y-.70,bottom-.25),
                 (b,y-.70,bottom+(3.6 if direction==1 else .2)),
                 (a,y-.70,bottom+(.2 if direction==1 else 3.6))]
        verts += [(x,y+1.4,z) for x,y,z in verts]
        kit.mesh(root,"Stone stair support",verts,[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],"LM_Stone")
    box(root, "Stair mid landing", (-.9, -7.25, 3.46), (1.5, 3.46, .28), "LM_Stone")
    box(root, "Roof stair landing", (-7.55, -5.9, 7.32), (1.65, 1.8, .24), "LM_Stone")


def plant(root, x, y, level, seed):
    box(root, "Stone planter", (x,y,level+.30), (2.1,.95,.60), "LM_Stone", .035)
    box(root, "Dark planting inset", (x,y,level+.61), (1.88,.74,.04), "LM_Ink")
    for i in range(3):
        xx=x+(i-1)*.56
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=1, location=(xx,y,level+1.04))
        obj=bpy.context.object
        obj.name="Roof planting silhouette"
        obj.scale=(.63,.50,.57)
        obj.rotation_euler[2]=seed+i*.7
        kit.finish(obj,root,"LM_Leaf")
    # A small trail hangs outside a supported planter, not through the facade.
    for i in range(13):
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=1,
            location=(x+.55+math.sin(i*.85+seed)*.21,y-.58,level+.74-i*.19))
        obj=bpy.context.object
        obj.name="Trailing terrace foliage"
        obj.scale=(.23+.08*(i%2),.13,.25)
        obj.rotation_euler[1]=math.sin(i+seed)*.8
        kit.finish(obj,root,"LM_Leaf")


def arch(root, x, y, base):
    # Entry ring is a closed extruded arch, not a decal or runtime primitive.
    inner, outer, spring = 1.15, 1.42, base+1.65
    for i in range(12):
        a,b=i*math.pi/12,(i+1)*math.pi/12
        points=[(x+inner*math.cos(a),spring+inner*math.sin(a)),
                (x+outer*math.cos(a),spring+outer*math.sin(a)),
                (x+outer*math.cos(b),spring+outer*math.sin(b)),
                (x+inner*math.cos(b),spring+inner*math.sin(b))]
        verts=[(xx,yy,zz) for yy in (y-.16,y+.15) for xx,zz in points]
        kit.mesh(root,"Entry arch voussoir",verts,[(0,1,2,3),(7,6,5,4),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)],"LM_Stone")
    for side in (-1,1):
        box(root,"Entry arch pier",(x+side*1.285,y,base+.83),(.27,.32,1.66),"LM_Stone")


def quarter(gallery=False):
    root=kit.group("LayeredGalleryQuarter" if gallery else "LayeredTerraceQuarter")
    box(root,"Continuous occupied foundation",(0,0,-10.95),(18,16,21.9),"LM_Mineral",.04)
    for level in (-18,-10.8,-3.6):
        for i in range(5): window(root,-7.2+i*3.6,-8.30,level,1.8,i%4==0)
        box(root,"Foundation stone course",(0,0,level-.05),(18.35,16.35,.26),"LM_Stone")
    box(root,"Street terrace",(0,-.50,-.18),(18.6,18.2,.50),"LM_Stone",.04)
    # Two occupied setback volumes and an offset red roof house make the silhouette.
    house(root,"Street house",0,1.10,18,12.8,0,2,"LM_Pomegranate" if gallery else "LM_Mineral")
    house(root,"Terrace house",-2.6 if gallery else 2.3,2.65,11.5 if gallery else 12.4,9.7,7.2,2,
          "LM_Mineral" if gallery else "LM_Pomegranate")
    house(root,"Upper house",-4.0 if gallery else 3.9,4.55,7.7,5.9,14.4,1 if gallery else 2,
          "LM_Pomegranate" if gallery else "LM_Mineral")
    # A recessed street door and formal arched surround at the staircase end.
    box(root,"Entry darkness",(5.7,-5.43,1.53),(2.22,.045,3.06),"LM_Ink")
    box(root,"Inset double door",(5.7,-5.47,1.21),(1.88,.045,2.32),"LM_Glass")
    arch(root,5.7,-5.52,0)
    canopy(root,5.7,-5.35,3.38,3.38,1.38)
    lantern(root,4.00,-5.56,2.22)
    lantern(root,7.45,-5.56,2.22)
    if gallery:
        # A two-bay arcade occupies the free side of the street terrace.
        for x in (2.5,5.15): arch(root,x,-8.20,0)
        box(root,"Arcade roof",(3.83,-6.92,3.30),(5.55,2.9,.28),"LM_Stone")
        box(root,"Arcade fascia",(3.83,-8.25,3.36),(5.62,.22,.45),"LM_Pomegranate")
    stair(root)
    for x,y,z,seed in [(-6,-5.0,7.45,1),(-2,-5.0,7.45,2),(6,-5.0,7.45,3),
                       (-6 if gallery else -2,-1.95,14.65,4),
                       (0 if gallery else 6,-1.95,14.65,5),
                       (-4 if gallery else 4,2.25,18.25 if gallery else 21.85,6)]:
        plant(root,x,y,z,seed)
    railing(root,-8.8,8.8,-9.43,.07)
    # Tall, sparse ribs give the roofline a distinct rhythm at running distance.
    pergola_center=6.5 if gallery else -5.8
    for x in (pergola_center-1.5,pergola_center,pergola_center+1.5):
        box(root,"Roof pergola post",(x,-2.65,9),( .09,.09,3.6),"LM_Ink")
        box(root,"Roof pergola beam",(x,-.85,10.8),(.13,3.6,.16),"LM_Ink")
    box(root,"Pergola front lintel",(pergola_center,-2.65,10.8),(3.5,.13,.16),"LM_Ink")
    for y in (-2.5,-1.9,-1.3,-.7,-.1,.5):
        box(root,"Pergola shade lath",(pergola_center,y,10.91),(3.5,.14,.10),"LM_Mineral")
    tankx=-4 if gallery else 3.9
    tankz=18.7 if gallery else 22.3
    kit.cylinder(root,"Roof water tank",(tankx,4.4,tankz),.66,1.25,"LM_Stone",vertices=12)
    for z in (tankz-.55,tankz+.55):
        kit.cylinder(root,"Tank reinforcing band",(tankx,4.4,z),.69,.08,"LM_Ink",vertices=12)
    box(root,"Tank feeder pipe",(tankx+.74,4.4,tankz-.39),(.11,.11,1.65),"LM_Ink")
    return root


def initialize():
    SOURCE.mkdir(parents=True,exist_ok=True)
    MODELS.mkdir(parents=True,exist_ok=True)
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    bpy.context.preferences.filepaths.save_version=0
    for name,rgb,metal,rough in [
        ("LM_Mineral",0x587E9B,0,.82),("LM_Pomegranate",0x9B4E65,0,.88),
        ("LM_Stone",0xABAAB5,0,.76),("LM_Ink",0x293646,.22,.55),
        ("LM_Glass",0x344D67,.12,.34),("LM_Window",0xE1D4A9,0,.64),
        ("LM_Leaf",0x455E49,0,.92)]:
        kit.material(name,tuple(((rgb>>shift)&255)/255 for shift in (16,8,0)),metal,rough)


def prepare_uvs(root):
    # All faces need a non-degenerate tangent basis even though the shared
    # Unity shader projects albedo in world space. Give custom arch/support
    # meshes the same metre-scaled UV convention as the authored boxes.
    for obj in root.children_recursive:
        if obj.type != "MESH":
            continue
        mesh=obj.data
        uv=mesh.uv_layers.get("ArchitecturalUV") or mesh.uv_layers.new(name="ArchitecturalUV")
        mesh.uv_layers.active=uv
        for polygon in mesh.polygons:
            dominant=max(range(3),key=lambda axis:abs(polygon.normal[axis]))
            axes=[axis for axis in range(3) if axis != dominant]
            for loop_index in polygon.loop_indices:
                vertex=mesh.vertices[mesh.loops[loop_index].vertex_index].co
                uv.data[loop_index].uv=(vertex[axes[0]]*.25,vertex[axes[1]]*.25)
        # The FBX exporter uses the active render UV set.
        uv.active_render=True


def main():
    initialize()
    roots=[quarter(),quarter(True)]
    reports=[]
    for root in roots:
        prepare_uvs(root)
        report=kit.export(root)
        assert report["triangles"] < 22000, report
        reports.append(report)
    (SOURCE/"manifest.json").write_text(json.dumps(reports,indent=2),encoding="utf-8")
    roots[1].location.x=28
    scene=bpy.context.scene
    bpy.ops.object.camera_add(location=(52,-66,38))
    camera=bpy.context.object
    camera.rotation_euler=(Vector((14,0,3))-camera.location).to_track_quat("-Z","Y").to_euler()
    camera.data.type="ORTHO"
    camera.data.ortho_scale=64
    scene.camera=camera
    bpy.ops.object.light_add(type="SUN",location=(0,-15,30))
    bpy.context.object.rotation_euler=(math.radians(28),math.radians(-32),math.radians(-30))
    bpy.context.object.data.energy=2.0
    scene.world.color=(.28,.32,.42)
    scene.render.engine="BLENDER_EEVEE"
    scene.render.resolution_x=1500
    scene.render.resolution_y=1080
    scene.render.resolution_percentage=100
    scene.render.image_settings.file_format="PNG"
    scene.render.filepath=str(SOURCE/"quarter-preview.png")
    scene.view_settings.view_transform="AgX"
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/"LayeredTerraceQuarter.blend"))
    bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    main()
