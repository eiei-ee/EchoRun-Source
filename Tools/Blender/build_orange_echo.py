"""Author the selected Orange Echo outfit and road kit in Blender.

Generated FBXs are authored assets, never runtime geometry. The original
Mixamo armature is exported unchanged; Unity remaps clothing to that skeleton.
"""
import bpy
import bmesh
import json
import math
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/Art/OrangeEcho/Models'
SOURCE = ROOT / 'ArtSource/OrangeEcho'
OUT.mkdir(parents=True, exist_ok=True)
SOURCE.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(ROOT / 'Assets/Models/Mixamo/ExoGray/ExoGray_TPose.fbx'))
rig = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
original = [o for o in bpy.data.objects if o.type == 'MESH']
bpy.context.view_layer.update()

def material(name, rgb, roughness=.72):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*rgb, 1)
    m.use_nodes = True
    bs = m.node_tree.nodes.get('Principled BSDF')
    bs.inputs['Base Color'].default_value = (*rgb, 1)
    bs.inputs['Roughness'].default_value = roughness
    return m

orange = material('OE_Jacket_Accent', (.80, .235, .067))
navy = material('OE_Navy_Fabric', (.047, .070, .092))
ivory = material('OE_Ivory_Fabric', (.83, .80, .69))
rubber = material('OE_Rubber', (.025, .032, .039))
metal = material('OE_RelayMetal', (.16, .19, .21), .4)
amber = material('OE_RelaySignal', (.96, .52, .11), .45)
garments = []

def weights(o, fn):
    for v in o.data.vertices:
        for bone, value in fn(v.co).items():
            if value <= .0001:
                continue
            name = 'mixamorig:' + bone
            g = o.vertex_groups.get(name) or o.vertex_groups.new(name=name)
            g.add([v.index], value, 'REPLACE')
    mod = o.modifiers.new('ExistingMixamoSkeleton', 'ARMATURE')
    mod.object = rig
    garments.append(o)
    return o

def interpolate(value, a, b, name_a, name_b):
    t = max(0., min(1., (value-a)/(b-a)))
    return {name_a: 1-t, name_b: t}

def torso_weights(p):
    if p.z < 1.16:
        return interpolate(p.z, .99, 1.16, 'Hips', 'Spine')
    if p.z < 1.30:
        return interpolate(p.z, 1.16, 1.30, 'Spine', 'Spine1')
    return interpolate(p.z, 1.30, 1.42, 'Spine1', 'Spine2')

def mesh(name, verts, faces, mats, selector=None):
    data = bpy.data.meshes.new(name)
    data.from_pydata(verts, [], faces)
    data.update()
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    for m in mats:
        data.materials.append(m)
    # Recalculate winding for mirrored sleeves and roads as well.
    bm = bmesh.new(); bm.from_mesh(data)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(data); bm.free()
    for f in data.polygons:
        f.use_smooth = name.startswith('OE_')
        if selector:
            f.material_index = selector(f.center)
    return obj

def rings(name, sections, plane, mats, weight_fn, selector=None, n=20, cap=False):
    verts=[]
    for k,(center,r1,r2) in enumerate(sections):
        for j in range(n):
            a=2*math.pi*j/n
            # Small designed fullness changes in each ring suggest cloth, not tubes.
            fold=1 + (.018*math.cos(j*5+k*1.3) if k not in (0,len(sections)-1) else 0)
            v=Vector(center)
            v[plane[0]] += r1*math.cos(a)*fold
            v[plane[1]] += r2*math.sin(a)*fold
            verts.append(tuple(v))
    faces=[]
    for k in range(len(sections)-1):
        for j in range(n):
            q=k*n+j; r=k*n+(j+1)%n
            faces.append((q,r,r+n,q+n))
    if cap:
        faces.extend([tuple(reversed(range(n))),tuple(range((len(sections)-1)*n,len(sections)*n))])
    obj=mesh(name,verts,faces,mats,selector)
    return weights(obj,weight_fn)

# Main shell and articulated underlayer. Body dimensions follow the imported rig.
rings('OE_Undershirt', [((0,.015,z),rx,ry) for z,rx,ry in
      [(.94,.165,.103),(1.04,.16,.105),(1.17,.175,.112),(1.31,.20,.12),(1.43,.225,.115),(1.49,.09,.07)]],
      (0,1),[navy],torso_weights)
rings('OE_CroppedJacket', [((0,.016,z),rx,ry) for z,rx,ry in
      [(1.095,.194,.135),(1.115,.20,.139),(1.22,.205,.142),(1.34,.237,.145),(1.43,.245,.128),(1.48,.238,.113),(1.505,.225,.103),(1.53,.13,.082),(1.545,.085,.074)]],
      (0,1),[orange,navy,ivory],torso_weights,
      lambda p: 1 if (p.y < -.06 and abs(p.x)<.045) or (abs(p.x)>.17 and p.z<1.23) else 2 if p.z>1.478 else 0)
rings('OE_StandCollar', [((0,.020,z),rx,ry) for z,rx,ry in [(1.525,.096,.085),(1.57,.093,.082),(1.59,.079,.073)]],
      (0,1),[ivory],lambda p:{'Spine2':1})
rings('OE_TrouserYoke',[((0,.013,z),rx,ry) for z,rx,ry in
      [(.87,.19,.107),(.925,.204,.123),(.98,.192,.12),(1.035,.174,.112)]],
      (0,1),[navy],lambda p:{'Hips':1})

for side,sign in [('Left',1),('Right',-1)]:
    def arm_weight(p, s=side):
        return interpolate(abs(p.x),.39,.54,s+'Arm',s+'ForeArm')
    rings('OE_'+side+'Sleeve',[((sign*x,.055,1.447),ry,rz) for x,ry,rz in
          [(.12,.091,.082),(.19,.104,.097),(.26,.116,.12),(.32,.113,.117),(.405,.101,.111),(.43,.102,.109),(.46,.100,.105)]],
          (1,2),[orange,ivory,navy],arm_weight,lambda p: 1 if abs(p.x)>.414 else 2 if p.z>1.53 else 0)
    rings('OE_'+side+'UnderSleeve',[((sign*x,.055,1.447),r,r) for x,r in
          [(.415,.080),(.46,.078),(.54,.072),(.64,.06),(.732,.046)]],
          (1,2),[navy],arm_weight)
    def leg_weight(p,s=side):
        if p.z>.83:
            return interpolate(p.z,.83,1.01,s+'UpLeg','Hips')
        return interpolate(p.z,.44,.59,s+'Leg',s+'UpLeg')
    rings('OE_'+side+'Trouser',[((sign*.098,.013,z),rx,ry) for z,rx,ry in
          [(1.00,.101,.122),(.925,.115,.128),(.83,.114,.133),(.72,.111,.123),(.61,.104,.11),(.54,.102,.103),(.49,.095,.105),(.43,.094,.100),(.31,.084,.091),(.21,.073,.077),(.165,.060,.062)]],
          (0,1),[navy,orange],leg_weight,
          lambda p,sgn=sign: 1 if p.x*sgn>.166 and p.z>.31 and abs(p.y-.013)<.065 else 0)
    rings('OE_'+side+'AnkleCuff',[((sign*.098,.02,z),rx,ry) for z,rx,ry in
          [(.165,.063,.064),(.188,.067,.068),(.21,.067,.069)]],
          (0,1),[navy],lambda p,s=side:{s+'Leg':1})
    def foot_weight(p,s=side):
        return interpolate(-p.y,.07,.18,s+'Foot',s+'ToeBase')
    rings('OE_'+side+'Shoe',[((sign*.097,y,z),rx,rz) for y,z,rx,rz in
          [(.09,.09,.042,.046),(.055,.105,.064,.085),(-.015,.099,.068,.074),(-.09,.080,.076,.050),(-.17,.060,.070,.032),(-.22,.055,.045,.023)]],
          (0,2),[ivory,navy],foot_weight,lambda p:1 if p.z>.13 else 0,cap=True)
    rings('OE_'+side+'Outsole',[((sign*.097,y,.027),rx,.022) for y,rx in
          [(.093,.045),(.05,.068),(-.04,.074),(-.13,.078),(-.20,.062),(-.233,.036)]],
          (0,2),[rubber,ivory],foot_weight,lambda p: 1 if p.z>.029 else 0,cap=True)

def box(name, center, size, mat, bone=None, bevel=.015):
    bpy.ops.mesh.primitive_cube_add(size=1,location=center)
    o=bpy.context.object; o.name=name; o.dimensions=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    o.data.materials.append(mat)
    if bevel:
        mod=o.modifiers.new('AuthoredEdgeBevel','BEVEL'); mod.width=bevel; mod.segments=2
        bpy.context.view_layer.objects.active=o; bpy.ops.object.modifier_apply(modifier=mod.name)
        mod=o.modifiers.new('WeightedNormals','WEIGHTED_NORMAL')
        bpy.ops.object.modifier_apply(modifier=mod.name)
    if bone:
        bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
        weights(o,lambda p:{bone:1})
    return o

# Distinct back view: circular relay and two broad reflective bars.
box('OE_RelayBacking',(0,.159,1.374),(.192,.025,.195),ivory,'Spine2',.025)
for name,radius,depth,mat,y in [('OE_RelayRim',.075,.026,metal,.179),('OE_RelayCore',.052,.029,rubber,.191),('OE_RelayIndicator',.015,.031,amber,.206)]:
    bpy.ops.mesh.primitive_cylinder_add(vertices=32,radius=radius,depth=depth,location=(0,y,1.385),rotation=(math.pi/2,0,0))
    o=bpy.context.object;o.name=name;o.data.materials.append(mat)
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    weights(o,lambda p:{'Spine2':1})
for z in [1.215,1.262]:
    box('OE_ReflectiveBar_'+str(z),(0,.16,z),(.155,.008,.018),ivory,'Spine1',.002)
box('OE_ChestLabel',(.105,-.136,1.355),(.066,.009,.024),ivory,'Spine2',.003)

# Preserve original head and hands, remove only triangles hidden by new clothing.
for o in original:
    evaluated=o.evaluated_get(bpy.context.evaluated_depsgraph_get())
    evaluated_mesh=evaluated.to_mesh()
    posed=[o.matrix_world@v.co for v in evaluated_mesh.vertices]
    bm=bmesh.new();bm.from_mesh(o.data)
    bm.verts.ensure_lookup_table()
    remove=[]
    for f in bm.faces:
        points=[posed[v.index] for v in f.verts]
        if not (all(p.z>1.53 and abs(p.x)<.145 for p in points) or min(abs(p.x) for p in points)>.726):
            remove.append(f)
    bmesh.ops.delete(bm,geom=remove,context='FACES')
    bmesh.ops.delete(bm,geom=[v for v in bm.verts if not v.link_faces],context='VERTS')
    bm.to_mesh(o.data);bm.free()
    evaluated.to_mesh_clear()
    # These are preserved reference fragments; Unity assigns the existing materials.
    o.name='OE_Preserved_'+o.name

def export(path, objects):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:o.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'ARMATURE','MESH'},
        add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True)

# Consolidate skinned garment parts to a single renderer with shared material slots.
bpy.ops.object.select_all(action='DESELECT')
for o in garments:o.select_set(True)
bpy.context.view_layer.objects.active=garments[0]
bpy.ops.object.join()
garments=[bpy.context.object]
garments[0].name='OE_OrangeEchoClothing'
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'OrangeEchoOutfit.blend'))
export(OUT/'OrangeEchoOutfit.fbx',[rig]+garments+[o for o in original if len(o.data.polygons)>0])
stats={'outfit_vertices':sum(len(o.data.vertices) for o in garments),'outfit_triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in garments),'outfit_meshes':len(garments),'bone_count':len(rig.data.bones)}

# Road kit: same playable coordinates as TrackGeometryStandards.
bpy.ops.wm.read_factory_settings(use_empty=True)
deck=material('OE_RoadDeck',(.15,.19,.225),.85)
edge=material('OE_RoadIvory',(.72,.70,.64),.8)
trim=material('OE_RoadOrange',(.72,.205,.055),.65)
joint=material('OE_RoadJoint',(.072,.09,.105),.9)
paint=material('OE_RoadPaint',(.65,.65,.59),.9)

def road_box(name,x,z,w,length,y,height,mat,bevel=.01):
    # Blender x/y are Unity x/z; Blender z is Unity y after export.
    # Unity's FBX importer flips X; compensate in the authored mesh, not at runtime.
    return box(name,(-x,-z,y),(w,length,height),mat,bevel=bevel)

def curb_line(parts,a,b):
    dx=b[0]-a[0];dz=b[1]-a[1];length=math.hypot(dx,dz)
    count=max(1,round(length/2.5));step=length/count
    for k in range(count):
        t=(k+.5)/count;x=a[0]+dx*t;z=a[1]+dz*t
        o=road_box('Coping',x,z,.34,step-.025,.255,.31,trim if k%4==1 else edge,.025)
        o.rotation_euler.z=-math.atan2(dx,dz)
        parts.append(o)
    # Slim dark drainage channel follows the inside edge, outside the 9m corridor.

def kit(name,turn=0):
    parts=[]
    if not turn:
        parts.append(road_box('DeckPanel',0,0,11,20,-.06,.32,deck,.018))
        for z in [-7.5,-5,-2.5,0,2.5,5,7.5]:
            parts.append(road_box('ExpansionJoint',0,z,10.15,.024,.101,.003,joint,0))
        for x in [-1.5,1.5]:
            for z in [-8,-4,0,4,8]:
                parts.append(road_box('LaneDash',x,z,.09,1.05,.104,.004,paint,0))
        for s in [-1,1]:
            curb_line(parts,(s*5.23,-10),(s*5.23,10))
            parts.append(road_box('DrainStrip',s*4.97,0,.095,20,.103,.004,joint,0))
    else:
        # L-shaped surface meets entry z=0 and exit x=+/-10 at z=10.
        coords=[(-5.5,0),(5.5,0),(5.5,4.5),(10,4.5),(10,15.5),(-5.5,15.5)]
        verts=[(-x*turn,-z,y) for y in [-.22,.10] for x,z in coords]
        faces=[(6,7,8,9,10,11),(5,4,3,2,1,0)]
        for i in range(6):faces.append((i,(i+1)%6,(i+1)%6+6,i+6))
        parts.append(mesh('CornerDeck',verts,faces,[deck]))
        for z in [2.5,5,7.5,10,12.5]:
            parts.append(road_box('ExpansionJoint',turn*(2.25 if z>=5 else 0),z,15.42 if z>=5 else 10.4,.024,.103,.003,joint,0))
        for x in [-1.5,1.5]:
            for z in [1,4]:parts.append(road_box('LaneDash',x*turn,z,.09,1.05,.104,.004,paint,0))
        # Orthogonal bend cues follow lane center changes, leave corner center quiet.
        for z in [8.5,11.5]:
            for x in [7.4]:parts.append(road_box('LaneDash',x*turn,z,1.05,.09,.104,.004,paint,0))
        for a,b in [((-5.23,0),(-5.23,15.23)),((-5.23,15.23),(10,15.23)),((5.23,0),(5.23,4.77)),((5.23,4.77),(10,4.77))]:
            curb_line(parts,(a[0]*turn,a[1]),(b[0]*turn,b[1]))
    # One mesh per material, baked offline: no per-panel renderer proliferation.
    merged=[]
    groups=[[o for o in parts if o.data.materials[0]==mat] for mat in [deck,edge,trim,joint,paint]]
    for mat,group in zip([deck,edge,trim,joint,paint],groups):
        if not group:continue
        bpy.ops.object.select_all(action='DESELECT')
        for o in group:o.select_set(True)
        bpy.context.view_layer.objects.active=group[0]
        if len(group)>1:bpy.ops.object.join()
        o=bpy.context.object;o.name=name+'_'+mat.name
        bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
        merged.append(o)
    export(OUT/(name+'.fbx'),merged)
    stats[name]={'vertices':sum(len(o.data.vertices) for o in merged),'renderers':len(merged)}
    return merged

all_roads=[]
for name,turn in [('OrangeRoadStraight',0),('OrangeRoadRight',1),('OrangeRoadLeft',-1)]:
    group=kit(name,turn);all_roads.extend(group)
    for o in group:o.hide_set(True)
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'OrangeEchoRoadKit.blend'))
(SOURCE/'asset-statistics.json').write_text(json.dumps(stats,indent=2))
print('ORANGE_ECHO_ASSETS_OK',json.dumps(stats))
