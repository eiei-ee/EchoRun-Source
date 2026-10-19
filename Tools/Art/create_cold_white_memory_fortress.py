import argparse
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


ASSET_FOLDER = "ColdWhiteMemoryFortress"
SOURCE_BLEND = "ColdWhiteMemoryFortress_60m.blend"
KIT_PREVIEW = "ColdWhiteMemoryFortress_Kit.png"
GAMEPLAY_PREVIEW = "ColdWhiteMemoryFortress_Gameplay56.png"
PORTRAIT_PREVIEW = "ColdWhiteMemoryFortress_Portrait62.png"


def parse_args():
    parser = argparse.ArgumentParser(
        description="Generate the EchoRun cold-white memory fortress kit.")
    parser.add_argument(
        "--output-root", required=True,
        help="Project-shaped output root. No files are written elsewhere.")
    parser.add_argument(
        "--skip-render", action="store_true",
        help="Export FBX and .blend without rendering previews.")
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    return parser.parse_args(argv)


def output_paths(output_root):
    root = Path(output_root).resolve()
    return {
        "root": root,
        "models": root / "Assets" / "Art" / "Environment" /
        ASSET_FOLDER / "Models",
        "blend": root / "ArtSource" / "Blender" / SOURCE_BLEND,
        "kit_preview": root / "ArtSource" / "Previews" / KIT_PREVIEW,
        "gameplay_preview": root / "ArtSource" / "Previews" /
        GAMEPLAY_PREVIEW,
        "portrait_preview": root / "ArtSource" / "Previews" /
        PORTRAIT_PREVIEW,
    }


def reset_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (
            bpy.data.meshes, bpy.data.curves, bpy.data.materials,
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
    principled = material.node_tree.nodes.get("Principled BSDF")
    principled.inputs["Base Color"].default_value = (*base_color, 1.0)
    principled.inputs["Metallic"].default_value = metallic
    principled.inputs["Roughness"].default_value = roughness
    if emission_color is not None:
        emission = principled.inputs.get("Emission Color") or \
            principled.inputs.get("Emission")
        if emission is not None:
            emission.default_value = (*emission_color, 1.0)
        strength = principled.inputs.get("Emission Strength")
        if strength is not None:
            strength.default_value = emission_strength
    return material


def create_materials():
    return {
        "concrete": make_material(
            "MF_ColdConcrete", (0.80, 0.84, 0.85), 0.03, 0.72),
        "ceramic": make_material(
            "MF_CeramicLight", (0.94, 0.96, 0.96), 0.08, 0.38),
        "metal": make_material(
            "MF_MetalDark", (0.045, 0.058, 0.064), 0.68, 0.27),
        "recess": make_material(
            "MF_RecessBlack", (0.008, 0.011, 0.014), 0.18, 0.42),
        "phase": make_material(
            "MF_PhaseEmitter", (0.025, 0.34, 0.43), 0.18, 0.28,
            (0.02, 0.58, 0.70), 2.8),
        "road": make_material(
            "MF_RoadGraphite", (0.025, 0.032, 0.038), 0.12, 0.68),
        "road_inset": make_material(
            "MF_RoadInset", (0.16, 0.18, 0.19), 0.34, 0.48),
        "road_edge": make_material(
            "MF_RoadEdgeWhite", (0.72, 0.78, 0.79), 0.10, 0.46),
    }


def create_root(name, role, dimensions, pivot="bottom_center"):
    root = bpy.data.objects.new(name, None)
    root.empty_display_type = "CUBE"
    root.empty_display_size = 0.55
    root["asset_role"] = role
    root["dimensions_m"] = dimensions
    root["pivot"] = pivot
    root["phase_material"] = "MF_PhaseEmitter"
    bpy.context.collection.objects.link(root)
    return root


def finish_object(obj, parent, material, bevel=0.04, preserve=False):
    obj.parent = parent
    obj.data.materials.append(material)
    obj["preserve_batch"] = bool(preserve)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    if bevel > 0.0:
        modifier = obj.modifiers.new("EdgeBevel", "BEVEL")
        modifier.width = bevel
        modifier.segments = 1
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    obj.select_set(False)
    return obj


def add_box(name, location, dimensions, material, parent, bevel=0.04,
            rotation=(0.0, 0.0, 0.0), preserve=False):
    bpy.ops.mesh.primitive_cube_add(location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.scale = tuple(value * 0.5 for value in dimensions)
    return finish_object(obj, parent, material, bevel, preserve)


def add_cylinder(name, location, radius, depth, material, parent,
                 vertices=24, rotation=(0.0, 0.0, 0.0), bevel=0.035,
                 preserve=False):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices, radius=radius, depth=depth,
        location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    return finish_object(obj, parent, material, bevel, preserve)


def add_torus(name, location, major_radius, minor_radius, material, parent,
              scale=(1.0, 1.0, 1.0),
              rotation=(math.radians(90.0), 0.0, 0.0),
              major_segments=48, minor_segments=6, preserve=False):
    bpy.ops.mesh.primitive_torus_add(
        major_segments=major_segments, minor_segments=minor_segments,
        major_radius=major_radius, minor_radius=minor_radius,
        location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    return finish_object(obj, parent, material, 0.018, preserve)


def add_pipe(name, start, end, radius, material, parent,
             vertices=10, preserve=False):
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
    return finish_object(obj, parent, material, 0.012, preserve)


def add_arc_pipe(name, center, radius, start_degrees, end_degrees,
                 segments, pipe_radius, material, parent, vertices=10):
    points = []
    for index in range(segments + 1):
        factor = index / segments
        angle = math.radians(start_degrees
                             + (end_degrees - start_degrees) * factor)
        points.append((
            center[0] + math.cos(angle) * radius,
            center[1],
            center[2] + math.sin(angle) * radius))
    for index in range(segments):
        add_pipe(f"{name}_{index + 1:02d}", points[index],
                 points[index + 1], pipe_radius, material, parent,
                 vertices=vertices)


def add_prism(name, outline, bottom_z, top_z, material, parent,
              preserve=False):
    count = len(outline)
    vertices = [(x, y, bottom_z) for x, y in outline]
    vertices += [(x, y, top_z) for x, y in outline]
    faces = [tuple(range(count - 1, -1, -1)), tuple(range(count, count * 2))]
    for index in range(count):
        next_index = (index + 1) % count
        faces.append((index, next_index, count + next_index, count + index))
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return finish_object(obj, parent, material, 0.025, preserve)


def merge_batches(root):
    groups = {}
    for obj in list(root.children_recursive):
        if obj.type != "MESH" or not obj.data.materials:
            continue
        if obj.get("preserve_batch", False):
            continue
        material = obj.data.materials[0]
        groups.setdefault(material.name, []).append(obj)

    for material_name, objects in groups.items():
        bpy.ops.object.select_all(action="DESELECT")
        for obj in objects:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = objects[0]
        if len(objects) > 1:
            bpy.ops.object.join()
        merged = bpy.context.object
        merged.name = material_name + "_Geometry"
        merged.parent = root
        merged.select_set(False)


def build_cantilever(materials):
    root = create_root(
        "CantileverSlab_A", "over_track_cantilever",
        "28 x 12 x 12; minimum road clearance 8.0")
    add_box("Concrete_LeftPier", (-10.8, 0.6, 4.2), (4.0, 6.8, 8.4),
            materials["concrete"], root, 0.14)
    add_box("Concrete_RightPier", (10.8, -0.6, 4.2), (4.0, 6.8, 8.4),
            materials["concrete"], root, 0.14)
    add_box("Ceramic_Cantilever", (0.0, 0.0, 10.5), (28.0, 12.0, 2.4),
            materials["ceramic"], root, 0.16)
    add_box("Recess_Underside", (0.0, -0.2, 9.24), (16.0, 9.6, 0.20),
            materials["recess"], root, 0.025)
    add_box("Metal_InnerSpine", (0.0, 3.8, 10.0), (18.0, 1.0, 1.0),
            materials["metal"], root, 0.05)
    for x in (-7.0, 0.0, 7.0):
        add_box(f"Phase_Underside_{x:+.0f}", (x, -4.62, 9.12),
                (3.1, 0.12, 0.10), materials["phase"], root, 0.015)
    merge_batches(root)
    return root


def build_silo(materials):
    root = create_root(
        "MemorySilo_A", "trackside_memory_silo", "8 x 8 x 16")
    add_cylinder("Concrete_MainDrum", (0.0, 0.0, 7.3), 3.75, 14.6,
                 materials["concrete"], root, vertices=28, bevel=0.07)
    add_cylinder("Ceramic_Crown", (0.0, 0.0, 15.4), 3.95, 1.2,
                 materials["ceramic"], root, vertices=28, bevel=0.08)
    add_cylinder("Metal_Base", (0.0, 0.0, 0.45), 4.0, 0.9,
                 materials["metal"], root, vertices=28, bevel=0.06)
    add_box("Recess_FrontSlot", (0.0, -3.70, 8.0), (2.0, 0.22, 10.8),
            materials["recess"], root, 0.04)
    for z in (3.0, 7.5, 12.0):
        add_torus(f"Metal_Band_{z:.1f}", (0.0, 0.0, z), 3.74, 0.10,
                  materials["metal"], root,
                  scale=(1.0, 1.0, 1.0), rotation=(0.0, 0.0, 0.0),
                  major_segments=32, minor_segments=4, preserve=False)
    add_box("Phase_VerticalMemorySlit", (0.0, -3.84, 8.0),
            (0.18, 0.10, 9.8), materials["phase"], root, 0.012)
    merge_batches(root)
    return root


def build_archive_tower(materials):
    root = create_root(
        "ArchiveTower_A", "offset_archive_tower", "9 x 7 x 22")
    blocks = [
        ("Lower", (-0.25, 0.0, 3.3), (7.5, 6.2, 6.6), 5.0,
         materials["concrete"]),
        ("Middle", (0.45, 0.0, 10.0), (7.2, 5.8, 6.8), -9.0,
         materials["ceramic"]),
        ("Upper", (-0.55, 0.0, 18.0), (7.0, 5.5, 8.0), 13.0,
         materials["concrete"]),
    ]
    for label, location, dimensions, angle, material in blocks:
        add_box(f"{label}_ArchiveBlock", location, dimensions, material, root,
                0.13, rotation=(0.0, 0.0, math.radians(angle)))
        add_box(f"Recess_{label}",
                (location[0], -dimensions[1] * 0.5 - 0.08,
                 location[2] + 0.15),
                (dimensions[0] * 0.62, 0.14, dimensions[2] * 0.48),
                materials["recess"], root, 0.025,
                rotation=(0.0, 0.0, math.radians(angle)))
    add_box("Metal_TowerBase", (0.0, 0.0, 0.35), (8.8, 6.8, 0.7),
            materials["metal"], root, 0.08)
    for index, (x, z, angle) in enumerate(
            ((-1.2, 4.0, -18.0), (0.6, 10.6, 12.0),
             (-0.8, 17.6, -14.0))):
        add_box(f"Phase_ArchiveCrack_{index + 1}", (x, -3.28, z),
                (0.12, 0.10, 2.7), materials["phase"], root, 0.012,
                rotation=(0.0, math.radians(angle), 0.0))
    merge_batches(root)
    return root


def build_scan_ring(materials):
    root = create_root(
        "ScanRing_A", "phase_transition_scan_gate",
        "18 x 2.5 x 12; broken upper arch; clear road plane")
    add_box("Concrete_LeftFoot", (-7.7, 0.0, 2.2), (2.4, 2.5, 4.4),
            materials["concrete"], root, 0.10)
    add_box("Concrete_RightFoot", (7.7, 0.0, 2.2), (2.4, 2.5, 4.4),
            materials["concrete"], root, 0.10)
    # An open upper arc keeps the landmark identity without sweeping an
    # opaque torus across the player camera when they cross the gate.
    add_arc_pipe("Ceramic_OuterArc", (0.0, 0.0, 5.2), 6.6,
                 8.0, 172.0, 14, 0.30, materials["ceramic"], root,
                 vertices=8)
    add_box("Metal_LeftBrace", (-6.8, 0.0, 4.0), (0.45, 2.1, 6.0),
            materials["metal"], root, 0.04,
            rotation=(0.0, math.radians(-8.0), 0.0))
    add_box("Metal_RightBrace", (6.8, 0.0, 4.0), (0.45, 2.1, 6.0),
            materials["metal"], root, 0.04,
            rotation=(0.0, math.radians(8.0), 0.0))
    add_box("Recess_LeftSocket", (-7.7, -1.28, 2.3), (1.1, 0.12, 2.4),
            materials["recess"], root, 0.025)
    add_box("Recess_RightSocket", (7.7, -1.28, 2.3), (1.1, 0.12, 2.4),
            materials["recess"], root, 0.025)
    add_arc_pipe("Scan_PhaseEmitter", (0.0, -0.20, 5.2), 6.05,
                 12.0, 168.0, 16, 0.065, materials["phase"], root,
                 vertices=8)
    merge_batches(root)
    return root


def build_broken_overpass(materials):
    root = create_root(
        "BrokenOverpass_A", "deleted_memory_overpass",
        "24 x 8 x 10; minimum road clearance 8.0")
    add_box("Concrete_LeftSupport", (-10.4, 0.0, 4.1), (3.2, 6.0, 8.2),
            materials["concrete"], root, 0.12)
    add_box("Concrete_RightSupport", (10.4, 0.0, 4.1), (3.2, 6.0, 8.2),
            materials["concrete"], root, 0.12)
    add_box("Ceramic_LeftFragment", (-7.3, 0.0, 9.0), (9.4, 8.0, 1.6),
            materials["ceramic"], root, 0.10,
            rotation=(0.0, math.radians(-2.0), math.radians(1.5)))
    add_box("Ceramic_RightFragment", (8.2, 0.1, 9.15), (7.6, 7.5, 1.55),
            materials["ceramic"], root, 0.10,
            rotation=(0.0, math.radians(3.0), math.radians(-2.0)))
    add_box("Metal_LeftUnderframe", (-7.3, 0.0, 8.15), (8.6, 6.8, 0.32),
            materials["metal"], root, 0.04)
    add_box("Metal_RightUnderframe", (8.2, 0.0, 8.3), (6.8, 6.4, 0.32),
            materials["metal"], root, 0.04)
    add_box("Recess_LeftBreak", (-2.55, -3.5, 9.0), (0.28, 0.16, 1.2),
            materials["recess"], root, 0.02,
            rotation=(0.0, math.radians(-18.0), 0.0))
    add_box("Recess_RightBreak", (4.35, -3.3, 9.15), (0.28, 0.16, 1.2),
            materials["recess"], root, 0.02,
            rotation=(0.0, math.radians(18.0), 0.0))
    add_box("Phase_LeftDeletionEdge", (-2.42, -3.62, 9.0),
            (0.10, 0.08, 1.1), materials["phase"], root, 0.01,
            rotation=(0.0, math.radians(-18.0), 0.0))
    add_box("Phase_RightDeletionEdge", (4.22, -3.42, 9.15),
            (0.10, 0.08, 1.1), materials["phase"], root, 0.01,
            rotation=(0.0, math.radians(18.0), 0.0))
    merge_batches(root)
    return root


def build_facility(materials, variant):
    root = create_root(
        f"MechanicalFacility_{variant}", "low_trackside_mechanical_facility",
        "5 x 4 x 3.2")
    mirror = -1.0 if variant == "A" else 1.0
    add_box("Metal_Base", (0.0, 0.0, 0.28), (5.0, 4.0, 0.56),
            materials["metal"], root, 0.08)
    add_box("Concrete_ServiceBody", (0.35 * mirror, 0.1, 1.35),
            (3.9, 3.2, 2.15), materials["concrete"], root, 0.10)
    add_box("Ceramic_TopShell", (-0.35 * mirror, 0.0, 2.62),
            (3.2, 2.8, 0.75), materials["ceramic"], root, 0.09,
            rotation=(0.0, math.radians(7.0 * mirror), 0.0))
    add_box("Recess_ServicePanel", (0.35 * mirror, -1.55, 1.42),
            (2.4, 0.12, 1.15), materials["recess"], root, 0.025)
    for index in range(3):
        add_box(f"Metal_Vent_{index + 1}",
                (0.35 * mirror, -1.64, 1.1 + index * 0.32),
                (1.55, 0.07, 0.075), materials["metal"], root, 0.01)
    add_box("Phase_StatusSlit", (-1.35 * mirror, -1.66, 1.48),
            (0.13, 0.07, 1.25), materials["phase"], root, 0.01)
    merge_batches(root)
    return root


def add_road_memory_node(parent, y, materials, prefix="Node"):
    add_box(prefix + "_CrossScale", (0.0, y, 0.035),
            (10.2, 0.16, 0.035), materials["road_inset"], parent, 0.0)
    for x in (-4.5, -3.0, -1.5, 0.0, 1.5, 3.0, 4.5):
        add_box(prefix + f"_Tick_{x:+.1f}", (x, y - 0.28, 0.038),
                (0.035, 0.42, 0.03), materials["road_inset"], parent, 0.0)


def build_straight_road(materials):
    root = create_root(
        "RoadStraight_A", "visual_road_skin_straight_20m",
        "11 x 20 x 0.3; lanes -3 0 3; playable width 9",
        pivot="entry_center_surface")
    add_box("Road_GraphiteSurface", (0.0, 10.0, -0.12),
            (11.0, 20.0, 0.24), materials["road"], root, 0.025)
    for side in (-1.0, 1.0):
        add_box("Road_Edge_Left" if side < 0 else "Road_Edge_Right",
                (side * 5.32, 10.0, 0.015), (0.28, 20.0, 0.06),
                materials["road_edge"], root, 0.012)
    for x in (-1.5, 1.5):
        add_box(f"Road_LaneInset_{x:+.1f}", (x, 10.0, 0.018),
                (0.070, 19.2, 0.025), materials["road_inset"], root, 0.0)
    add_road_memory_node(root, 18.5, materials, "MemoryNode20")
    merge_batches(root)
    return root


def build_turn_right_road(materials):
    root = create_root(
        "RoadTurnRight_A", "visual_road_skin_right_turn_20m",
        "solid turn platform; route 10 forward + 10 right; width 11; lanes -3 0 3",
        pivot="entry_center_surface")
    outline = [
        (-5.5, 0.0), (10.0, 0.0),
        (10.0, 15.5), (-5.5, 15.5),
    ]
    add_prism("Road_GraphiteTurnSurface", outline, -0.24, 0.0,
              materials["road"], root)
    edge_segments = [
        ((-5.32, 0.0, 0.02), (-5.32, 15.45, 0.02)),
        ((9.82, 0.0, 0.02), (9.82, 4.55, 0.02)),
        ((5.32, 0.18, 0.02), (10.0, 0.18, 0.02)),
        ((-5.32, 15.32, 0.02), (10.0, 15.32, 0.02)),
    ]
    for index, (start, end) in enumerate(edge_segments):
        add_pipe(f"Road_Edge_{index + 1}", start, end, 0.13,
                 materials["road_edge"], root, vertices=8)
    seam_paths = [
        [(-1.5, 0.4), (-1.5, 7.8), (-1.25, 9.1),
         (-0.45, 10.4), (1.0, 11.3), (9.6, 11.5)],
        [(1.5, 0.4), (1.5, 7.2), (1.8, 8.1),
         (2.7, 8.6), (4.0, 8.5), (9.6, 8.5)],
    ]
    for path_index, points in enumerate(seam_paths):
        for segment_index in range(len(points) - 1):
            start = (*points[segment_index], 0.022)
            end = (*points[segment_index + 1], 0.022)
            add_pipe(f"Road_LaneInset_{path_index}_{segment_index}",
                     start, end, 0.035, materials["road_inset"], root,
                     vertices=8)
    add_road_memory_node(root, 2.0, materials, "TurnEntryNode")
    merge_batches(root)
    return root


def select_hierarchy(root):
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for child in root.children_recursive:
        child.select_set(True)
    bpy.context.view_layer.objects.active = root


def export_fbx(root, path):
    path.parent.mkdir(parents=True, exist_ok=True)
    select_hierarchy(root)
    bpy.ops.export_scene.fbx(
        filepath=str(path), use_selection=True,
        object_types={"EMPTY", "MESH"},
        apply_scale_options="FBX_SCALE_UNITS", apply_unit_scale=True,
        bake_space_transform=False, axis_forward="-Z", axis_up="Y",
        add_leaf_bones=False, mesh_smooth_type="FACE",
        use_mesh_modifiers=True, path_mode="AUTO", embed_textures=False)


def hierarchy_meshes(root):
    return [obj for obj in root.children_recursive if obj.type == "MESH"]


def root_report(root):
    meshes = hierarchy_meshes(root)
    triangles = 0
    points = []
    for obj in meshes:
        obj.data.calc_loop_triangles()
        triangles += len(obj.data.loop_triangles)
        points.extend(obj.matrix_world @ Vector(corner) for corner in obj.bound_box)
    minimum = tuple(min(point[index] for point in points) for index in range(3))
    maximum = tuple(max(point[index] for point in points) for index in range(3))
    size = tuple(maximum[index] - minimum[index] for index in range(3))
    return {
        "renderers": len(meshes),
        "triangles": triangles,
        "minimum": minimum,
        "maximum": maximum,
        "size": size,
        "materials": sorted({
            material.name for obj in meshes for material in obj.data.materials
            if material is not None}),
    }


def duplicate_hierarchy(source_root, name, location=(0.0, 0.0, 0.0),
                        rotation_z=0.0, scale=1.0):
    duplicate_root = source_root.copy()
    duplicate_root.name = name
    duplicate_root.data = None
    duplicate_root.location = location
    duplicate_root.rotation_euler = (0.0, 0.0, rotation_z)
    duplicate_root.scale = (scale, scale, scale)
    bpy.context.collection.objects.link(duplicate_root)
    mapping = {source_root: duplicate_root}
    for source in source_root.children_recursive:
        duplicate = source.copy()
        duplicate.data = source.data
        bpy.context.collection.objects.link(duplicate)
        duplicate.parent = mapping.get(source.parent, duplicate_root)
        mapping[source] = duplicate
    duplicate_root["preview_instance"] = True
    return duplicate_root


def delete_hierarchy(root):
    objects = list(root.children_recursive) + [root]
    for obj in reversed(objects):
        bpy.data.objects.remove(obj, do_unlink=True)


def aim_at(obj, target):
    direction = Vector(target) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def configure_world(color, strength):
    world = bpy.context.scene.world
    world.use_nodes = True
    background = world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = (*color, 1.0)
    background.inputs["Strength"].default_value = strength


def add_area_light(name, location, energy, size, color, target):
    bpy.ops.object.light_add(type="AREA", location=location)
    light = bpy.context.object
    light.name = name
    light.data.energy = energy
    light.data.shape = "DISK"
    light.data.size = size
    light.data.color = color
    aim_at(light, target)
    return light


def add_preview_ground(material, size=120.0, location=(0.0, 0.0, -0.28)):
    bpy.ops.mesh.primitive_plane_add(size=size, location=location)
    ground = bpy.context.object
    ground.name = "_PreviewGround"
    ground.data.materials.append(material)
    return ground


def set_render(path, resolution_x, resolution_y):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = resolution_x
    scene.render.resolution_y = resolution_y
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.filepath = str(path)
    scene.view_settings.look = "AgX - Medium High Contrast"
    path.parent.mkdir(parents=True, exist_ok=True)


def render_kit_preview(roots, materials, path):
    instances = [
        duplicate_hierarchy(roots["CantileverSlab_A"], "Kit_Cantilever",
                            (-18.0, 10.0, 0.0), scale=0.72),
        duplicate_hierarchy(roots["MemorySilo_A"], "Kit_Silo",
                            (9.0, 13.0, 0.0), scale=0.72),
        duplicate_hierarchy(roots["ArchiveTower_A"], "Kit_Tower",
                            (18.0, 12.0, 0.0), scale=0.62),
        duplicate_hierarchy(roots["ScanRing_A"], "Kit_Ring",
                            (-17.0, -12.0, 0.0), scale=0.72),
        duplicate_hierarchy(roots["BrokenOverpass_A"], "Kit_Bridge",
                            (4.0, -12.0, 0.0), scale=0.66),
        duplicate_hierarchy(roots["MechanicalFacility_A"], "Kit_FacilityA",
                            (17.0, -9.0, 0.0), scale=0.95),
        duplicate_hierarchy(roots["MechanicalFacility_B"], "Kit_FacilityB",
                            (22.5, -9.0, 0.0), scale=0.95),
    ]
    preview_material = make_material(
        "_PreviewNeutral", (0.42, 0.44, 0.45), 0.05, 0.78)
    ground = add_preview_ground(preview_material, 120.0)
    bpy.ops.object.camera_add(location=(46.0, -58.0, 38.0))
    camera = bpy.context.object
    camera.name = "_KitCamera"
    camera.data.lens = 52.0
    aim_at(camera, (1.0, 0.0, 6.0))
    bpy.context.scene.camera = camera
    key = add_area_light("_KitKey", (-24.0, -28.0, 42.0),
                         3900.0, 18.0, (0.90, 0.96, 1.0), (0.0, 0.0, 6.0))
    fill = add_area_light("_KitFill", (26.0, -2.0, 22.0),
                          2600.0, 14.0, (0.86, 0.91, 0.94), (0.0, 0.0, 7.0))
    configure_world((0.40, 0.43, 0.45), 0.68)
    set_render(path, 1600, 1000)
    bpy.ops.render.render(write_still=True)
    for obj in (camera, key, fill, ground):
        bpy.data.objects.remove(obj, do_unlink=True)
    for instance in instances:
        delete_hierarchy(instance)


def render_gameplay_preview(roots, materials, path, portrait_path):
    instances = [
        duplicate_hierarchy(roots["RoadStraight_A"], "Sample_Road_Final",
                            (10.0, 30.0, 0.0), rotation_z=math.radians(-90.0)),
        duplicate_hierarchy(roots["ScanRing_A"], "Sample_FinalRing",
                            (32.0, 30.0, 0.0), rotation_z=math.radians(-90.0),
                            scale=0.90),
        duplicate_hierarchy(roots["ArchiveTower_A"], "Sample_FinalTower",
                            (22.0, 39.5, 0.0), rotation_z=math.radians(-18.0),
                            scale=0.92),
        duplicate_hierarchy(roots["MemorySilo_A"], "Sample_FinalSilo",
                            (16.0, 21.0, 0.0), scale=0.90),
        duplicate_hierarchy(roots["MechanicalFacility_A"],
                            "Sample_FinalFacilityA", (17.0, 23.0, 0.0),
                            rotation_z=math.radians(90.0)),
        duplicate_hierarchy(roots["MechanicalFacility_B"],
                            "Sample_FinalFacilityB", (25.0, 36.8, 0.0),
                            rotation_z=math.radians(-90.0)),
    ]
    floor_material = make_material(
        "_PreviewVoid", (0.055, 0.064, 0.068), 0.03, 0.82)
    ground = add_preview_ground(floor_material, 180.0, (15.0, 30.0, -0.32))
    # Unity landscape gameplay framing: vertical FOV 56 degrees, player-relative
    # offset (0, 4.6, -8.2), facing the final +X approach after the turn.
    player = Vector((12.0, 30.0, 0.0))
    forward = Vector((1.0, 0.0, 0.0))
    camera_position = player - forward * 8.2 + Vector((0.0, 0.0, 4.6))
    bpy.ops.object.camera_add(location=camera_position)
    camera = bpy.context.object
    camera.name = "_Gameplay56Camera"
    camera.data.sensor_fit = "VERTICAL"
    camera.data.angle_y = math.radians(56.0)
    aim_at(camera, player + forward * 5.0 + Vector((0.0, 0.0, 1.0)))
    bpy.context.scene.camera = camera
    key = add_area_light("_GameplayKey", (8.0, 19.0, 24.0),
                         3600.0, 16.0, (0.88, 0.95, 1.0), (23.0, 30.0, 5.0))
    side = add_area_light("_GameplaySide", (34.0, 20.0, 13.0),
                          1700.0, 10.0, (1.0, 0.73, 0.42),
                          (25.0, 30.0, 5.0))
    fill = add_area_light("_GameplayFill", (12.0, 40.0, 15.0),
                          1300.0, 12.0, (0.82, 0.90, 0.95),
                          (23.0, 30.0, 4.0))
    configure_world((0.12, 0.15, 0.17), 0.50)
    set_render(path, 1600, 900)
    bpy.ops.render.render(write_still=True)
    # Portrait framing catches gate occlusion that a wide concept render hides.
    camera.data.angle_y = math.radians(62.0)
    set_render(portrait_path, 720, 1280)
    bpy.ops.render.render(write_still=True)
    for obj in (camera, key, side, fill, ground):
        bpy.data.objects.remove(obj, do_unlink=True)
    for instance in instances:
        delete_hierarchy(instance)


def build_all(materials):
    ordered = [
        build_cantilever(materials),
        build_silo(materials),
        build_archive_tower(materials),
        build_scan_ring(materials),
        build_broken_overpass(materials),
        build_facility(materials, "A"),
        build_facility(materials, "B"),
        build_straight_road(materials),
        build_turn_right_road(materials),
    ]
    return {root.name: root for root in ordered}


def main():
    args = parse_args()
    paths = output_paths(args.output_root)
    reset_scene()
    bpy.context.preferences.filepaths.save_version = 0
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.0
    materials = create_materials()
    roots = build_all(materials)

    for name, root in roots.items():
        path = paths["models"] / f"{name}.fbx"
        export_fbx(root, path)
        report = root_report(root)
        size = "x".join(f"{value:.3f}" for value in report["size"])
        minimum = ",".join(f"{value:.3f}" for value in report["minimum"])
        maximum = ",".join(f"{value:.3f}" for value in report["maximum"])
        print(f"MF_FBX={path}")
        print(f"MF_ASSET={name}")
        print(f"MF_RENDERERS={report['renderers']}")
        print(f"MF_TRIANGLES={report['triangles']}")
        print(f"MF_BOUNDS_MIN={minimum}")
        print(f"MF_BOUNDS_MAX={maximum}")
        print(f"MF_BOUNDS_SIZE={size}")
        print("MF_MATERIALS=" + ",".join(report["materials"]))

    if not args.skip_render:
        render_kit_preview(roots, materials, paths["kit_preview"])
        render_gameplay_preview(roots, materials, paths["gameplay_preview"],
                                paths["portrait_preview"])
        print(f"MF_KIT_PREVIEW={paths['kit_preview']}")
        print(f"MF_GAMEPLAY_PREVIEW={paths['gameplay_preview']}")
        print(f"MF_PORTRAIT_PREVIEW={paths['portrait_preview']}")

    paths["blend"].parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(paths["blend"]))
    print(f"MF_BLEND={paths['blend']}")
    print("MF_GENERATION_OK")


if __name__ == "__main__":
    main()
