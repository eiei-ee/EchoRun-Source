import math
from pathlib import Path

import bpy
from mathutils import Vector


PROJECT_ROOT = Path(__file__).resolve().parents[2]
FBX_PATH = PROJECT_ROOT / "Assets" / "Art" / "Pickups" / \
    "MemoryPulseShard" / "Models" / "MemoryPulseShard_B.fbx"
BLEND_PATH = PROJECT_ROOT / "ArtSource" / "Blender" / \
    "MemoryPulseShard_B.blend"
PREVIEW_PATH = PROJECT_ROOT / "ArtSource" / "Previews" / \
    "MemoryPulseShard_B_blender.png"

ROLE_COLORS = {
    "MP_Frame": (1.0, 0.0, 0.0, 0.90),
    "MP_Groove": (1.0, 0.0, 0.0, 0.04),
    "MP_Core": (0.0, 1.0, 0.0, 1.0),
    "MP_CoreTrace": (0.0, 0.36, 0.0, 0.82),
    "MP_Accent": (0.0, 0.0, 1.0, 1.0),
}


def reset_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (bpy.data.meshes, bpy.data.curves, bpy.data.materials,
                       bpy.data.cameras, bpy.data.lights):
        for datablock in list(datablocks):
            if datablock.users == 0:
                datablocks.remove(datablock)


def make_material(name, color, metallic, roughness,
                  emission=None, emission_strength=0.0):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    material.diffuse_color = (*color, 1.0)
    material.metallic = metallic
    material.roughness = roughness
    principled = material.node_tree.nodes.get("Principled BSDF")
    principled.inputs["Base Color"].default_value = (*color, 1.0)
    principled.inputs["Metallic"].default_value = metallic
    principled.inputs["Roughness"].default_value = roughness
    if emission is not None:
        emission_input = principled.inputs.get("Emission Color") or \
            principled.inputs.get("Emission")
        if emission_input is not None:
            emission_input.default_value = (*emission, 1.0)
        strength_input = principled.inputs.get("Emission Strength")
        if strength_input is not None:
            strength_input.default_value = emission_strength
    return material


def finish_object(obj, root, material, bevel=0.0):
    obj.parent = root
    obj.data.materials.append(material)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    if bevel > 0.0:
        modifier = obj.modifiers.new("HardSurfaceBevel", "BEVEL")
        modifier.width = bevel
        modifier.segments = 1
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    obj.select_set(False)
    return obj


def add_box(name, location, dimensions, material, root, bevel=0.012,
            rotation=(0.0, 0.0, 0.0)):
    bpy.ops.mesh.primitive_cube_add(location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    return finish_object(obj, root, material, bevel)


def add_bar_between(name, start, end, width, depth, material, root,
                    y=0.0, bevel=0.012):
    start_v = Vector((start[0], 0.0, start[1]))
    end_v = Vector((end[0], 0.0, end[1]))
    delta = end_v - start_v
    midpoint = (start_v + end_v) * 0.5
    angle = -math.atan2(delta.z, delta.x)
    return add_box(
        name, (midpoint.x, y, midpoint.z),
        (delta.length + 0.018, depth, width), material, root,
        bevel=bevel, rotation=(0.0, angle, 0.0))


def add_cylinder(name, location, radius, depth, material, root,
                 vertices=20, bevel=0.008):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices, radius=radius, depth=depth,
        location=location, rotation=(math.pi * 0.5, 0.0, 0.0))
    obj = bpy.context.object
    obj.name = name
    return finish_object(obj, root, material, bevel)


def add_arc_strip(name, radius_inner, radius_outer, start_degrees,
                  end_degrees, depth, material, root, y=0.0,
                  segments=8):
    vertices = []
    faces = []
    half_depth = depth * 0.5
    for index in range(segments + 1):
        t = index / segments
        angle = math.radians(start_degrees
                             + (end_degrees - start_degrees) * t)
        sine = math.sin(angle)
        cosine = math.cos(angle)
        for local_y in (-half_depth, half_depth):
            vertices.append((radius_inner * cosine, y + local_y,
                             radius_inner * sine))
            vertices.append((radius_outer * cosine, y + local_y,
                             radius_outer * sine))

    for index in range(segments):
        a = index * 4
        b = (index + 1) * 4
        faces.extend([
            (a, b, b + 2, a + 2),
            (a + 1, a + 3, b + 3, b + 1),
            (a, a + 1, b + 1, b),
            (a + 2, b + 2, b + 3, a + 3),
        ])
    faces.extend([
        (0, 2, 3, 1),
        (segments * 4, segments * 4 + 1,
         segments * 4 + 3, segments * 4 + 2),
    ])
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return finish_object(obj, root, material, 0.0)


def add_polygon_ring(name, radius, sides, width, depth, material, root,
                     y=-0.045, rotation_degrees=0.0):
    points = []
    for index in range(sides):
        angle = math.radians(rotation_degrees + 360.0 * index / sides)
        points.append((radius * math.cos(angle), radius * math.sin(angle)))
    for index in range(sides):
        add_bar_between(f"{name}_{index:02d}", points[index],
                        points[(index + 1) % sides], width, depth,
                        material, root, y=y, bevel=0.001)


def build_source():
    frame = make_material(
        "MP_Frame", (0.055, 0.070, 0.078), 0.86, 0.24)
    groove = make_material(
        "MP_Groove", (0.009, 0.014, 0.018), 0.62, 0.34)
    core = make_material(
        "MP_Core", (0.0, 0.08, 0.11), 0.28, 0.22,
        (0.0, 0.55, 0.70), 0.85)
    trace = make_material(
        "MP_CoreTrace", (0.0, 0.055, 0.070), 0.22, 0.28,
        (0.0, 0.24, 0.31), 0.34)
    accent = make_material(
        "MP_Accent", (0.96, 0.30, 0.025), 0.60, 0.23,
        (1.0, 0.22, 0.01), 3.0)

    root = bpy.data.objects.new("MemoryPulseShard_B_Source", None)
    root.empty_display_type = "CUBE"
    root.empty_display_size = 0.12
    root["asset_role"] = "collectible_memory_data"
    root["dimensions_m"] = "1.00 x 0.20 x 1.00"
    root["variant"] = "B_refined_layered_armor"
    root["pivot"] = "center"
    root["unity_contract"] = "visual_only_no_colliders"
    bpy.context.collection.objects.link(root)

    main_path = [
        (0.31, 0.44), (-0.27, 0.49), (-0.46, 0.25),
        (-0.43, -0.28), (-0.19, -0.48), (0.27, -0.44),
    ]
    for index in range(len(main_path) - 1):
        start = main_path[index]
        end = main_path[index + 1]
        add_bar_between(f"FrameUnderlay_{index:02d}", start, end,
                        0.105, 0.090, groove, root, y=0.018,
                        bevel=0.006)
        add_bar_between(f"FrameMain_{index:02d}", start, end,
                        0.074, 0.132, frame, root, y=0.0,
                        bevel=0.010)

    # A narrow rear spine and front face plates create the layered machined
    # profile from the references without making the frontal silhouette heavy.
    for index in range(len(main_path) - 1):
        start = main_path[index]
        end = main_path[index + 1]
        add_bar_between(f"RearSpine_{index:02d}", start, end,
                        0.052, 0.042, groove, root, y=0.082,
                        bevel=0.004)

    # A separated blade carries the broken-ring silhouette from the reference.
    add_bar_between("FrameDetachedUnderlay", (0.47, -0.32),
                    (0.52, -0.04), 0.095, 0.090, groove, root,
                    y=0.018, bevel=0.006)
    add_bar_between("FrameDetachedBlade", (0.47, -0.32),
                    (0.52, -0.04), 0.064, 0.132, frame, root,
                    bevel=0.009)
    add_bar_between("DetachedRearSpine", (0.47, -0.32),
                    (0.52, -0.04), 0.043, 0.042, groove, root,
                    y=0.082, bevel=0.004)

    # Front armor panels provide the layered hard-surface read without small
    # texture noise that would disappear from the runner camera.
    panel_segments = [
        ((0.25, 0.436), (-0.12, 0.466)),
        ((-0.31, 0.435), (-0.415, 0.29)),
        ((-0.444, 0.18), (-0.425, -0.10)),
        ((-0.39, -0.30), (-0.21, -0.445)),
        ((-0.10, -0.455), (0.19, -0.427)),
    ]
    for index, (start, end) in enumerate(panel_segments):
        add_bar_between(f"ArmorPlate_{index:02d}", start, end,
                        0.052, 0.022, frame, root, y=-0.076,
                        bevel=0.004)
        add_bar_between(f"ArmorSeam_{index:02d}", start, end,
                        0.010, 0.008, groove, root, y=-0.092,
                        bevel=0.0)

    # Two cyan guide rails are enough to imply an active data channel. They
    # stay inside the C frame and never become a second luminous outline.
    add_bar_between("GuideRailLeft", (-0.399, 0.255),
                    (-0.405, -0.055), 0.014, 0.018, core, root,
                    y=-0.092, bevel=0.002)
    add_bar_between("GuideRailTop", (-0.105, 0.451),
                    (0.155, 0.426), 0.012, 0.018, core, root,
                    y=-0.092, bevel=0.002)
    add_bar_between("GuideRailBottom", (-0.115, -0.445),
                    (0.070, -0.430), 0.009, 0.016, core, root,
                    y=-0.092, bevel=0.001)

    # Flat memory wafer: the bevel is tiny so it cannot become a jelly dome.
    add_cylinder("MemoryWafer", (0.0, -0.014, 0.0), 0.272, 0.052,
                 core, root, vertices=32, bevel=0.006)
    add_cylinder("MemoryGlyph", (0.0, -0.044, 0.0), 0.075, 0.010,
                 core, root, vertices=6, bevel=0.002)
    add_polygon_ring("CoreCircuitInner", 0.115, 6, 0.006, 0.006,
                     trace, root, y=-0.045, rotation_degrees=30.0)
    add_polygon_ring("CoreCircuitOuter", 0.188, 12, 0.004, 0.005,
                     trace, root, y=-0.045, rotation_degrees=15.0)

    # Six independent lock modules and their recessed underlay make the inner
    # mechanism feel assembled rather than cut from one thick ring.
    lock_segments = ((8, 57), (68, 117), (128, 177),
                     (188, 237), (248, 297), (308, 352))
    for start, end in lock_segments:
        add_arc_strip(f"CoreGlow_{start}", 0.276, 0.292,
                      start + 3, end - 3, 0.036, core, root,
                      y=0.012, segments=7)
        add_arc_strip(f"CoreLockUnderlay_{start}", 0.294, 0.344,
                      start, end, 0.048, groove, root,
                      y=0.018, segments=8)
        add_arc_strip(f"CoreLock_{start}", 0.301, 0.333,
                      start + 2, end - 2, 0.072, frame, root,
                      y=-0.020, segments=8)

    # Panel cuts and recessed fasteners are intentionally sparse: enough for a
    # close-up reward, not enough to turn into noise from the runner camera.
    add_bar_between("DataCutTop", (-0.04, 0.455), (0.08, 0.447),
                    0.018, 0.169, groove, root, y=-0.004, bevel=0.0)
    add_bar_between("DataCutLeft", (-0.438, 0.02), (-0.430, -0.09),
                    0.018, 0.169, groove, root, y=-0.004, bevel=0.0)
    add_bar_between("DataCutBottom", (-0.04, -0.45), (0.10, -0.435),
                    0.018, 0.169, groove, root, y=-0.004, bevel=0.0)
    for index, (x, z) in enumerate(((-0.34, 0.35), (-0.405, 0.17),
                                     (-0.405, -0.10), (-0.34, -0.34),
                                     (-0.11, -0.435), (0.20, -0.405),
                                     (0.23, 0.405), (0.485, -0.13))):
        add_cylinder(f"Fastener_{index:02d}", (x, -0.086, z),
                     0.010, 0.014, groove, root, vertices=8, bevel=0.0)

    # Orange never encloses the core; it remains a small data identifier.
    add_bar_between("AccentTop", (0.18, 0.455), (0.26, 0.445),
                    0.042, 0.174, accent, root, y=-0.002,
                    bevel=0.003)
    add_bar_between("AccentBottom", (0.12, -0.445), (0.205, -0.432),
                    0.038, 0.174, accent, root, y=-0.002,
                    bevel=0.003)
    add_bar_between("AccentBlade", (0.486, -0.225), (0.505, -0.145),
                    0.024, 0.174, accent, root, y=-0.002,
                    bevel=0.002)
    # Source and export share the production scale. This keeps the authored
    # detail while fitting the existing one-metre pickup trigger envelope.
    root.scale = (0.92, 1.0, 0.92)
    return root


def make_export_mesh(source_root):
    bpy.context.view_layer.update()
    duplicates = []
    for source in source_root.children_recursive:
        if source.type != "MESH":
            continue
        duplicate = source.copy()
        duplicate.data = source.data.copy()
        duplicate.parent = None
        duplicate.matrix_world = source.matrix_world.copy()
        bpy.context.collection.objects.link(duplicate)
        duplicates.append(duplicate)

    bpy.ops.object.select_all(action="DESELECT")
    for duplicate in duplicates:
        duplicate.select_set(True)
    bpy.context.view_layer.objects.active = duplicates[0]
    bpy.ops.object.join()
    export_object = bpy.context.object
    export_object.name = "MemoryPulseShard_B"
    mesh = export_object.data
    mesh.name = "MemoryPulseShard_B_Mesh"

    color_layer = mesh.color_attributes.get("Color") or \
        mesh.color_attributes.new(
            name="Color", type="BYTE_COLOR", domain="CORNER")
    for polygon in mesh.polygons:
        material = mesh.materials[polygon.material_index]
        color = ROLE_COLORS.get(material.name, ROLE_COLORS["MP_Frame"])
        for loop_index in polygon.loop_indices:
            color_layer.data[loop_index].color = color

    uv_layer = mesh.uv_layers.get("MemoryPulseUV") or \
        mesh.uv_layers.new(name="MemoryPulseUV")
    for loop in mesh.loops:
        point = mesh.vertices[loop.vertex_index].co
        uv_layer.data[loop.index].uv = (
            max(0.0, min(1.0, (point.x + 0.56) / 1.12)),
            max(0.0, min(1.0, (point.z + 0.54) / 1.08)),
        )

    export_material = make_material(
        "MemoryPulse_VertexMask", (0.04, 0.055, 0.065), 0.72, 0.27)
    mesh.materials.clear()
    mesh.materials.append(export_material)
    for polygon in mesh.polygons:
        polygon.material_index = 0
    export_object.hide_render = True
    export_object["color_mask"] = "R=frame G=core B=accent A=surface shade"
    export_object["triangle_budget"] = "under 4500"
    return export_object


def export_fbx(export_object):
    FBX_PATH.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    export_object.hide_set(False)
    export_object.select_set(True)
    bpy.context.view_layer.objects.active = export_object
    bpy.ops.export_scene.fbx(
        filepath=str(FBX_PATH), use_selection=True,
        object_types={"MESH"}, apply_scale_options="FBX_SCALE_UNITS",
        apply_unit_scale=True, bake_space_transform=False,
        axis_forward="-Z", axis_up="Y", add_leaf_bones=False,
        mesh_smooth_type="FACE", use_mesh_modifiers=True,
        path_mode="AUTO", embed_textures=False, bake_anim=False)


def aim_at(obj, target):
    direction = Vector(target) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def create_preview(source_root, export_object):
    export_object.hide_render = True
    bpy.ops.mesh.primitive_plane_add(
        size=4.5, location=(0.0, 0.28, 0.0),
        rotation=(math.pi * 0.5, 0.0, 0.0))
    backdrop = bpy.context.object
    backdrop.name = "PreviewBackdrop"
    backdrop.data.materials.append(make_material(
        "PreviewBackdropMaterial", (0.0035, 0.006, 0.010), 0.0, 0.78))
    backdrop.hide_render = True

    bpy.ops.object.camera_add(location=(1.25, -3.05, 1.05))
    camera = bpy.context.object
    camera.name = "PreviewCamera"
    camera.data.lens = 68
    aim_at(camera, (0.0, 0.0, 0.0))
    bpy.context.scene.camera = camera

    bpy.ops.object.light_add(type="AREA", location=(-1.8, -2.2, 2.6))
    key = bpy.context.object
    key.name = "PreviewKey"
    key.data.energy = 720
    key.data.shape = "DISK"
    key.data.size = 2.4
    key.data.color = (0.68, 0.82, 1.0)
    aim_at(key, (0.0, 0.0, 0.0))

    bpy.ops.object.light_add(type="AREA", location=(1.9, -0.8, 1.2))
    rim = bpy.context.object
    rim.name = "PreviewRim"
    rim.data.energy = 620
    rim.data.size = 1.5
    rim.data.color = (0.16, 0.75, 1.0)
    aim_at(rim, (0.0, 0.0, 0.0))

    bpy.ops.object.light_add(type="AREA", location=(-1.2, -0.4, -1.7))
    fill = bpy.context.object
    fill.name = "PreviewFill"
    fill.data.energy = 340
    fill.data.size = 1.2
    fill.data.color = (1.0, 0.23, 0.055)
    aim_at(fill, (0.0, 0.0, -0.15))

    world = bpy.context.scene.world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = \
        (0.0025, 0.0045, 0.008, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.16

    scene = bpy.context.scene
    try:
        scene.render.engine = "BLENDER_EEVEE_NEXT"
    except TypeError:
        scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1100
    scene.render.resolution_y = 1100
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.filepath = str(PREVIEW_PATH)
    scene.render.film_transparent = False
    try:
        scene.view_settings.look = "AgX - Medium High Contrast"
    except TypeError:
        pass
    PREVIEW_PATH.parent.mkdir(parents=True, exist_ok=True)
    BLEND_PATH.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))
    bpy.ops.render.render(write_still=True)


def report(export_object):
    export_object.data.calc_loop_triangles()
    corners = [export_object.matrix_world @ Vector(corner)
               for corner in export_object.bound_box]
    minimum = Vector((min(p.x for p in corners), min(p.y for p in corners),
                      min(p.z for p in corners)))
    maximum = Vector((max(p.x for p in corners), max(p.y for p in corners),
                      max(p.z for p in corners)))
    size = maximum - minimum
    print(f"MEMORY_PULSE_FBX={FBX_PATH}")
    print(f"MEMORY_PULSE_BLEND={BLEND_PATH}")
    print(f"MEMORY_PULSE_PREVIEW={PREVIEW_PATH}")
    print("MEMORY_PULSE_EXPORT_MESHES=1")
    print("MEMORY_PULSE_EXPORT_MATERIALS=1")
    print(f"MEMORY_PULSE_TRIANGLES={len(export_object.data.loop_triangles)}")
    print("MEMORY_PULSE_BOUNDS_BLENDER="
          f"{size.x:.3f}x{size.y:.3f}x{size.z:.3f}")


def main():
    reset_scene()
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    source_root = build_source()
    export_object = make_export_mesh(source_root)
    export_fbx(export_object)
    create_preview(source_root, export_object)
    report(export_object)


if __name__ == "__main__":
    main()
