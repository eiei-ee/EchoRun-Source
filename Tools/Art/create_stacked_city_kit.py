"""Author and export the reusable Stacked City kit in metres.

Run with Blender --background --python Tools/Art/create_stacked_city_kit.py.
Blender +Z is height and -Y is the train's nose; FBX uses -Z forward / Y up.
These are formal static art meshes. Gameplay colliders and traffic are Unity-owned.
"""
import json
import math
from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[2]
MODEL_DIR = ROOT / "Assets/Art/StackedCity/Models"
SOURCE_DIR = ROOT / "ArtSource/Blender"
BLEND_PATH = SOURCE_DIR / "StackedCityKit.blend"
MATERIALS = {}


def material(name, color, metallic=0.0, roughness=0.5, emission=0.0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    mat.diffuse_color = (*color, 1.0)
    node = mat.node_tree.nodes.get("Principled BSDF")
    node.inputs["Base Color"].default_value = (*color, 1.0)
    node.inputs["Metallic"].default_value = metallic
    node.inputs["Roughness"].default_value = roughness
    if emission:
        node.inputs["Emission Color"].default_value = (*color, 1.0)
        node.inputs["Emission Strength"].default_value = emission
    MATERIALS[name] = mat
    return mat


def group(name):
    obj = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(obj)
    return obj


def finish(obj, parent, mat, bevel=0.0):
    obj.parent = parent
    obj.data.materials.append(MATERIALS[mat])
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    if bevel:
        mod = obj.modifiers.new("Authored edge bevel", "BEVEL")
        mod.width = bevel
        mod.segments = 2
        bpy.ops.object.modifier_apply(modifier=mod.name)
    obj.select_set(False)
    return obj


def box(parent, name, center, size, mat="SC_Concrete", bevel=0.025, rotation=None):
    bpy.ops.mesh.primitive_cube_add(location=center)
    obj = bpy.context.object
    obj.name = name
    obj.scale = tuple(v / 2 for v in size)
    if rotation:
        obj.rotation_euler = rotation
    return finish(obj, parent, mat, bevel)


def cylinder(parent, name, center, radius, depth, mat="SC_Metal", axis=None, vertices=12):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=center)
    obj = bpy.context.object
    obj.name = name
    if axis:
        obj.rotation_euler = Vector(axis).to_track_quat("Z", "Y").to_euler()
    return finish(obj, parent, mat, 0.02)


def beam(parent, name, start, end, radius=0.06, mat="SC_Metal"):
    delta = Vector(end) - Vector(start)
    return cylinder(parent, name, (Vector(end) + Vector(start)) / 2, radius, delta.length, mat, delta)


def mesh(parent, name, vertices, faces, mat="SC_Concrete", bevel=0.0):
    data = bpy.data.meshes.new(name)
    data.from_pydata(vertices, [], faces)
    data.update()
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    obj.select_set(True)
    return finish(obj, parent, mat, bevel)


def panel(parent, name, corners, mat, thickness=0.016):
    obj = mesh(parent, name, corners, [tuple(range(len(corners)))], mat)
    bpy.context.view_layer.objects.active = obj
    mod = obj.modifiers.new("Panel thickness", "SOLIDIFY")
    mod.thickness = thickness
    bpy.ops.object.modifier_apply(modifier=mod.name)
    return obj


def merge_materials(root):
    groups = {}
    for obj in list(root.children_recursive):
        if obj.type == "MESH":
            groups.setdefault(obj.data.materials[0].name, []).append(obj)
    for name, objects in groups.items():
        bpy.ops.object.select_all(action="DESELECT")
        for obj in objects:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = objects[0]
        if len(objects) > 1:
            bpy.ops.object.join()
        joined = bpy.context.object
        joined.name = root.name + "_" + name
        bpy.context.scene.cursor.location = (0, 0, 0)
        bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
        joined.select_set(False)


def train():
    root = group("SkyTrainCar")
    # Rounded sections taper into the front cab. The rear is flatter for a coupler.
    rings = [(-7.0, .72, .76, 1.42), (-6.36, 1.20, .62, 2.48),
             (-5.82, 1.30, .61, 2.70), (5.94, 1.30, .61, 2.70),
             (6.70, 1.13, .68, 2.51)]
    vertices = []
    for y, half, bottom, top in rings:
        b = .19
        vertices.extend([(-half + b, y, bottom), (half - b, y, bottom),
                         (half, y, bottom + b), (half, y, top - .30),
                         (half - .09, y, top - .12), (half - .29, y, top),
                         (-half + .29, y, top), (-half + .09, y, top - .12),
                         (-half, y, top - .30), (-half, y, bottom + b)])
    faces = [tuple(range(10))]
    for ring in range(len(rings) - 1):
        for j in range(10):
            a, b = ring * 10 + j, ring * 10 + (j + 1) % 10
            faces.append((a, a + 10, b + 10, b))
    faces.append(tuple(reversed(range(40, 50))))
    mesh(root, "Sculpted silver train shell", vertices, faces, bevel=.045)
    box(root, "Undercarriage", (0, .08, .53), (1.95, 11.6, .35), "SC_Metal", .12)
    for y in (-4.5, 4.5):
        box(root, "Bogie", (0, y, .29), (1.75, 1.75, .33), "SC_Metal", .1)
        for wheel_y in (y - .55, y + .55):
            for x in (-.94, .94):
                cylinder(root, "Wheel", (x, wheel_y, .28), .28, .24, axis=(1, 0, 0), vertices=16)
                cylinder(root, "Wheel hub", (x * 1.13, wheel_y, .28), .105, .028, "SC_Concrete", (1, 0, 0))
    for side in (-1, 1):
        x = side * 1.307
        box(root, "Continuous recessed glazing", (x, .04, 1.92), (.025, 11.54, .82), "SC_Glass", .012)
        box(root, "Azure route stripe", (side * 1.314, 0, 1.38), (.022, 11.62, .07), "SC_Cyan", .009)
        box(root, "Passenger cabin warm light", (side * 1.324, .04, 2.285), (.018, 11.20, .035), "SC_Warm", .006)
        for y in (-5.58, -4.70, -2.42, -1.2, 0, 1.2, 2.42, 4.70, 5.58):
            box(root, "Window mullion", (side * 1.330, y, 1.94), (.035, .10, .90), "SC_Metal", .009)
        for y in (-3.45, 3.45):
            box(root, "Door portal", (side * 1.327, y, 1.53), (.045, 1.24, 1.71), "SC_Metal", .018)
            for d in (-1, 1):
                box(root, "Sliding door", (side * 1.354, y + d * .301, 1.52), (.025, .574, 1.60), "SC_Concrete", .012)
                box(root, "Door glazing", (side * 1.369, y + d * .301, 1.92), (.019, .445, .71), "SC_Glass", .016)
                box(root, "Door handle", (side * 1.384, y + d * .052, 1.30), (.028, .025, .25), "SC_Metal", .005)
                box(root, "Door stripe", (side * 1.372, y + d * .301, 1.38), (.02, .562, .07), "SC_Cyan", .008)
            box(root, "Door status lamp", (side * 1.374, y, 2.39), (.02, .16, .048), "SC_Cyan", .009)
        box(root, "Lower skirt", (side * 1.235, .1, .76), (.11, 10.8, .13), "SC_Metal", .025)
    # Sloped windshield follows the forward cab taper and is visible from above.
    panel(root, "Cab windshield", [(-.53, -6.99, 1.47), (.53, -6.99, 1.47),
          (.88, -6.38, 2.49), (-.88, -6.38, 2.49)], "SC_Glass", .025)
    beam(root, "Windshield centre pillar", (0, -7.005, 1.49), (0, -6.395, 2.51), .025)
    for side in (-1, 1):
        box(root, "Forward headlamp", (side * .50, -7.00, 1.07), (.35, .045, .095), "SC_Warm", .035)
        box(root, "Cab side accent", (side * 1.175, -6.14, 1.28), (.04, .55, .07), "SC_Cyan", .012)
        box(root, "Rear light", (side * .68, 6.726, 1.34), (.18, .028, .065), "SC_Warm", .016)
    box(root, "Rear vestibule glazing", (0, 6.713, 1.93), (1.20, .028, .72), "SC_Glass", .025)
    box(root, "Rear coupler", (0, 6.85, .56), (.46, .30, .17), "SC_Metal", .04)
    for y in (-2.50, 2.50):
        box(root, "Roof climate unit", (0, y, 2.79), (1.42, 2.4, .24), "SC_Concrete", .105)
        for dy in (-.82, -.41, 0, .41, .82):
            box(root, "Vent grille", (0, y + dy, 2.92), (1.00, .065, .025), "SC_Metal", .006)
    box(root, "Roof centre equipment", (0, .08, 2.81), (.70, 1.18, .24), "SC_Metal", .06)
    return root


def foliage(root, name, center, size, seed):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=1, location=center)
    obj = bpy.context.object
    obj.name = name
    # Deterministic, lightly irregular crown; no billboard dependencies.
    for vert in obj.data.vertices:
        factor = 1 + .10 * math.sin(vert.index * 2.731 + seed)
        vert.co *= factor
    obj.scale = size
    obj.rotation_euler[2] = seed
    return finish(obj, root, "SC_Foliage")


def planter():
    root = group("SkyPlanter")
    box(root, "Recessed planter foot", (0, 0, .14), (4.65, 1.67, .28), "SC_Metal", .075)
    box(root, "Concrete planting trough", (0, 0, .47), (5.0, 2.0, .74), bevel=.10)
    box(root, "Dark soil inset", (0, 0, .85), (4.69, 1.69, .05), "SC_Metal", .05)
    for side in (-1, 1):
        box(root, "Thick planter rim", (0, side * .94, .87), (5.0, .12, .14), bevel=.025)
        box(root, "Recessed planter strip", (0, side * 1.006, .52), (3.8, .012, .037), "SC_Warm", .005)
    for x, h, seed in [(-1.40, 2.14, .2), (.14, 2.26, 1.7), (1.60, 1.87, 2.9)]:
        beam(root, "Branching tree trunk", (x, 0, .86), (x + .05, 0, h), .09)
        beam(root, "Tree branch", (x, 0, 1.47), (x - .29, -.30, h + .16), .046)
        beam(root, "Tree branch", (x, 0, 1.68), (x + .35, .22, h + .10), .043)
        foliage(root, "Sculpted green crown", (x, 0, h + .13), (.80, .70, .52), seed)
        foliage(root, "Crown branch mass", (x - .33, -.18, h - .04), (.57, .51, .44), seed + 2)
    for x in (-1.95, -.73, .70, 1.94):
        foliage(root, "Low planting", (x, .48, 1.03), (.43, .38, .25), x + 4)
    return root


def rail(root, start, end, name="Balustrade", glass=True):
    a, b = Vector(start), Vector(end)
    length = (b - a).length
    direction = (b - a) / length
    # Endpoints lie at the top of the deck.
    beam(root, name + " handrail", a + Vector((0, 0, 1.18)), b + Vector((0, 0, 1.18)), .052)
    beam(root, name + " lower rail", a + Vector((0, 0, .20)), b + Vector((0, 0, .20)), .035)
    steps = max(1, math.ceil(length / 2.6))
    for i in range(steps + 1):
        p = a.lerp(b, i / steps)
        beam(root, name + " post", p, p + Vector((0, 0, 1.21)), .052)
    if glass:
        for i in range(steps):
            p = a + direction * (length * i / steps + .11)
            q = a + direction * (length * (i + 1) / steps - .11)
            panel(root, name + " glazing", [p + Vector((0, 0, .27)), q + Vector((0, 0, .27)),
                  q + Vector((0, 0, 1.08)), p + Vector((0, 0, 1.08))], "SC_Glass", .018)


def viaduct():
    root = group("SkyViaduct")
    box(root, "Continuous bridge deck", (0, 0, -.30), (7, 24, .60), bevel=.045)
    # Paired deep structural girders visibly continue underneath the deck.
    for x in (-2.62, 2.62):
        box(root, "Longitudinal girder", (x, 0, -1.23), (.50, 24, 1.54), bevel=.05)
        box(root, "Girder foot", (x, 0, -1.88), (.78, 24, .24), bevel=.035)
    for y in (-9, -3, 3, 9):
        box(root, "Underside cross beam", (0, y, -1.14), (6.18, .44, .67), bevel=.045)
    for side in (-1, 1):
        x = side * 3.35
        box(root, "Raised edge parapet", (x, 0, .27), (.30, 24, .54), bevel=.04)
        box(root, "Parapet silver coping", (x, 0, .58), (.37, 24, .10), "SC_Metal", .015)
        rail(root, (x, -11.92, -.40), (x, 11.92, -.40), glass=False)
    # Central single line. Train wheels ride at rail top Y=.16.
    box(root, "Track bed", (0, 0, .016), (2.65, 24, .032), "SC_Metal", .005)
    for y in range(-11, 12):
        box(root, "Concrete sleeper", (0, y, .068), (2.18, .21, .105), bevel=.008)
    for x in (-.85, .85):
        box(root, "Steel running rail", (x, 0, .135), (.075, 24, .050), "SC_Metal", .009)
    for side in (-1, 1):
        box(root, "Inspection walkway", (side * 2.53, 0, .045), (1.07, 24, .09), bevel=.02)
        for y in range(-11, 12, 3):
            box(root, "Walkway edge marker", (side * 2.03, y, .094), (.05, .68, .012), "SC_Warm", .003)
    return root


def terrace():
    root = group("SkyTerrace")
    box(root, "Occupied roof deck", (0, 0, -.50), (18, 12, 1), bevel=.06)
    box(root, "Roof deck perimeter skirt", (0, 0, -.76), (17.74, 11.74, .27), "SC_Metal", .035)
    # Subtle slab seams preserve a calm visual field around the playable route.
    for x in (-6, -3, 0, 3, 6):
        box(root, "Slab expansion seam", (x, 0, .008), (.020, 11.55, .015), "SC_Metal", .002)
    for y in (-3, 0, 3):
        box(root, "Slab expansion seam", (0, y, .008), (17.55, .020, .015), "SC_Metal", .002)
    # Four 4m-wide entrances let this module connect to bridges or stairs.
    for side in (-1, 1):
        for lo, hi in ((-8.80, -2), (2, 8.80)):
            rail(root, (lo, side * 5.80, 0), (hi, side * 5.80, 0))
        for lo, hi in ((-5.80, -2), (2, 5.80)):
            rail(root, (side * 8.80, lo, 0), (side * 8.80, hi, 0))
        for x in (-7.40, 7.40):
            box(root, "Perimeter warm light", (x, side * 5.82, -.10), (2.1, .055, .065), "SC_Warm", .01)
    return root


def stair():
    root = group("SkyStair")
    # 30 treads, 0.20m rise / 0.30m going: a 6m city-layer connection.
    # Lower entry is at Blender Y=+4.5, upper entry Y=-4.5.
    n = 30
    for i in range(n):
        y = 4.5 - (i + .5) * .30
        top = (i + 1) * .20
        box(root, "Concrete tread", (0, y, top - .15), (3.8, .30, .30), bevel=.012)
        box(root, "Tread nosing", (0, y + .136, top + .006), (3.63, .028, .012), "SC_Metal", .003)
    # Solid sloped stringer profiles carry the flight without filling the void.
    for side in (-1, 1):
        x = side * 1.72
        panel(root, "Structural stair stringer", [(x, 4.50, 0), (x, -4.50, 6),
              (x, -4.50, 5.48), (x, 4.50, -.52)], "SC_Concrete", .22)
        beam(root, "Continuous stair handrail", (side * 1.81, 4.47, 1.18),
             (side * 1.81, -4.48, 7.16), .055)
        for i in range(0, n, 4):
            y = 4.5 - (i + .5) * .30
            h = (i + 1) * .20
            beam(root, "Stair railing post", (side * 1.81, y, h),
                 (side * 1.81, y, h + 1.16), .046)
        beam(root, "Continuous stair lower rail", (side * 1.81, 4.47, .51),
             (side * 1.81, -4.48, 6.49), .030)
    return root


def export(root):
    merge_materials(root)
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for obj in root.children_recursive:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    path = MODEL_DIR / (root.name + ".fbx")
    bpy.ops.export_scene.fbx(filepath=str(path), use_selection=True, object_types={"EMPTY", "MESH"},
        apply_scale_options="FBX_SCALE_UNITS", apply_unit_scale=True, bake_space_transform=False,
        axis_forward="-Z", axis_up="Y", add_leaf_bones=False, mesh_smooth_type="FACE",
        use_mesh_modifiers=True, path_mode="AUTO", embed_textures=False, bake_anim=False)
    meshes = [obj for obj in root.children_recursive if obj.type == "MESH"]
    corners = [obj.matrix_world @ Vector(c) for obj in meshes for c in obj.bound_box]
    low = [min(c[i] for c in corners) for i in range(3)]
    high = [max(c[i] for c in corners) for i in range(3)]
    triangles = 0
    for obj in meshes:
        obj.data.calc_loop_triangles()
        triangles += len(obj.data.loop_triangles)
    entry = {"asset": root.name, "fbx": str(path.relative_to(ROOT)).replace("\\", "/"),
             "mesh_renderers": len(meshes), "triangles": triangles,
             "materials": [obj.data.materials[0].name for obj in meshes],
             "blender_min_xyz_m": [round(v, 4) for v in low],
             "blender_max_xyz_m": [round(v, 4) for v in high],
             "unity_size_xyz_m": [round(high[i] - low[i], 4) for i in (0, 2, 1)],
             "bytes": path.stat().st_size}
    assert len(meshes) <= 7, entry
    print("STACKED_CITY_ASSET=" + json.dumps(entry))
    return entry


def preview(roots):
    # Inspection arrangement in .blend; all exported FBX files remain at origin.
    offsets = [(11, -1, .17), (-5, -9, 0), (11, -1, 0), (-9, 2, 0), (-9, 14, 0)]
    for root, offset in zip(roots, offsets):
        root.location = offset
    scene = bpy.context.scene
    bpy.ops.object.camera_add(location=(39, -49, 34))
    camera = bpy.context.object
    camera.name = "Kit inspection camera"
    camera.rotation_euler = (Vector((0, 0, 0)) - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 48
    scene.camera = camera
    bpy.ops.object.light_add(type="AREA", location=(3, -13, 24))
    light = bpy.context.object
    light.data.energy = 4200
    light.data.size = 20
    light.rotation_euler = (Vector((0, 0, 0)) - light.location).to_track_quat("-Z", "Y").to_euler()
    bpy.ops.object.light_add(type="SUN", location=(6, -8, 16))
    bpy.context.object.rotation_euler = (math.radians(22), math.radians(-20), math.radians(-25))
    bpy.context.object.data.energy = 2.4
    scene.world.use_nodes = True
    scene.world.node_tree.nodes["Background"].inputs["Color"].default_value = (.32, .39, .47, 1)
    scene.world.node_tree.nodes["Background"].inputs["Strength"].default_value = .7
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1500
    scene.render.resolution_y = 1080
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = str(SOURCE_DIR / "StackedCityKit.preview.png")
    scene.view_settings.view_transform = "AgX"
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))
    bpy.ops.render.render(write_still=True)


def main():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.0
    bpy.context.preferences.filepaths.save_version = 0
    MODEL_DIR.mkdir(parents=True, exist_ok=True)
    SOURCE_DIR.mkdir(parents=True, exist_ok=True)
    material("SC_Concrete", (.63, .66, .66), .08, .48)
    material("SC_Glass", (.018, .061, .082), .36, .19)
    material("SC_Metal", (.062, .085, .103), .65, .33)
    material("SC_Cyan", (.015, .70, .88), .20, .27, 1.6)
    material("SC_Warm", (1.0, .72, .32), .06, .28, 2.2)
    material("SC_Foliage", (.095, .245, .112), .0, .82)
    roots = [train(), planter(), viaduct(), terrace(), stair()]
    report = [export(root) for root in roots]
    (SOURCE_DIR / "StackedCityKit.manifest.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    rows = "\n".join(f"| {r['asset']} | {' x '.join(str(v) for v in r['unity_size_xyz_m'])} | {r['mesh_renderers']} | {r['triangles']} |" for r in report)
    readme = """# Stacked City Kit

Original authored meshes generated locally in Blender by `Tools/Art/create_stacked_city_kit.py`.
No downloaded or third-party model, texture, font, or decal is included.

All units are metres. FBX export: -Z forward, Y up, apply unit scale, no animation.
Train nose points toward Blender -Y (Unity +Z under the project import convention).
Train/planter roots are at pavement height. Bridge and terrace deck surface is Y=0;
their supporting structures extend downwards. Bridge length is 24m with a single
central line, steel rails at X=-0.85m and +0.85m, and two inspection walkways.
Rail top is Y=0.16m: place train root at Y=0.16m.
The stair flight rises 6m over 9m horizontal length toward Unity +Z; the railing
continues to ~7.2m above its lower entry. The terrace has 4m entrance openings.

Every mesh is merged by its SC_* material. Unity should remap slots to project
materials, keep model scale at one metre, and disable generated colliders for
these background presentation modules. Windows are intentionally opaque dark
glazing: no alpha sorting, transparency pass, or texture dependency is required.

The blend file includes an inspection arrangement and lighting. Each exported
FBX is centred independently; do not use the inspection arrangement as a level.

| Asset | Unity XYZ bounds (m) | Mesh renderers | Triangles |
| --- | --- | ---: | ---: |
""" + rows + "\n\nThe manifest records exact bounds, material names, and export byte counts.\n"
    (SOURCE_DIR / "StackedCityKit.README.md").write_text(readme, encoding="utf-8")
    preview(roots)
    print("STACKED_CITY_KIT_OK")


if __name__ == "__main__":
    main()
