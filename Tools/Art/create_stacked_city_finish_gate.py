"""Author the Stacked City pedestrian arrival gate as a persistent static FBX.

Blender --background --python Tools/Art/create_stacked_city_finish_gate.py
The front lettering faces Unity -Z; feet sit on Y=0, pivot at ground centre.
No physics, animation, font, light, material shader, or gameplay object exports.
"""
import importlib.util
import json
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent


def module(name, filename):
    spec = importlib.util.spec_from_file_location(name, HERE / filename)
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


wall = module("finish_wall", "create_stacked_city_wall_details.py")
audit = module("finish_audit", "create_stacked_city_continuity.py")
kit = wall.kit
LETTER_OBJECTS = []


def box(root, name, centre, size, material="SC_Concrete", bevel=.012):
    return kit.box(root, name, centre, size, material, bevel)


def lettering(root, wording, x, z, height, width, depth=.387, back=False):
    previous = set(root.children_recursive)
    wall.text(root, wording, x, z, height, width, "SC_White", depth)
    obj = next(obj for obj in root.children_recursive if obj not in previous)
    if back:
        for vertex in obj.data.vertices:
            vertex.co.x *= -1
            vertex.co.y *= -1
        obj.data.update()
    LETTER_OBJECTS.append((obj, back))


def author():
    root = kit.group("CityFinishGate")
    # Panels stand on discrete plinths. Their 18 mm joints expose a narrower
    # recessed metal spine, not a decal lying on the mineral face.
    for side in (-1, 1):
        x = side * 5.08
        box(root, "Low stone footing", (x, 0, .16), (.86, .96, .32), bevel=.022)
        box(root, "Recessed structural column", (x, 0, 2.91), (.62, .62, 5.22), "SC_Metal")
        for index in range(4):
            bottom = .34 + index * 1.214
            box(root, "Mineral cladding course %d" % index,
                (x, 0, bottom + .598), (.78, .80, 1.196), bevel=.015)
        box(root, "Capital shadow collar", (x, 0, 5.19), (.82, .86, .074), "SC_Metal")
        box(root, "Mineral head capital", (x, 0, 5.335), (.90, 1.00, .20), bevel=.020)
        # Slim light fittings stay entirely outside the 9.3 metre passage.
        for face in (-1, 1):
            box(root, "Inset column signal mounting", (x - side * .276, face * .426, 2.78),
                (.100, .025, 2.66), "SC_Metal", .006)
            box(root, "Column cyan guide", (x - side * .276, face * .462, 2.78),
                (.052, .015, 2.56), "SC_Cyan", .004)
            box(root, "Small orange registration", (x + side * .14, face * .421, 4.70),
                (.28, .018, .115), "SC_Orange", .005)
            box(root, "Arrival column enamel badge", (x, face * .424, .84),
                (.43, .025, .38), "SC_Metal", .010)
        lettering(root, "A1", x, .84, .17, .29, .455)
        lettering(root, "A1", -x, .84, .17, .29, .455, back=True)
    # A dark boxed lintel carries the sign. Mineral cap and metal subframe use
    # distinct depths/heights so their exposed edges never share a surface.
    box(root, "Deep graphite arrival lintel", (0, 0, 4.91), (10.38, .72, 1.22), "SC_Metal", .020)
    box(root, "Top white weather flashing", (0, 0, 5.515), (11.04, .86, .070), "SC_White", .010)
    box(root, "Overhanging mineral canopy", (0, 0, 5.665), (11.30, 1.08, .230), bevel=.024)
    # Each front is a shallow recessed field framed by substantial shoulders.
    # Lettering sits 27 mm in front of the dark face, recessed behind the caps.
    for face in (-1, 1):
        for side in (-1, 1):
            box(root, "Lintel mineral cheek", (side * 4.73, face * .426, 4.93),
                (.38, .12, .90), bevel=.015)
            box(root, "Orange lintel register", (side * 4.36, face * .389, 5.015),
                (.070, .018, .59), "SC_Orange", .004)
            # Restrained finish-line tiles, deliberately square and unlit.
            for column in range(3):
                for row in range(2):
                    if (column + row) % 2 == 0:
                        box(root, "Enamel finish register", (side * (3.63 + column * .21), face * .389, 4.87 + row * .21),
                            (.17, .018, .17), "SC_White", .004)
        for x in (-2.88, 0, 2.88):
            box(root, "Undersign cyan line", (x, face * .388, 4.395),
                (2.57, .016, .043), "SC_Cyan", .003)
    lettering(root, "终点", 0, 5.055, .69, 2.60)
    lettering(root, "F I N I S H", 0, 4.565, .175, 2.02)
    lettering(root, "FINISH", 0, 4.985, .48, 3.20, back=True)
    # Authored wall-kit glyph fronts are -Y. A 180 degree turn makes the gate
    # front +Y in Blender, hence -Z in Unity under the existing FBX convention.
    for obj in root.children_recursive:
        if obj.type == "MESH":
            for vertex in obj.data.vertices:
                vertex.co.x *= -1
                vertex.co.y *= -1
            obj.data.update()
    return root


def validate(root):
    lettering_objects = {obj for obj, _ in LETTER_OBJECTS}
    for obj in root.children_recursive:
        if obj.type != "MESH":
            continue
        if obj in lettering_objects:
            continue
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        assert all(edge.is_manifold for edge in bm.edges), obj.name
        assert bm.calc_volume(signed=True) > 0, obj.name
        bm.to_mesh(obj.data)
        bm.free()
        obj.data.update()
    for obj, back in LETTER_OBJECTS:
        # Native glyph front normals retain correct front/back handedness.
        expected = -1 if back else 1
        assert all(p.normal.y * expected > .99 for p in obj.data.polygons), obj.name
    bpy.context.view_layer.update()
    audit.audit_material_planes(root)
    for obj in root.children_recursive:
        if obj.type != "MESH":
            continue
        # No authored geometry occupies the runner/camera aperture.
        for vertex in obj.data.vertices:
            p = obj.matrix_world @ vertex.co
            assert abs(p.x) >= 4.6499 or p.z >= 4.2999, (obj.name, list(p))
    entry = kit.export(root)
    low, high = entry["blender_min_xyz_m"], entry["blender_max_xyz_m"]
    entry["unity_min_xyz_m"] = [low[0], low[2], -high[1]]
    entry["unity_max_xyz_m"] = [high[0], high[2], -low[1]]
    assert entry["unity_size_xyz_m"] == [11.3, 5.78, 1.08], entry
    assert entry["triangles"] <= 8000, entry
    assert entry["mesh_renderers"] <= 6, entry
    entry.update({
        "orientation": "Unity +Y up; front Chinese lettering faces Unity -Z; runner approaches along +Z",
        "pivot": "Ground centre at Unity (0,0,0)",
        "clear_aperture_width_m": 9.3,
        "clear_aperture_height_m": 4.3,
        "column_centres_x_m": [-5.08, 5.08],
        "outward_normals_verified": True,
        "lettering_front_and_back_normals_verified": True,
        "competing_coplanar_material_pairs": 0,
        "lettering_clearance_from_lintel_face_m": .027,
        "signal_mesh": "CityFinishGate_SC_Cyan",
        "colliders": 0, "animations": 0, "runtime_fonts": 0,
        "lettering": wall.TEXT_RECORDS,
        "provenance": "Original locally authored 3D model. Chinese: installed SimHei outlines. Latin: existing EchoRun Sans SC (SIL OFL 1.1) outlines. No font file or external texture embedded."
    })
    return entry


def preview(root):
    scene = bpy.context.scene
    bpy.ops.object.camera_add(location=(9.0, 23.0, 10.0))
    camera = bpy.context.object
    camera.name = "Arrival gate front inspection camera"
    camera.rotation_euler = (Vector((0, 0, 2.75)) - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 14.15
    scene.camera = camera
    for name, position, energy, size in [
        ("Warm afternoon key", (-6, 10, 12), 1850, 8),
        ("Cool city fill", (7, 7, 4), 950, 6),
        ("Canopy edge light", (-2, -5, 8), 1600, 5)]:
        bpy.ops.object.light_add(type="AREA", location=position)
        light = bpy.context.object
        light.name = name
        light.data.energy, light.data.size = energy, size
        light.rotation_euler = (Vector((0, 0, 2.8)) - light.location).to_track_quat("-Z", "Y").to_euler()
    scene.world = bpy.data.worlds.new("Stacked City arrival gate inspection world")
    scene.world.use_nodes = True
    scene.world.node_tree.nodes["Background"].inputs["Color"].default_value = (.13, .165, .19, 1)
    scene.world.node_tree.nodes["Background"].inputs["Strength"].default_value = .7
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x, scene.render.resolution_y = 1500, 1050
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = str(kit.SOURCE_DIR / "StackedCityFinishGate.preview.png")
    scene.view_settings.view_transform = "AgX"
    for curve in list(bpy.data.curves):
        if curve.users == 0:
            bpy.data.curves.remove(curve)
    for font in list(bpy.data.fonts):
        if font.name != "Bfont" and font.users == 0:
            bpy.data.fonts.remove(font)
    bpy.ops.wm.save_as_mainfile(filepath=str(kit.SOURCE_DIR / "StackedCityFinishGate.blend"))
    bpy.ops.render.render(write_still=True)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1
    kit.material("SC_Concrete", (.77, .75, .68), 0, .65)
    kit.material("SC_Metal", (.045, .069, .084), .5, .32)
    kit.material("SC_White", (.90, .92, .91), .08, .44)
    kit.material("SC_Orange", (.85, .28, .055), .05, .48)
    kit.material("SC_Cyan", (.015, .70, .88), .20, .27, 1.2)
    wall.FONTS["latin"] = bpy.data.fonts.load(str(wall.LATIN_PATH))
    wall.FONTS["chinese"] = bpy.data.fonts.load(str(wall.CHINESE_PATH))
    root = author()
    report = validate(root)
    (kit.SOURCE_DIR / "StackedCityFinishGate.manifest.json").write_text(
        json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")
    (kit.SOURCE_DIR / "StackedCityFinishGate.README.md").write_text(
        "# Stacked City Finish Gate\n\n"
        "Original formal model authored by `Tools/Art/create_stacked_city_finish_gate.py`. "
        "Two segmented mineral columns carry a deep graphite lintel and weather canopy. "
        "White Chinese 终点 / FINISH lettering, narrow cyan fittings and orange registration tabs "
        "use the existing Stacked City material family. The back carries readable FINISH lettering.\n\n"
        "Metres, ground-centre pivot. Unity size **11.30 × 5.78 × 1.08 m**, front **-Z**, "
        "column centres X=±5.08 m. A **9.30 × 4.30 m** aperture stays completely open. "
        "No floor bar, suspended low part, rig, animation, collider, light, runtime text or font file.\n\n"
        f"Budget: **{report['triangles']} triangles**, **{report['mesh_renderers']} material meshes**, "
        f"**{report['bytes']} FBX bytes**. Signal geometry is merged separately as "
        "`CityFinishGate_SC_Cyan`; only that mesh should receive optional signal animation. "
        "Materials: SC_Concrete, SC_Metal, SC_White, SC_Orange, SC_Cyan.\n\n"
        "The generator checks solid component manifoldness and outward normals, both lettering "
        "directions, empty passage and competing coplanar material faces before exporting. "
        "Text is 27 mm ahead of the lintel face. The preview is an asset inspection, not gameplay acceptance. "
        "Unity import, binding, collision preservation and real game-camera verification remain the integrator's responsibility.\n\n"
        "Chinese text uses outlines of the installed SimHei font. Latin text uses the existing EchoRun Sans SC "
        "(Noto Sans CJK SC subset, SIL OFL 1.1). No font files, textures or external models are redistributed.\n",
        encoding="utf-8")
    preview(root)
    print("STACKED_CITY_FINISH_GATE_OK " + json.dumps(report, ensure_ascii=False))


if __name__ == "__main__":
    main()
