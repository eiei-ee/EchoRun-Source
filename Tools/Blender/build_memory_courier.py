"""Author the Layered Memory courier's production garment, using the existing rig.

Blender offline only. Export one weighted mesh and the unchanged Mixamo skeleton;
Unity binds the mesh to the scene's existing bones. No runtime garment generator,
cloth simulation, new animation, or external models. Head and hands stay in Unity.
"""
import bpy
import bmesh
import json
import math
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'ArtSource/Blender/LayeredMemory'
OUTPUT = ROOT / 'Assets/Art/LayeredMemory/Models/MemoryCourier.fbx'
SOURCE.mkdir(parents=True, exist_ok=True)
OUTPUT.parent.mkdir(parents=True, exist_ok=True)
PARTS = []
SURFACE_STATS = []


def material(name, rgb, roughness=.78):
    result = bpy.data.materials.new(name)
    result.diffuse_color = (*rgb, 1)
    result.use_nodes = True
    shader = result.node_tree.nodes.get('Principled BSDF')
    shader.inputs['Base Color'].default_value = (*rgb, 1)
    shader.inputs['Roughness'].default_value = roughness
    return result


def blend(value, lo, hi, a, b):
    t = max(0., min(1., (value - lo) / (hi - lo)))
    return {a: 1-t, b: t}


def torso(p):
    if p.z < 1.16:
        return blend(p.z, .99, 1.16, 'Hips', 'Spine')
    if p.z < 1.30:
        return blend(p.z, 1.16, 1.30, 'Spine', 'Spine1')
    return blend(p.z, 1.30, 1.42, 'Spine1', 'Spine2')


def bind(obj, fn):
    for vertex in obj.data.vertices:
        for bone, value in fn(vertex.co).items():
            if value <= .0001:
                continue
            name = 'mixamorig:' + bone
            if name not in RIG.data.bones:
                raise RuntimeError('Missing original bone: ' + name)
            group = obj.vertex_groups.get(name) or obj.vertex_groups.new(name=name)
            group.add([vertex.index], value, 'REPLACE')
    modifier = obj.modifiers.new('ExistingMixamoSkeleton', 'ARMATURE')
    modifier.object = RIG
    PARTS.append(obj)
    return obj


def mesh(name, vertices, faces, materials, weights, selector=None, smooth=False):
    data = bpy.data.meshes.new(name)
    data.from_pydata(vertices, [], faces)
    data.update()
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    for mat in materials:
        data.materials.append(mat)
    bm = bmesh.new()
    bm.from_mesh(data)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(data)
    bm.free()
    for polygon in data.polygons:
        polygon.use_smooth = smooth
        if selector:
            polygon.material_index = selector(polygon.center)
    return bind(obj, weights)


def rings(name, stations, axes, materials, weights, selector=None, cap=False, n=20):
    vertices = []
    for k, (center, r1, r2) in enumerate(stations):
        for j in range(n):
            angle = j * math.tau / n
            co = Vector(center)
            # restrained sewn fullness instead of a perfectly inflated cylinder
            fold = 1 + (.009*math.cos(j*4+k*.8) if 0 < k < len(stations)-1 else 0)
            co[axes[0]] += math.cos(angle)*r1*fold
            co[axes[1]] += math.sin(angle)*r2*fold
            vertices.append(tuple(co))
    faces = []
    for k in range(len(stations)-1):
        for j in range(n):
            a, b = k*n+j, k*n+(j+1)%n
            faces.append((a, b, b+n, a+n))
    if cap:
        faces += [tuple(reversed(range(n))), tuple(range((len(stations)-1)*n, len(stations)*n))]
    return mesh(name, vertices, faces, materials, weights, selector, True)


def box(name, center, dimensions, mat, weights, bevel=.006, rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=center, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(mat)
    if bevel:
        modifier = obj.modifiers.new('SewnEdges', 'BEVEL')
        modifier.width, modifier.segments = bevel, 2
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        modifier = obj.modifiers.new('WeightedCorners', 'WEIGHTED_NORMAL')
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return bind(obj, weights)


def ribbon(name, centers, width, thickness, mat, weights):
    points = [Vector(p) for p in centers]
    vertices = []
    for i, point in enumerate(points):
        tangent = points[min(i+1, len(points)-1)] - points[max(0, i-1)]
        across = Vector((tangent.z, 0, -tangent.x)).normalized() * width/2
        for side, depth in [(-1, -1), (1, -1), (1, 1), (-1, 1)]:
            vertices.append(tuple(point + across*side + Vector((0, depth*thickness/2, 0))))
    faces = [(3, 2, 1, 0)]
    for i in range(len(points)-1):
        for j in range(4):
            a, b = i*4+j, i*4+(j+1)%4
            faces.append((a, b, b+4, a+4))
    faces.append(tuple(range((len(points)-1)*4, len(points)*4)))
    return mesh(name, vertices, faces, [mat], weights)


def continuous_surface(name, pieces, materials, weights, selector, triangle_budget):
    """Bake closed construction volumes into one connected, smoothly skinned garment.

    Voxel union and relaxation happen only in Blender. The exported runtime mesh
    has ordinary vertices/weights; no modifiers, cloth simulation or extra rig.
    """
    bpy.ops.object.select_all(action='DESELECT')
    for piece in pieces:
        PARTS.remove(piece)
        piece.modifiers.clear()
        piece.select_set(True)
    bpy.context.view_layer.objects.active = pieces[0]
    bpy.ops.object.join()
    obj = bpy.context.object
    obj.name = name
    obj.vertex_groups.clear()
    obj.data.materials.clear()
    remesh = obj.modifiers.new('ContinuousClothSurface', 'REMESH')
    remesh.mode = 'VOXEL'
    remesh.voxel_size = .007
    remesh.use_smooth_shade = True
    bpy.ops.object.modifier_apply(modifier=remesh.name)
    smooth = obj.modifiers.new('ClothRelaxation', 'SMOOTH')
    smooth.factor, smooth.iterations = .55, 4
    bpy.ops.object.modifier_apply(modifier=smooth.name)
    triangles = sum(len(face.vertices)-2 for face in obj.data.polygons)
    decimate = obj.modifiers.new('MobileSurfaceBudget', 'DECIMATE')
    decimate.ratio = min(1, triangle_budget/triangles)
    decimate.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier=decimate.name)
    for mat in materials:
        obj.data.materials.append(mat)
    obj.data.update()
    for face in obj.data.polygons:
        face.use_smooth = True
        face.material_index = selector(face.center)
    # Check actual topology rather than calling intersecting shells continuous.
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    unseen = set(bm.verts)
    components = 0
    while unseen:
        components += 1
        stack = [unseen.pop()]
        while stack:
            for edge in stack.pop().link_edges:
                for other in edge.verts:
                    if other in unseen:
                        unseen.remove(other)
                        stack.append(other)
    non_manifold = sum(not edge.is_manifold for edge in bm.edges)
    bm.free()
    if components != 1 or non_manifold:
        raise RuntimeError(f'{name}: disconnected or open cloth: {components}/{non_manifold}')
    SURFACE_STATS.append({'name':name, 'connected_components':components,
                          'non_manifold_edges':non_manifold,
                          'triangles':sum(len(p.vertices)-2 for p in obj.data.polygons)})
    return bind(obj, weights)


def softened(obj, levels=1, triangle_budget=400):
    """Bake curvature into small fabric/footwear parts, preserving skin weights."""
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    sub = obj.modifiers.new('SoftFabricProfile', 'SUBSURF')
    sub.levels = levels
    # Apply before the armature so this remains rest-pose authoring.
    bpy.ops.object.modifier_move_up(modifier=sub.name)
    bpy.ops.object.modifier_apply(modifier=sub.name)
    triangles = sum(len(face.vertices)-2 for face in obj.data.polygons)
    if triangles > triangle_budget:
        decimate = obj.modifiers.new('SoftPartBudget', 'DECIMATE')
        decimate.ratio = triangle_budget/triangles
        decimate.use_collapse_triangulate = True
        bpy.ops.object.modifier_move_up(modifier=decimate.name)
        bpy.ops.object.modifier_apply(modifier=decimate.name)
    for face in obj.data.polygons:
        face.use_smooth = True
    return obj


def garments():
    # Ribcage, waist and shoulder line are independently profiled. Rounded
    # shoulder volumes are fused into the torso instead of intersecting tubes.
    def shirt_weights(p):
        side = 'Left' if p.x >= 0 else 'Right'
        if abs(p.x) >= .245:
            return blend(abs(p.x),.39,.54,side+'Arm',side+'ForeArm')
        body = torso(p)
        arm_amount = max(0., min(1., (abs(p.x)-.13)/.115))
        arm_amount *= max(0., min(1., (p.z-1.34)/.07))
        result = {bone:value*(1-arm_amount) for bone,value in body.items()}
        result[side+'Arm'] = arm_amount
        return result

    shirt = [rings('MC_TorsoConstruction', [((0,cy,z),x,y) for z,cy,x,y in
        [(1.04,.018,.145,.084),(1.075,.018,.149,.087),(1.14,.014,.142,.086),
         (1.20,.016,.143,.089),(1.27,.019,.161,.098),(1.34,.021,.180,.103),
         (1.405,.023,.191,.095),(1.465,.028,.195,.077),(1.505,.025,.151,.063),
         (1.535,.018,.092,.055),(1.557,.017,.061,.049)]],
        (0,1),[LEMON],torso,cap=True,n=28)]
    for side, sign in [('Left',1),('Right',-1)]:
        arm = lambda p,s=side: blend(abs(p.x),.39,.54,s+'Arm',s+'ForeArm')
        shirt.append(rings('MC_'+side+'_ArmConstruction',
            [((sign*x,cy,cz),y,z) for x,cy,cz,y,z in
             [(.125,.049,1.444,.045,.044),(.175,.052,1.449,.058,.058),
              (.225,.054,1.450,.060,.057),(.285,.054,1.445,.056,.051),
              (.350,.053,1.441,.049,.046),(.395,.053,1.437,.045,.045),
              (.414,.053,1.437,.044,.044)]],
            (1,2),[LEMON],arm,cap=True,n=24))
    continuous_surface('MC_TailoredShell',shirt,[LEMON],shirt_weights,lambda p:0,2250)

    # The collar rolls outwards at the top, with a lower opening at the front.
    collar = rings('MC_FoldedScarf',[((0,.016,z),x,y) for z,x,y in
        [(1.518,.085,.074),(1.540,.080,.070),(1.565,.082,.072),
         (1.585,.089,.078),(1.596,.089,.078),(1.598,.085,.074),
         (1.578,.078,.067),(1.54,.073,.062),(1.519,.081,.070)]],
        (0,1),[LILAC],lambda p:{'Spine2':1},n=24)
    for vertex in collar.data.vertices:
        if vertex.co.z > 1.56:
            vertex.co.z += .012 * (vertex.co.y-.016)/.064
    softened(collar, triangle_budget=450)

    # One continuous trouser surface, with a shaped seat/crotch and calf bulge.
    def pants_weights(p):
        side = 'Left' if p.x >= 0 else 'Right'
        if p.z < .79:
            return blend(p.z,.44,.59,side+'Leg',side+'UpLeg')
        if abs(p.x) < .073:
            hips = max(.48,min(1.,(p.z-.78)/.22))
            left = max(0.,min(1.,(p.x+.073)/.146))
            return {'Hips':hips,'LeftUpLeg':(1-hips)*left,'RightUpLeg':(1-hips)*(1-left)}
        return blend(p.z,.82,1.005,side+'UpLeg','Hips')
    pants = [rings('MC_HipConstruction',[((0,cy,z),x,y) for z,cy,x,y in
        [(.803,.018,.025,.03),(.823,.020,.077,.061),(.861,.023,.146,.079),
         (.918,.021,.165,.094),(.972,.018,.157,.087),(1.035,.017,.143,.078),
         (1.073,.016,.142,.078)]],(0,1),[INK],pants_weights,cap=True,n=28)]
    for side,sign in [('Left',1),('Right',-1)]:
        pants.append(rings('MC_'+side+'_LegConstruction',
            [((sign*cx,cy,z),x,y) for z,cx,cy,x,y in
             [(1.01,.096,.016,.074,.078),(.929,.098,.016,.085,.089),
              (.844,.100,.018,.086,.090),(.748,.099,.012,.078,.082),
              (.654,.098,.002,.065,.071),(.570,.098,-.003,.059,.063),
              (.508,.098,.002,.059,.061),(.433,.098,.021,.063,.069),
              (.362,.098,.025,.060,.068),(.284,.098,.023,.052,.059),
              (.218,.098,.021,.044,.050),(.169,.098,.020,.041,.045)]],
            (0,1),[INK],pants_weights,cap=True,n=24))
    continuous_surface('MC_Trousers',pants,[INK],pants_weights,lambda p:0,2350)

    for side,sign in [('Left',1),('Right',-1)]:
        foot = lambda p,s=side: blend(-p.y,.07,.18,s+'Foot',s+'ToeBase')
        arm = lambda p,s=side: blend(abs(p.x),.39,.54,s+'Arm',s+'ForeArm')
        # The undersleeve is a separate sewn fabric, giving the cuff a real,
        # smooth boundary rather than colouring arbitrary decimated triangles.
        inner = rings('MC_'+side+'_UnderSleeve',
            [((sign*x,cy,cz),y,z) for x,cy,cz,y,z in
             [(.382,.053,1.437,.042,.042),(.430,.053,1.438,.044,.042),
              (.477,.054,1.441,.047,.044),(.55,.055,1.445,.041,.040),
              (.635,.055,1.447,.037,.035),(.719,.055,1.447,.035,.034),
              (.744,.055,1.447,.036,.036)]],
            (1,2),[INK],arm,cap=True,n=20)
        softened(inner, triangle_budget=400)
        rings('MC_'+side+'_SleeveHem',
            [((sign*x,.053,1.437),r,r) for x,r in
             [(.395,.045),(.401,.047),(.413,.047),(.418,.043)]],
            (1,2),[LEMON],arm,n=20)
        cuff = rings('MC_'+side+'_WristCuff',
            [((sign*x,.055,1.447),r,r) for x,r in
             [(.718,.036),(.723,.038),(.735,.039),(.743,.037)]],
            (1,2),[RUBBER],lambda p,s=side:{s+'ForeArm':1},n=16)
        softened(cuff, triangle_budget=128)
        # Quiet wrist marker and sewn tab; no square cargo pockets or knee blocks.
        box('MC_'+side+'_WristMarker',(sign*.687,.055,1.485),(.024,.029,.004),
            LILAC,lambda p,s=side:{s+'ForeArm':1},.002)
        ribbon('MC_'+side+'_PocketTab',
            [(sign*.181,-.014,.866),(sign*.181,-.015,.846)],
            .015,.003,LEMON if sign>0 else LILAC,pants_weights)
        rings('MC_'+side+'_Ankle',[((sign*.098,y,z),x,depth) for z,y,x,depth in
            [(.098,.035,.035,.039),(.14,.025,.039,.044),(.168,.02,.041,.045),
             (.184,.02,.042,.047),(.198,.02,.042,.047)]],
            (0,1),[INK],lambda p,s=side:blend(p.z,.105,.18,s+'Foot',s+'Leg'),n=16)
        shoe = rings('MC_'+side+'_Shoe',[((sign*.097,y,z),x,h) for y,z,x,h in
            [(.089,.086,.032,.04),(.069,.102,.042,.051),(.018,.092,.046,.045),
             (-.044,.082,.050,.043),(-.115,.060,.049,.029),
             (-.188,.044,.036,.019),(-.224,.041,.020,.012)]],
            (0,2),[LINING],foot,cap=True,n=16)
        softened(shoe, triangle_budget=450)
        # Preserve the contact plane, foot length and animation pivots.
        rings('MC_'+side+'_Sole',[((sign*.097,y,.023),x,.018) for y,x in
            [(.091,.032),(.065,.043),(.018,.048),(-.07,.052),(-.15,.048),(-.212,.029),(-.228,.015)]],
            (0,2),[RUBBER],foot,cap=True,n=16)
        for j in range(3):
            box('MC_'+side+'_Lace_'+str(j),(sign*.097,-.036-j*.022,.124-j*.011),
                (.059-j*.004,.006,.004),INK,foot,.0015)

    # Thin zipper follows the shaped front; no straight-edged colour panels.
    ribbon('MC_Closure',[(0,-.046,1.531),(0,-.068,1.472),(0,-.079,1.405),
        (0,-.087,1.34),(0,-.083,1.27),(0,-.076,1.20),(0,-.075,1.14),(0,-.071,1.075)],
        .006,.004,LINING,torso)


def equipment():
    # Diagonal strap follows the torso surface with shared body weights.
    ribbon('MC_FrontSling',[(-.128,-.025,1.51),(-.114,-.052,1.47),(-.09,-.075,1.41),
        (-.056,-.090,1.34),(-.02,-.088,1.27),(.02,-.08,1.20),(.06,-.072,1.14),
        (.10,-.055,1.075)],.022,.005,INK,torso)
    ribbon('MC_BackSling',[(-.128,.071,1.51),(-.111,.092,1.47),(-.086,.113,1.41),
        (-.055,.127,1.34),(-.015,.124,1.27),(.03,.110,1.20),(.07,.098,1.14),
        (.10,.088,1.075)],.024,.005,INK,torso)
    box('MC_SlingBuckle',(.063,-.075,1.15),(.029,.006,.034),METAL,torso,.003,rotation=(0,-.50,0))
    chest = lambda p:{'Spine2':1}
    # Rounded textile archive, with two inset memory strips. It follows the back
    # instead of reading as another rectangular torso stuck onto the character.
    bag = rings('MC_ArchiveBody',[((.024,y,1.377),x,z) for y,x,z in
        [(.110,.035,.053),(.116,.064,.091),(.130,.078,.102),
         (.145,.071,.094),(.154,.056,.077),(.158,.030,.045)]],
        (0,2),[INK],chest,cap=True,n=20)
    softened(bag, triangle_budget=500)
    face = rings('MC_ArchiveFace',[((.024,y,1.377),x,z) for y,x,z in
        [(.147,.052,.069),(.157,.056,.076),(.162,.047,.064),(.164,.026,.038)]],
        (0,2),[LINING],chest,cap=True,n=16)
    softened(face, triangle_budget=350)
    ribbon('MC_ArchiveFlap',[(-.028,.144,1.439),(.006,.154,1.448),
        (.045,.154,1.445),(.073,.143,1.431)],.017,.004,LEMON,chest)
    for x,z,height in [(.002,1.369,.065),(.043,1.378,.084)]:
        slot = rings('MC_MemoryGlass_'+str(x),
            [((x,.165,z+h),r,.003) for h,r in
             [(-height/2,.007),(-height*.38,.010),(height*.38,.010),(height/2,.007)]],
            (0,1),[LILAC],chest,cap=True,n=12)
        softened(slot, triangle_budget=128)
    # Soft close-fitting cap covers the dated hair shell; the face remains visible.
    head = lambda p:{'Head':1}
    crown = rings('MC_CapCrown',[((0,.004,z),x,y) for z,x,y in
        [(1.733,.083,.088),(1.759,.089,.094),(1.803,.078,.082),(1.831,.052,.061),(1.842,.013,.02)]],
        (0,1),[INK],head,cap=True,n=20)
    softened(crown)
    rings('MC_CapBand',[((0,.004,z),.085,.089) for z in [1.731,1.746]],
        (0,1),[LINING],head,n=24)
    # Curved short peak, far smaller than the old wide shoulder silhouette.
    vertices=[]
    for depth in [0,.009]:
        for distance in [0,1]:
            for j in range(11):
                x=-.085+j*.017
                y=-.041-(.087 if distance else .016)*math.sqrt(max(0,1-(x/.092)**2))
                vertices.append((x,y,1.742-.012*distance-.10*x*x+depth))
    faces=[]
    for j in range(10):
        faces.extend([(j,j+1,j+12,j+11),(j+22,j+33,j+34,j+23),
                      (j+11,j+12,j+34,j+33),(j,j+22,j+23,j+1)])
    faces.extend([(0,11,33,22),(10,32,43,21)])
    mesh('MC_CapPeak',vertices,faces,[INK],head)
    box('MC_CapRearTab',(0,.098,1.756),(.034,.009,.024),LEMON,head,.003)


def silhouette_measurements():
    """Authored cross sections in metres, independent of camera or posed bounds."""
    def extent(name, dimension, section_axis=None, station=None, left_only=False):
        data = bpy.data.objects[name].data
        if section_axis is None:
            points = [vertex.co for vertex in data.vertices]
        else:
            # Intersect edges with the plane; sparse decimated vertices are not
            # a valid cross section and can understate depth by more than half.
            points = []
            for edge in data.edges:
                a,b = [data.vertices[i].co for i in edge.vertices]
                lo,hi = sorted([a[section_axis],b[section_axis]])
                if lo <= station <= hi:
                    delta = b[section_axis]-a[section_axis]
                    if abs(delta) < 1e-8:
                        points.extend([a,b])
                    else:
                        points.append(a.lerp(b,(station-a[section_axis])/delta))
        if left_only:
            points = [p for p in points if p.x > 0]
        if not points:
            raise RuntimeError('Missing garment measurement section: ' + name)
        return round(max(p[dimension] for p in points)-min(p[dimension] for p in points), 6)

    return {
        'coat_chest_depth': extent('MC_TailoredShell', 1, 2, 1.30),
        'coat_waist_width': extent('MC_TailoredShell', 0, 2, 1.19),
        'upper_sleeve_depth': extent('MC_TailoredShell', 1, 0, .22),
        'upper_sleeve_height': extent('MC_TailoredShell', 2, 0, .22),
        'trouser_thigh_depth': extent('MC_Trousers', 1, 2, .83, True),
        'trouser_calf_width': extent('MC_Trousers', 0, 2, .31, True),
        'archive_body_depth': extent('MC_ArchiveBody', 1),
        'shoe_max_width': extent('MC_Left_Shoe', 0),
        'sole_min_height': round(min(v.co.z for v in bpy.data.objects['MC_Left_Sole'].data.vertices), 6)
    }


def export():
    # Keep useful object names in the editable .blend, joining only the export copy.
    bpy.ops.object.select_all(action='DESELECT')
    clones=[]
    for part in PARTS:
        clone=part.copy()
        clone.data=part.data.copy()
        bpy.context.collection.objects.link(clone)
        clones.append(clone)
        clone.select_set(True)
        part.hide_render=True
        part.hide_set(True)
    bpy.context.view_layer.objects.active=clones[0]
    bpy.ops.object.join()
    clothing=bpy.context.object
    clothing.name='MC_MemoryCourierClothing'
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    # Explicit UVs provide a valid tangent basis even for the shared colour shader.
    uv=clothing.data.uv_layers.new(name='GarmentUV')
    for face in clothing.data.polygons:
        axis=max(range(3),key=lambda i:abs(face.normal[i]))
        a,b=[i for i in range(3) if i!=axis]
        for index in face.loop_indices:
            co=clothing.data.vertices[clothing.data.loops[index].vertex_index].co
            uv.data[index].uv=(co[a]*3,co[b]*3)
    clothing.data.uv_layers.active=uv
    triangles=sum(len(face.vertices)-2 for face in clothing.data.polygons)
    if triangles>11000:
        raise RuntimeError('Garment triangle budget exceeded: '+str(triangles))
    bpy.ops.object.select_all(action='DESELECT')
    clothing.select_set(True)
    RIG.select_set(True)
    bpy.context.view_layer.objects.active=RIG
    bpy.ops.export_scene.fbx(filepath=str(OUTPUT),use_selection=True,
        object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=False,
        axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True)
    stats={'vertices':len(clothing.data.vertices),'triangles':triangles,
           'bones':len(RIG.data.bones),'materials':[m.name for m in clothing.data.materials],
           'source_parts':len(PARTS),'bind_skeleton':'ExoGray_TPose.fbx',
           'continuous_garment_surfaces':SURFACE_STATS,
           'measurement_method':'mesh edge intersections with exact section planes',
           'silhouette_metres':silhouette_measurements(),
           'original_rig_unchanged':all(tuple(b.matrix_local)==BONES[b.name] for b in RIG.data.bones)}
    if not stats['original_rig_unchanged']:
        raise RuntimeError('Original skeleton changed')
    (SOURCE/'courier-manifest.json').write_text(json.dumps(stats,indent=2),encoding='utf-8')
    # Source opens with the editable parts visible; it does not duplicate the export mesh.
    bpy.data.objects.remove(clothing,do_unlink=True)
    for part in PARTS:
        part.hide_render=False
        part.hide_set(False)
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'MemoryCourier.blend'))
    print('MEMORY_COURIER_EXPORTED',json.dumps(stats))


if __name__=='__main__':
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(ROOT/'Assets/Models/Mixamo/ExoGray/ExoGray_TPose.fbx'))
    RIG=next(o for o in bpy.data.objects if o.type=='ARMATURE')
    BONES={b.name:tuple(b.matrix_local) for b in RIG.data.bones}
    for obj in list(bpy.data.objects):
        if obj.type=='MESH': bpy.data.objects.remove(obj,do_unlink=True)
    LEMON=material('OE_Jacket_Accent',(.953,.906,.557))
    INK=material('OE_Navy_Fabric',(.114,.169,.263))
    LINING=material('OE_Ivory_Fabric',(.851,.804,.863))
    RUBBER=material('OE_Rubber',(.094,.082,.114))
    METAL=material('OE_RelayMetal',(.463,.416,.522),.62)
    LILAC=material('OE_RelaySignal',(.773,.604,.937),.65)
    garments()
    equipment()
    export()
