"""A bounded corner-tower variant with a solid central podium sign bay.

Blender --background --python Tools/Art/create_stacked_city_corner_district.py
Reuses the original continuity tower; only podium front/back glazing and its
centre piers differ. Original tower and foundation assets are never exported.
"""
from collections import Counter
import hashlib
import importlib.util
import json
import math
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector


HERE = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location("continuity", HERE / "create_stacked_city_continuity.py")
continuity = importlib.util.module_from_spec(spec)
spec.loader.exec_module(continuity)
kit = continuity.kit
ORIGINAL_FACADE = continuity.facade
PODIUM = "Four-sided urban podium"
HALF_SIGN_BAY = 1.8


def district_facade(root, name, cx, cy, width, depth, bottom, top, pitch=4.2):
    if name != PODIUM:
        return ORIGINAL_FACADE(root, name, cx, cy, width, depth, bottom, top, pitch)
    first = bottom + 2.1
    rows = max(1, int((top - bottom - 2.0) / pitch))
    left_edge, right_edge = cx - (width - .9) / 2, cx + (width - .9) / 2
    for index in range(rows):
        height = first + index * pitch
        for side in (-1, 1):
            for label, low, high in ((" left", left_edge, cx - HALF_SIGN_BAY),
                                     (" right", cx + HALF_SIGN_BAY, right_edge)):
                continuity.box(root, name + " front glazing" + label,
                               ((low + high) / 2, cy + side * (depth / 2 + .016), height),
                               (high - low, .036, 2.0), "SC_Glass")
            continuity.box(root, name + " side glazing", (cx + side * (width / 2 + .016), cy, height),
                           (.036, depth - .9, 2.0), "SC_Glass")
    for side in (-1, 1):
        horizontal_bays = max(2, round(width / 3))
        for index in range(horizontal_bays + 1):
            x = cx - width / 2 + .20 + index * (width - .40) / horizontal_bays
            if x + .13 > cx - HALF_SIGN_BAY and x - .13 < cx + HALF_SIGN_BAY:
                continue
            continuity.box(root, name + " front vertical pier",
                           (x, cy + side * (depth / 2 + .057), (bottom + top) / 2),
                           (.26, .13, top - bottom), "SC_Concrete")
        depth_bays = max(2, round(depth / 3))
        for index in range(depth_bays + 1):
            y = cy - depth / 2 + .20 + index * (depth - .40) / depth_bays
            continuity.box(root, name + " side vertical pier",
                           (cx + side * (width / 2 + .057), y, (bottom + top) / 2),
                           (.13, .26, top - bottom), "SC_Concrete")


def bounds(obj):
    points = [obj.matrix_world @ v.co for v in obj.data.vertices]
    return [min(p[i] for p in points) for i in range(3)], [max(p[i] for p in points) for i in range(3)]


def altered_component(obj):
    if obj.name.startswith(PODIUM + " front glazing"):
        return True
    if obj.name.startswith(PODIUM + " front vertical pier"):
        low, high = bounds(obj)
        return high[0] > -HALF_SIGN_BAY and low[0] < HALF_SIGN_BAY
    return False


def geometry_signature(obj):
    payload = {
        "material": obj.data.materials[0].name,
        "vertices": [[round(c, 6) for c in (obj.matrix_world @ v.co)] for v in obj.data.vertices],
        "faces": [list(p.vertices) for p in obj.data.polygons]
    }
    return hashlib.sha256(json.dumps(payload, separators=(",", ":")).encode()).hexdigest()


def unchanged_components(root):
    return Counter(geometry_signature(obj) for obj in root.children_recursive
                   if obj.type == "MESH" and not altered_component(obj))


def verify_target_bay(root):
    glazing = [obj for obj in root.children_recursive if obj.name.startswith(PODIUM + " front glazing")]
    assert len(glazing) == 16, len(glazing)
    for obj in glazing:
        low, high = bounds(obj)
        assert high[0] <= -HALF_SIGN_BAY + .00001 or low[0] >= HALF_SIGN_BAY - .00001, obj.name
    piers = [obj for obj in root.children_recursive if obj.name.startswith(PODIUM + " front vertical pier")]
    assert len(piers) == 12, len(piers)
    for obj in piers:
        low, high = bounds(obj)
        assert high[0] < -HALF_SIGN_BAY or low[0] > HALF_SIGN_BAY, obj.name
    body = next(obj for obj in root.children_recursive if obj.name.startswith(PODIUM + " inhabited solid"))
    bm = bmesh.new()
    try:
        bm.from_mesh(body.data)
        assert all(edge.is_manifold for edge in bm.edges)
        assert bm.calc_volume(signed=True) > 0
    finally:
        bm.free()
    return {"blank_front_back_x_m": [-HALF_SIGN_BAY, HALF_SIGN_BAY],
            "front_back_wall_blender_y_m": [-10.7, 10.7], "solid_podium_height_m": 20,
            "front_back_glazing_segments": len(glazing), "front_back_piers": len(piers),
            "podium_closed_manifold": True}


def preview(root):
    scene = bpy.context.scene
    scene.world = bpy.data.worlds.new("Corner district inspection world")
    scene.world.use_nodes = True
    scene.world.node_tree.nodes["Background"].inputs["Color"].default_value = (.12, .17, .23, 1)
    scene.world.node_tree.nodes["Background"].inputs["Strength"].default_value = .7
    bpy.ops.object.camera_add(location=(76, -135, 65))
    camera = bpy.context.object
    camera.name = "Corner district complete model inspection"
    camera.rotation_euler = (Vector((0, 0, 40)) - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 93
    scene.camera = camera
    bpy.ops.object.light_add(type="SUN", location=(5, -20, 40))
    sun = bpy.context.object
    sun.rotation_euler = (math.radians(27), math.radians(-25), math.radians(-28))
    sun.data.energy = 2.5
    sun.data.angle = math.radians(6)
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 900
    scene.render.resolution_y = 1300
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.view_settings.view_transform = "AgX"
    scene.render.filepath = str(kit.SOURCE_DIR / "StackedCityCornerDistrict.preview.png")
    bpy.ops.wm.save_as_mainfile(filepath=str(kit.SOURCE_DIR / "StackedCityCornerDistrict.blend"))
    bpy.ops.render.render(write_still=True)
    camera.location = (4.5, -54, 12)
    camera.rotation_euler = (Vector((0, -10.7, 10)) - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.ortho_scale = 24
    scene.render.resolution_x = 1000
    scene.render.resolution_y = 1100
    scene.render.filepath = str(kit.SOURCE_DIR / "StackedCityCornerDistrict.podium.png")
    bpy.ops.render.render(write_still=True)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1
    kit.material("SC_Concrete", (.77, .75, .68), 0, .65)
    kit.material("SC_Metal", (.078, .11, .13), .50, .32)
    kit.material("SC_Glass", (.019, .065, .09), .44, .17)
    kit.material("SC_Orange", (.85, .28, .055), .05, .48)
    kit.material("SC_Foliage", (.095, .20, .07), 0, .85)
    baseline = continuity.corner_tower()
    bpy.context.view_layer.update()
    expected = unchanged_components(baseline)
    try:
        continuity.facade = district_facade
        root = continuity.corner_tower()
    finally:
        continuity.facade = ORIGINAL_FACADE
    root.name = "CityCornerDistrictTower"
    bpy.context.view_layer.update()
    actual = unchanged_components(root)
    assert actual == expected, {"added": actual - expected, "removed": expected - actual}
    bay_report = verify_target_bay(root)
    for obj in list(baseline.children_recursive) + [baseline]:
        bpy.data.objects.remove(obj, do_unlink=True)
    for mesh in list(bpy.data.meshes):
        if mesh.users == 0:
            bpy.data.meshes.remove(mesh)
    report = continuity.verify_and_export(root, 15000)
    original_manifest = json.loads((kit.SOURCE_DIR / "StackedCityContinuity.manifest.json").read_text(encoding="utf-8"))
    original = next(entry for entry in original_manifest if entry["asset"] == "CityCornerTower")
    for key in ("unity_min_xyz_m", "unity_max_xyz_m", "unity_size_xyz_m"):
        assert report[key] == original[key], (key, report[key], original[key])
    assert report["unity_min_xyz_m"][1] == 0
    report["source_variant_of"] = original["fbx"]
    report["unchanged_component_count"] = sum(expected.values())
    report["unchanged_components_geometry_sha256_verified"] = True
    report["podium_sign_bay"] = bay_report
    (kit.SOURCE_DIR / "StackedCityCornerDistrict.manifest.json").write_text(
        json.dumps(report, indent=2), encoding="utf-8")
    preview(root)
    print("STACKED_CITY_CORNER_DISTRICT_OK unchanged_components=%d triangles=%d" %
          (sum(expected.values()), report["triangles"]))


if __name__ == "__main__":
    main()
