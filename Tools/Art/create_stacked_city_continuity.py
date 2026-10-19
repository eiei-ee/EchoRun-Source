"""Author solid city foundations and a stepped corner tower in metres.

Blender +Z is height; FBX exports Unity +Y. This is an offline formal-model
authoring script, not runtime geometry. The two FBX roots remain at the origin.
"""
import importlib.util
import json
import math
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location("stacked_kit", HERE / "create_stacked_city_kit.py")
kit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(kit)


def box(root, name, center, size, mat="SC_Concrete", bevel=0):
    return kit.box(root, name, center, size, mat, bevel)


def facade(root, name, cx, cy, width, depth, bottom, top, pitch=4.2):
    """Continuous occupied window bands with piers, all inside the module edge."""
    first = bottom + 2.1
    rows = max(1, int((top - bottom - 2.0) / pitch))
    for index in range(rows):
        height = first + index * pitch
        for side in (-1, 1):
            box(root, name + " front glazing", (cx, cy + side * (depth / 2 + .016), height),
                (width - .9, .036, 2.0), "SC_Glass")
            box(root, name + " side glazing", (cx + side * (width / 2 + .016), cy, height),
                (.036, depth - .9, 2.0), "SC_Glass")
    for side in (-1, 1):
        horizontal_bays = max(2, round(width / 3))
        for index in range(horizontal_bays + 1):
            x = cx - width / 2 + .20 + index * (width - .40) / horizontal_bays
            box(root, name + " front vertical pier", (x, cy + side * (depth / 2 + .057), (bottom + top) / 2),
                (.26, .13, top - bottom), "SC_Concrete")
        depth_bays = max(2, round(depth / 3))
        for index in range(depth_bays + 1):
            y = cy - depth / 2 + .20 + index * (depth - .40) / depth_bays
            box(root, name + " side vertical pier", (cx + side * (width / 2 + .057), y, (bottom + top) / 2),
                (.13, .26, top - bottom), "SC_Concrete")


def slab(root, name, cx, cy, width, depth, level, thickness=.55):
    box(root, name + " structural deck", (cx, cy, level - thickness / 2),
        (width, depth, thickness), "SC_Concrete", .07)
    box(root, name + " underside reveal", (cx, cy, level - thickness - .09),
        (width - .18, depth - .18, .18), "SC_Metal", .015)


def guard(root, name, a, b, level):
    """Low-cost authored parapet; posts follow a supported deck edge."""
    a, b = Vector((*a, level)), Vector((*b, level))
    delta = b - a
    mid = (a + b) / 2
    along_x = abs(delta.x) > abs(delta.y)
    beam_size = (abs(delta.x), .12, .10) if along_x else (.12, abs(delta.y), .10)
    box(root, name + " handrail", (mid.x, mid.y, level + 1.1), beam_size, "SC_Metal")
    curb_size = (abs(delta.x), .25, .28) if along_x else (.25, abs(delta.y), .28)
    box(root, name + " curb", (mid.x, mid.y, level + .14), curb_size)
    count = max(1, math.ceil(delta.length / 3.0))
    for index in range(count + 1):
        point = a.lerp(b, index / count)
        box(root, name + " upright", (point.x, point.y, level + .63), (.11, .11, .96), "SC_Metal")


def planted_trough(root, name, cx, cy, level, width=4.0, seed=0):
    box(root, name + " trough", (cx, cy, level + .33), (width, 1.1, .66), bevel=.065)
    box(root, name + " planting soil", (cx, cy, level + .674), (width - .26, .86, .04), "SC_Metal")
    for index in range(3):
        x = cx + (index - 1) * width * .28
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=1, location=(x, cy, level + 1.18))
        obj = bpy.context.object
        obj.name = name + " shrub crown"
        obj.scale = (.73, .45, .58)
        obj.rotation_euler[2] = seed + index * .73
        kit.finish(obj, root, "SC_Foliage")


def deep_base():
    root = kit.group("CityDeepBase")
    # A full inhabited body continues to the lower foundation, with no empty stilts.
    box(root, "Continuous inhabited foundation core", (0, 0, -47), (18.5, 18.5, 94), bevel=.12)
    facade(root, "Lower district", 0, 0, 18.5, 18.5, -92, -1.0, 4.2)
    # Broad shoulders articulate three further street levels. They stay within
    # a twenty-metre square so fitting the module cannot invade a track corridor.
    for index, level in enumerate((-14.0, -38.0, -66.0)):
        slab(root, "Lower street shoulder " + str(index + 1), 0, 0, 20, 20, level, .8)
        for side in (-1, 1):
            box(root, "Crosshead front support", (0, side * 9.36, level - 1.56),
                (18.4, .75, 1.48), bevel=.04)
            box(root, "Crosshead side support", (side * 9.36, 0, level - 1.56),
                (.75, 18.4, 1.48), bevel=.04)
    for x in (-8.88, 8.88):
        for y in (-8.88, 8.88):
            # End caps sit below the upper coping and inside the closed foot;
            # they must not share the core's top or bottom material plane.
            box(root, "Continuous corner structure", (x, y, -47), (.76, .76, 92.8), "SC_Metal", .055)
    box(root, "Closed chamfered foundation foot", (0, 0, -94.5), (20, 20, 3), bevel=.23)
    box(root, "Foundation upper step", (0, 0, -92.78), (19.35, 19.35, .46), bevel=.075)
    # Flush mating face; absolutely no mesh extends above local Unity Y zero.
    box(root, "Flush building interface cornice", (0, 0, -.24), (19.1, 19.1, .48), bevel=.045)
    return root


def occupied_mass(root, name, center, size, base, pitch=4.2):
    width, depth, height = size
    cx, cy = center
    box(root, name + " inhabited solid", (cx, cy, base + height / 2), size, bevel=.11)
    facade(root, name, cx, cy, width, depth, base + .5, base + height - .7, pitch)
    for side in (-1, 1):
        box(root, name + " vertical service spine", (cx + side * (width / 2 - .50), cy - depth / 2 - .08, base + height / 2),
            (.65, .16, height - .2), "SC_Metal", .025)


def corner_tower():
    root = kit.group("CityCornerTower")
    occupied_mass(root, "Four-sided urban podium", (0, 0), (17.4, 21.4, 20), 0)
    slab(root, "Podium public street", 0, 0, 18, 22, 20, .75)
    occupied_mass(root, "West terraced wing", (-4.5, 1.2), (7.8, 15.3, 43.8), 20)
    occupied_mass(root, "East landmark tower", (4.7, 2.2), (7.0, 16.0, 55.5), 20)
    occupied_mass(root, "Setback upper crown", (4.7, 2.8), (5.9, 10.8, 8.1), 75.5)
    # The low front hall links both towers, making the terraces inhabited spaces.
    occupied_mass(root, "Cross-block gallery", (0, -6.1), (14.9, 5.9, 9.8), 20)
    slab(root, "Gallery roof square", 0, -6.1, 15.3, 6.3, 29.8)
    guard(root, "Gallery front railing", (-7.5, -9.13), (7.5, -9.13), 29.8)
    planted_trough(root, "Gallery planting west", -4.6, -7.8, 29.8, 3.2, .2)
    planted_trough(root, "Gallery planting east", 4.6, -7.8, 29.8, 3.2, 1.5)
    # Upper bridges sit between and overlap their own two solid wings. They do
    # not project beyond the authored footprint or hang across a gameplay road.
    for index, level in enumerate((42.0, 55.0)):
        slab(root, "Attached sky gallery " + str(index + 1), .10, -4.45, 16.65, 3.65, level, .65)
        guard(root, "Sky gallery front " + str(index + 1), (-8.08, -6.16), (8.28, -6.16), level)
        for x in (-7.5, 7.5):
            box(root, "Gallery root pier", (x, -4.5, level - 1.1), (.6, 3.1, 1.55), bevel=.055)
    slab(root, "West roof terrace", -4.5, 1.2, 8.1, 15.6, 63.8)
    guard(root, "West skyline edge", (-8.4, -6.4), (-.6, -6.4), 63.8)
    guard(root, "West garden edge", (-8.4, -6.4), (-8.4, 8.8), 63.8)
    planted_trough(root, "West roof planting", -4.5, -4.8, 63.8, 4.2, 2.2)
    slab(root, "East shoulder terrace", 4.7, 2.2, 7.3, 16.3, 75.5)
    guard(root, "East roof front edge", (1.15, -5.8), (8.25, -5.8), 75.5)
    planted_trough(root, "East shoulder planting", 4.7, -4.7, 75.5, 3.4, 3.0)
    slab(root, "Upper crown cornice", 4.7, 2.8, 6.2, 11.1, 83.6)
    # Two warm porcelain strips distinguish this corner landmark at a distance.
    for x in (4.05, 4.7):
        box(root, "Crown amber locator", (x, -2.785, 79.1), (.19, .035, 5.4), "SC_Orange")
    return root


def cross2(a, b):
    return a[0] * b[1] - a[1] * b[0]


def minus2(a, b):
    return (a[0] - b[0], a[1] - b[1])


def triangle_intersection_area(a, b):
    # Same co-facing triangle clipping rule as the Unity CitySurfaceReview.
    for axis in range(2):
        if max(point[axis] for point in a) <= min(point[axis] for point in b):
            return 0
        if max(point[axis] for point in b) <= min(point[axis] for point in a):
            return 0
    polygon = list(a)
    sign = 1 if cross2(minus2(b[1], b[0]), minus2(b[2], b[0])) >= 0 else -1
    for edge in range(3):
        if not polygon:
            break
        start = b[edge]
        direction = minus2(b[(edge + 1) % 3], start)
        output = []
        previous = polygon[-1]
        before = sign * cross2(direction, minus2(previous, start))
        for current in polygon:
            after = sign * cross2(direction, minus2(current, start))
            if (after >= 0) != (before >= 0):
                factor = before / (before - after)
                output.append(tuple(previous[i] + (current[i] - previous[i]) * factor for i in range(2)))
            if after >= 0:
                output.append(current)
            previous, before = current, after
        polygon = output
    return abs(sum(cross2(point, polygon[(index + 1) % len(polygon)])
                   for index, point in enumerate(polygon))) * .5


def audit_material_planes(root):
    planes = {}
    for obj in root.children_recursive:
        if obj.type != "MESH":
            continue
        obj.data.calc_loop_triangles()
        vertices = [obj.matrix_world @ vertex.co for vertex in obj.data.vertices]
        material_name = obj.data.materials[0].name
        for tri in obj.data.loop_triangles:
            corners = [vertices[index] for index in tri.vertices]
            normal = (corners[1] - corners[0]).cross(corners[2] - corners[0])
            if normal.length_squared < 1e-12:
                continue
            normal.normalize()
            key = tuple(round(value * 1000) for value in normal) + (round(corners[0].dot(normal) * 2000),)
            axis = max(range(3), key=lambda index: abs(normal[index]))
            u, v = (axis + 1) % 3, (axis + 2) % 3
            points = [(point[u], point[v]) for point in corners]
            planes.setdefault(key, []).append((material_name, points))
    overlaps = {}
    for key, faces in planes.items():
        for index, (mat_a, points_a) in enumerate(faces):
            for mat_b, points_b in faces[index + 1:]:
                if mat_a == mat_b:
                    continue
                area = triangle_intersection_area(points_a, points_b)
                if area >= .002:
                    pair = (mat_a, mat_b, key)
                    overlaps[pair] = overlaps.get(pair, 0) + area
    print("CONTINUITY_MATERIAL_PLANE_AUDIT=" + root.name + " " + repr(overlaps))
    assert not overlaps, (root.name, overlaps)


def verify_and_export(root, max_triangles):
    # Material order and outward normals are explicit before the common exporter.
    groups = {}
    for obj in list(root.children_recursive):
        if obj.type == "MESH":
            groups.setdefault(obj.data.materials[0].name, []).append(obj)
    for material_name in sorted(groups):
        objects = groups[material_name]
        bpy.ops.object.select_all(action="DESELECT")
        for obj in objects:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = objects[0]
        if len(objects) > 1:
            bpy.ops.object.join()
        obj = bpy.context.object
        obj.name = root.name + "_" + material_name
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        assert bm.calc_volume(signed=True) > 0, obj.name
        assert all(not face.normal.length_squared < .9 for face in bm.faces), obj.name
        bm.to_mesh(obj.data)
        bm.free()
        obj.select_set(False)
    audit_material_planes(root)
    report = kit.export(root)
    report["materials"] = sorted(report["materials"])
    report["unity_min_xyz_m"] = [report["blender_min_xyz_m"][0], report["blender_min_xyz_m"][2], -report["blender_max_xyz_m"][1]]
    report["unity_max_xyz_m"] = [report["blender_max_xyz_m"][0], report["blender_max_xyz_m"][2], -report["blender_min_xyz_m"][1]]
    report["outward_normals_verified"] = True
    report["competing_coplanar_material_pairs"] = 0
    report["material_limit"] = 5
    report["triangle_limit"] = max_triangles
    assert report["triangles"] <= max_triangles, report
    assert report["mesh_renderers"] <= 5, report
    return report


def preview(roots):
    # Separate inspection positions do not affect independently exported FBXs.
    roots[0].location = (-24, 0, 0)
    roots[1].location = (22, 0, -42)
    scene = bpy.context.scene
    scene.world.use_nodes = True
    scene.world.node_tree.nodes["Background"].inputs["Color"].default_value = (.12, .17, .23, 1)
    scene.world.node_tree.nodes["Background"].inputs["Strength"].default_value = .6
    bpy.ops.object.camera_add(location=(135, -210, 98))
    camera = bpy.context.object
    target = Vector((0, 0, -27))
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 158
    scene.camera = camera
    bpy.ops.object.light_add(type="SUN", location=(5, -20, 40))
    sun = bpy.context.object
    sun.rotation_euler = (math.radians(27), math.radians(-25), math.radians(-28))
    sun.data.energy = 2.5
    sun.data.angle = math.radians(6)
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1800
    scene.render.resolution_y = 1500
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = str(kit.SOURCE_DIR / "StackedCityContinuity.preview.png")
    scene.view_settings.view_transform = "AgX"
    bpy.ops.wm.save_as_mainfile(filepath=str(kit.SOURCE_DIR / "StackedCityContinuity.blend"))
    bpy.ops.render.render(write_still=True)


def main():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.context.scene.unit_settings.system = "METRIC"
    kit.material("SC_Concrete", (.77, .75, .68), .0, .65)
    kit.material("SC_Metal", (.078, .11, .13), .50, .32)
    kit.material("SC_Glass", (.019, .065, .09), .44, .17)
    kit.material("SC_Orange", (.85, .28, .055), .05, .48)
    kit.material("SC_Foliage", (.095, .20, .07), 0, .85)
    roots = [deep_base(), corner_tower()]
    report = [verify_and_export(roots[0], 7000), verify_and_export(roots[1], 15000)]
    assert report[0]["unity_min_xyz_m"][1] == -96.0, report[0]
    assert report[0]["unity_max_xyz_m"][1] == 0.0, report[0]
    (kit.SOURCE_DIR / "StackedCityContinuity.manifest.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    rows = "\n".join(f"| {item['asset']} | {item['unity_min_xyz_m']} | {item['unity_max_xyz_m']} | {item['triangles']} | {item['mesh_renderers']} |" for item in report)
    readme = """# Stacked City Continuity

Original static architectural meshes authored locally in Blender by
`Tools/Art/create_stacked_city_continuity.py`. No downloaded model, texture,
font, or animation is included. Existing project SC_* materials are reused.

Blender +Z is height, FBX uses -Z forward / +Y up, metre units. Unity importer
must remap materials, disable animation and colliders, and preserve local root.
CityDeepBase attaches by its top face at local Y=0 and extends down 96 metres.
Its inhabited core is 18.5 metres square with a closed 20-metre foundation foot.
Horizontal X/Z fitting is supported; retain its full vertical depth. It is
intended to meet another building, not to be the sole visible building top.
CityCornerTower attaches by its base at Y=0; stepped wings, attached galleries,
four occupied terraces, and a setback crown form a distinct tall silhouette.
All galleries overlap their own solid support wings. No geometry or scripts
extend a bridge into a track corridor; placement and clearance are Unity-owned.

Geometry is merged into one mesh per shared material. All component normals
were recalculated outward and signed volumes checked positive before export.
An offline equivalent of CitySurfaceReview checks same-facing triangle plane
overlap across materials, including bottom faces; both models have zero pairs.
Foundation metal corner caps terminate inside the coping/foot assembly instead
of sharing the concrete core's top and bottom planes.
Windows are opaque dark glazing with continuous bands and raised masonry piers.
The final five-material budget includes a small amount of rooftop foliage.

The Blender source contains an offset inspection arrangement and lighting;
FBX files were exported at their own origins before that arrangement. The
preview is a source-model inspection image, not gameplay acceptance evidence.

| Asset | Unity minimum XYZ (m) | Unity maximum XYZ (m) | Triangles | Meshes/materials |
| --- | --- | --- | ---: | ---: |
""" + rows + "\n\nExact bounds, materials, and byte counts are in the manifest.\n"
    (kit.SOURCE_DIR / "StackedCityContinuity.README.md").write_text(readme, encoding="utf-8")
    preview(roots)
    print("STACKED_CITY_CONTINUITY_MODELS_OK")


if __name__ == "__main__":
    main()
