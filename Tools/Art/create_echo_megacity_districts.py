import math
from pathlib import Path

import bpy
from mathutils import Vector


PROJECT_ROOT = Path(__file__).resolve().parents[2]
MODEL_DIR = PROJECT_ROOT / "Assets" / "Art" / "Environment" / \
    "EchoMegacityDistricts" / "Models"
BLEND_PATH = PROJECT_ROOT / "ArtSource" / "Blender" / \
    "EchoMegacityDistricts.blend"
PREVIEW_PATH = PROJECT_ROOT / "ArtSource" / "Previews" / \
    "EchoMegacityDistricts_preview.png"


def reset_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (bpy.data.meshes, bpy.data.curves, bpy.data.materials,
                       bpy.data.cameras, bpy.data.lights):
        for datablock in list(datablocks):
            if datablock.users == 0:
                datablocks.remove(datablock)


def make_material(name, base_color, metallic, roughness,
                  emission_color=None, emission_strength=0.0):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    material.diffuse_color = (*base_color, 1.0)
    material.metallic = metallic
    material.roughness = roughness
    principled = next(
        node for node in material.node_tree.nodes
        if node.type == "BSDF_PRINCIPLED")
    principled.inputs["Base Color"].default_value = (*base_color, 1.0)
    principled.inputs["Metallic"].default_value = metallic
    principled.inputs["Roughness"].default_value = roughness
    emission_input = principled.inputs.get("Emission Color") or \
        principled.inputs.get("Emission")
    if emission_color is not None and emission_input is not None:
        emission_input.default_value = (*emission_color, 1.0)
    strength_input = principled.inputs.get("Emission Strength")
    if strength_input is not None:
        strength_input.default_value = emission_strength
    return material


def finish_object(obj, parent, material, bevel=0.08):
    obj.parent = parent
    obj.data.materials.append(material)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    if bevel > 0:
        modifier = obj.modifiers.new("EdgeBevel", "BEVEL")
        modifier.width = bevel
        modifier.segments = 2
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    obj.select_set(False)
    return obj


def add_box(name, location, dimensions, material, parent, bevel=0.08,
            rotation=(0.0, 0.0, 0.0)):
    bpy.ops.mesh.primitive_cube_add(location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.scale = tuple(value * 0.5 for value in dimensions)
    return finish_object(obj, parent, material, bevel)


def add_cylinder(name, location, radius, depth, material, parent,
                 rotation=(0.0, 0.0, 0.0), vertices=16, bevel=0.04):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices, radius=radius, depth=depth,
        location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    return finish_object(obj, parent, material, bevel)


def add_pipe(name, start, end, radius, material, parent, vertices=12):
    start_v = Vector(start)
    end_v = Vector(end)
    direction = end_v - start_v
    midpoint = (start_v + end_v) * 0.5
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices, radius=radius, depth=direction.length,
        location=midpoint)
    obj = bpy.context.object
    obj.name = name
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = direction.to_track_quat("Z", "Y")
    obj.rotation_mode = "XYZ"
    return finish_object(obj, parent, material, 0.025)


def add_wedge(name, location, dimensions, material, parent, lean=0.28):
    sx, sy, sz = (value * 0.5 for value in dimensions)
    inset = sx * lean
    vertices = [
        (-sx, -sy, -sz), (sx, -sy, -sz),
        (-sx, sy, -sz), (sx, sy, -sz),
        (-sx + inset, -sy, sz), (sx - inset, -sy, sz),
        (-sx + inset, sy, sz), (sx - inset, sy, sz),
    ]
    faces = [
        (0, 1, 3, 2), (0, 4, 5, 1), (2, 3, 7, 6),
        (0, 2, 6, 4), (1, 5, 7, 3), (4, 6, 7, 5),
    ]
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.location = location
    return finish_object(obj, parent, material, 0.07)


def add_window_grid(prefix, center_x, front_y, base_z, columns, rows,
                    spacing_x, spacing_z, material, parent, phase=0):
    for row in range(rows):
        for column in range(columns):
            if (row * 3 + column + phase) % 7 == 0:
                continue
            x = center_x + (column - (columns - 1) * 0.5) * spacing_x
            z = base_z + row * spacing_z
            add_box(f"{prefix}_Window_{row:02d}_{column:02d}",
                    (x, front_y, z), (0.42, 0.07, 0.18),
                    material, parent, 0.018)


def add_tower(prefix, x, y, width, depth, height, structure, dark,
              cyan, gold, parent, crown="cap", phase=0):
    add_wedge(prefix + "_Body", (x, y, height * 0.5 + 0.45),
              (width, depth, height), structure, parent,
              0.10 if height < 7.0 else 0.18)
    front_y = y - depth * 0.5 - 0.045
    add_box(prefix + "_FacadeInset", (x, front_y, height * 0.51 + 0.42),
            (width * 0.78, 0.10, height * 0.72), dark, parent, 0.035)

    columns = max(3, int(width / 0.75))
    rows = max(4, int(height / 0.72))
    add_window_grid(prefix, x, front_y - 0.065, 1.30, columns, rows,
                    min(0.68, width / (columns + 0.2)), 0.58,
                    cyan, parent, phase)

    for side in (-1, 1):
        rib_x = x + side * width * 0.46
        add_box(prefix + ("_RibL" if side < 0 else "_RibR"),
                (rib_x, front_y - 0.08, height * 0.51 + 0.42),
                (0.16, 0.16, height * 0.82), dark, parent, 0.03)

    for band in (0.30, 0.58, 0.82):
        add_box(prefix + f"_Band_{int(band * 100)}",
                (x, front_y - 0.10, 0.45 + height * band),
                (width * 0.91, 0.14, 0.12), dark, parent, 0.025)

    if crown == "cap":
        add_box(prefix + "_RoofCap", (x, y, height + 0.62),
                (width * 0.92, depth * 0.92, 0.34), dark, parent, 0.10)
        add_box(prefix + "_RoofGlow", (x, front_y - 0.12, height + 0.63),
                (width * 0.64, 0.08, 0.10), gold, parent, 0.018)
    else:
        add_wedge(prefix + "_Crown", (x, y, height + 0.90),
                  (width * 0.74, depth * 0.72, 1.00), dark, parent, 0.22)
        add_box(prefix + "_CrownGlow", (x, front_y + depth * 0.12,
                                        height + 0.90),
                (width * 0.44, 0.08, 0.18), gold, parent, 0.02)


def add_rooftop_cluster(prefix, x, y, z, structure, dark, cyan, parent):
    add_cylinder(prefix + "_Reactor", (x, y, z + 0.35), 0.38, 0.70,
                 dark, parent, vertices=16, bevel=0.05)
    add_cylinder(prefix + "_ReactorRing", (x, y, z + 0.36), 0.43, 0.10,
                 cyan, parent, vertices=16, bevel=0.02)
    add_pipe(prefix + "_Antenna", (x + 0.55, y, z),
             (x + 0.55, y, z + 1.60), 0.055, structure, parent)
    add_cylinder(prefix + "_Beacon", (x + 0.55, y, z + 1.66),
                 0.12, 0.20, cyan, parent, vertices=12, bevel=0.025)


def build_district(variant, materials):
    structure, dark, cyan, gold, coral = materials
    root = bpy.data.objects.new(f"EchoMegacityDistrict{variant}", None)
    root.empty_display_type = "CUBE"
    root.empty_display_size = 0.5
    root["asset_role"] = "authored_trackside_city_district"
    root["dimensions_m"] = "15 x 6 x 11.6"
    root["pivot"] = "bottom_center"
    root["variant"] = variant
    bpy.context.collection.objects.link(root)

    add_box("District_Plinth", (0, 0, 0.22), (15.0, 6.0, 0.44),
            dark, root, 0.12)
    add_box("District_UpperDeck", (0, -0.10, 0.55),
            (14.4, 5.55, 0.28), structure, root, 0.08)
    add_box("District_FrontTrim", (0, -2.91, 0.54),
            (13.2, 0.12, 0.18), cyan, root, 0.025)

    if variant == "A":
        towers = [
            ("Habitat", -4.75, 0.45, 4.35, 4.85, 8.20, "slope", 0),
            ("Transit", -0.35, 0.70, 3.55, 4.25, 5.25, "cap", 2),
            ("Core", 4.45, 0.70, 4.20, 4.45, 10.25, "slope", 4),
        ]
    else:
        towers = [
            ("Relay", -5.10, 0.65, 3.35, 4.20, 5.70, "cap", 3),
            ("Archive", -1.10, 0.45, 4.05, 4.85, 10.05, "slope", 1),
            ("Market", 3.25, 0.82, 3.70, 4.15, 7.10, "cap", 5),
            ("Beacon", 6.00, 1.10, 1.55, 3.30, 4.80, "slope", 0),
        ]

    for prefix, x, y, width, depth, height, crown, phase in towers:
        add_tower(prefix, x, y, width, depth, height,
                  structure, dark, cyan, gold, root, crown, phase)

    # A continuous utility spine ties the individual buildings into one
    # authored district silhouette instead of a loose pile of boxes.
    add_pipe("UtilitySpine", (-6.4, 2.20, 3.10), (6.4, 2.20, 3.10),
             0.19, dark, root, vertices=16)
    for x in (-5.7, -2.4, 1.0, 4.6):
        add_pipe(f"UtilityDrop_{x}", (x, 2.20, 3.10),
                 (x, 2.20, 0.72), 0.10, structure, root)
        add_cylinder(f"UtilityNode_{x}", (x, 2.20, 3.10),
                     0.27, 0.22, coral, root,
                     rotation=(math.radians(90), 0, 0), vertices=16,
                     bevel=0.025)

    # Large readable sign, visible from the gameplay camera.
    sign_x = -0.2 if variant == "A" else 3.2
    sign_z = 4.55 if variant == "A" else 5.55
    add_box("HoloSign_Back", (sign_x, -2.70, sign_z),
            (3.10, 0.16, 1.25), dark, root, 0.08)
    add_box("HoloSign_Top", (sign_x, -2.82, sign_z + 0.48),
            (2.65, 0.08, 0.10), cyan, root, 0.02)
    add_box("HoloSign_Diagonal", (sign_x - 0.45, -2.83, sign_z),
            (1.30, 0.08, 0.13), gold, root, 0.02,
            rotation=(0, math.radians(-28), 0))
    add_box("HoloSign_Core", (sign_x + 0.66, -2.83, sign_z - 0.05),
            (0.52, 0.08, 0.52), coral, root, 0.05)

    roof_x = 4.45 if variant == "A" else -1.10
    roof_z = 10.72 if variant == "A" else 10.52
    add_rooftop_cluster("MainRoof", roof_x, 0.7, roof_z,
                        structure, dark, cyan, root)

    # Angled foreground braces prevent a rectangular silhouette at road level.
    for side in (-1, 1):
        add_pipe("FrontBraceL" if side < 0 else "FrontBraceR",
                 (side * 6.95, -2.55, 0.72),
                 (side * 5.75, -2.55, 3.25),
                 0.14, structure, root, vertices=12)
        add_cylinder("BraceFootL" if side < 0 else "BraceFootR",
                     (side * 6.95, -2.55, 0.72), 0.28, 0.22,
                     dark, root, rotation=(math.radians(90), 0, 0),
                     vertices=16, bevel=0.03)

    return root


def merge_by_material(root):
    groups = {}
    for obj in root.children_recursive:
        if obj.type != "MESH" or not obj.data.materials:
            continue
        material = obj.data.materials[0]
        groups.setdefault(material.name, []).append(obj)

    for material_name, objects in groups.items():
        bpy.ops.object.select_all(action="DESELECT")
        for obj in objects:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = objects[0]
        bpy.ops.object.join()
        merged = bpy.context.object
        merged.name = material_name + "_Geometry"
        merged.parent = root
        merged.select_set(False)


def select_hierarchy(root):
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for child in root.children_recursive:
        child.select_set(True)
    bpy.context.view_layer.objects.active = root


def export_fbx(root, path):
    MODEL_DIR.mkdir(parents=True, exist_ok=True)
    select_hierarchy(root)
    bpy.ops.export_scene.fbx(
        filepath=str(path), use_selection=True,
        object_types={"EMPTY", "MESH"},
        apply_scale_options="FBX_SCALE_UNITS", apply_unit_scale=True,
        bake_space_transform=False, axis_forward="-Z", axis_up="Y",
        add_leaf_bones=False, mesh_smooth_type="FACE",
        use_mesh_modifiers=True, path_mode="AUTO", embed_textures=False)


def aim_at(obj, target):
    direction = Vector(target) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def create_preview(root_a, root_b):
    root_a.location.x = -8.2
    root_b.location.x = 8.2

    bpy.ops.mesh.primitive_plane_add(size=50, location=(0, 0, -0.02))
    ground = bpy.context.object
    ground.data.materials.append(make_material(
        "PreviewGround", (0.008, 0.015, 0.026), 0.05, 0.62))

    bpy.ops.object.camera_add(location=(20.5, -27.5, 16.0))
    camera = bpy.context.object
    camera.data.lens = 56
    aim_at(camera, (0, 0, 4.5))
    bpy.context.scene.camera = camera

    bpy.ops.object.light_add(type="AREA", location=(-8.0, -10.0, 18.0))
    key = bpy.context.object
    key.data.energy = 2100
    key.data.shape = "DISK"
    key.data.size = 9.0
    key.data.color = (0.66, 0.84, 1.0)
    aim_at(key, (0, 0, 4.0))

    bpy.ops.object.light_add(type="AREA", location=(10.0, -2.0, 10.0))
    rim = bpy.context.object
    rim.data.energy = 1400
    rim.data.size = 6.0
    rim.data.color = (1.0, 0.30, 0.08)
    aim_at(rim, (0, 0, 5.0))

    world = bpy.context.scene.world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = \
        (0.004, 0.008, 0.018, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.20

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1400
    scene.render.resolution_y = 800
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = str(PREVIEW_PATH)
    scene.view_settings.look = "AgX - Medium High Contrast"
    PREVIEW_PATH.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.render.render(write_still=True)

    root_a.location.x = 0
    root_b.location.x = 0


def report(root, path):
    mesh_objects = [obj for obj in root.children_recursive if obj.type == "MESH"]
    triangles = 0
    for obj in mesh_objects:
        obj.data.calc_loop_triangles()
        triangles += len(obj.data.loop_triangles)
    print(f"ECHO_DISTRICT_FBX={path}")
    print(f"ECHO_DISTRICT_ROOT={root.name}")
    print(f"ECHO_DISTRICT_RENDERERS={len(mesh_objects)}")
    print(f"ECHO_DISTRICT_TRIANGLES={triangles}")


def main():
    reset_scene()
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.0
    materials = (
        make_material("EchoStructure", (0.10, 0.16, 0.24), 0.58, 0.27),
        make_material("EchoDepth", (0.018, 0.035, 0.065), 0.38, 0.32),
        make_material("EchoCyan", (0.025, 0.56, 0.84), 0.22, 0.22,
                      (0.02, 0.72, 1.00), 5.5),
        make_material("EchoGold", (0.78, 0.34, 0.045), 0.42, 0.28,
                      (1.00, 0.22, 0.01), 4.0),
        make_material("EchoCoral", (0.80, 0.08, 0.045), 0.30, 0.30,
                      (1.00, 0.08, 0.025), 4.5),
    )
    root_a = build_district("A", materials)
    root_b = build_district("B", materials)
    merge_by_material(root_a)
    merge_by_material(root_b)

    path_a = MODEL_DIR / "EchoMegacityDistrictA.fbx"
    path_b = MODEL_DIR / "EchoMegacityDistrictB.fbx"
    export_fbx(root_a, path_a)
    export_fbx(root_b, path_b)
    create_preview(root_a, root_b)
    BLEND_PATH.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))
    report(root_a, path_a)
    report(root_b, path_b)
    print(f"ECHO_DISTRICT_BLEND={BLEND_PATH}")
    print(f"ECHO_DISTRICT_PREVIEW={PREVIEW_PATH}")


if __name__ == "__main__":
    main()
