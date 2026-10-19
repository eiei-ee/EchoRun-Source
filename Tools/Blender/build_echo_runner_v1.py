import bpy
import json
import math
import os
from mathutils import Vector


ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SOURCE_DIR = os.path.join(ROOT, "ArtSource", "EchoRunner")
MODEL_DIR = os.path.join(ROOT, "Assets", "Models", "EchoRunner")
DOCS_DIR = os.path.join(ROOT, "docs", "ConceptArt")

BLEND_PATH = os.path.join(SOURCE_DIR, "EchoRunner_v1.blend")
FBX_PATH = os.path.join(MODEL_DIR, "EchoRunner_v1.fbx")
PREVIEW_PATH = os.path.join(DOCS_DIR, "EchoRunner-Blender-v1.png")
STATS_PATH = os.path.join(SOURCE_DIR, "EchoRunner_v1-stats.json")

for path in (SOURCE_DIR, MODEL_DIR, DOCS_DIR):
    os.makedirs(path, exist_ok=True)


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (bpy.data.meshes, bpy.data.curves, bpy.data.armatures,
                       bpy.data.materials, bpy.data.cameras, bpy.data.lights):
        for datablock in list(datablocks):
            if datablock.users == 0:
                datablocks.remove(datablock)


def material(name, color, metallic=0.0, roughness=0.65, emission=None):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1.0)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    if emission:
        bsdf.inputs["Emission Color"].default_value = (*emission, 1.0)
        bsdf.inputs["Emission Strength"].default_value = 3.2
    return mat


def apply_transform(obj):
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    obj.select_set(False)


def bevel(obj, width=0.025, segments=2):
    mod = obj.modifiers.new("Soft tailoring", "BEVEL")
    mod.width = width
    mod.segments = segments
    mod.limit_method = "ANGLE"
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.modifier_apply(modifier=mod.name)
    obj.select_set(False)


def assign_part(obj, mat, bone):
    obj.data.materials.append(mat)
    group = obj.vertex_groups.new(name=bone)
    group.add(range(len(obj.data.vertices)), 1.0, "REPLACE")
    obj["deform_bone"] = bone
    parts.append(obj)
    return obj


def rounded_box(name, location, scale, mat, bone, bevel_width=0.025, rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    apply_transform(obj)
    bevel(obj, bevel_width, 2)
    return assign_part(obj, mat, bone)


def ellipsoid(name, location, scale, mat, bone, subdivisions=2):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdivisions, radius=1.0, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    apply_transform(obj)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    return assign_part(obj, mat, bone)


def limb(name, start, end, radius_start, radius_end, mat, bone, vertices=12):
    start = Vector(start)
    end = Vector(end)
    direction = end - start
    bpy.ops.mesh.primitive_cone_add(
        vertices=vertices,
        radius1=radius_start,
        radius2=radius_end,
        depth=direction.length,
        location=(start + end) * 0.5,
    )
    obj = bpy.context.object
    obj.name = name
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = direction.to_track_quat("Z", "Y")
    apply_transform(obj)
    bevel(obj, min(radius_start, radius_end) * 0.18, 2)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    return assign_part(obj, mat, bone)


def loft(name, rings, mat, bone, sides=16):
    vertices = []
    faces = []
    for z, rx, ry, y_offset in rings:
        for index in range(sides):
            angle = (2.0 * math.pi * index / sides) + math.pi / sides
            vertices.append((rx * math.cos(angle), y_offset + ry * math.sin(angle), z))
    for ring in range(len(rings) - 1):
        for index in range(sides):
            nxt = (index + 1) % sides
            a = ring * sides + index
            b = ring * sides + nxt
            c = (ring + 1) * sides + nxt
            d = (ring + 1) * sides + index
            faces.append((a, b, c, d))
    faces.append(tuple(reversed(range(sides))))
    top = (len(rings) - 1) * sides
    faces.append(tuple(top + i for i in range(sides)))
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    bevel(obj, 0.018, 2)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    return assign_part(obj, mat, bone)


def create_armature():
    armature_data = bpy.data.armatures.new("EchoRunner_Armature")
    rig = bpy.data.objects.new("EchoRunner_Rig", armature_data)
    bpy.context.collection.objects.link(rig)
    rig.show_in_front = True
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")

    def bone(name, head, tail, parent=None, connected=False):
        edit_bone = armature_data.edit_bones.new(name)
        edit_bone.head = head
        edit_bone.tail = tail
        edit_bone.use_deform = True
        if parent:
            edit_bone.parent = armature_data.edit_bones.get(parent)
            edit_bone.use_connect = connected
        return edit_bone

    bone("Hips", (0, 0, 0.94), (0, 0, 1.12))
    bone("Spine", (0, 0, 1.12), (0, 0, 1.34), "Hips", True)
    bone("Chest", (0, 0, 1.34), (0, 0, 1.58), "Spine", True)
    bone("Neck", (0, 0, 1.58), (0, 0, 1.72), "Chest", True)
    bone("Head", (0, 0, 1.72), (0, 0, 2.03), "Neck", True)

    bone("LeftUpperArm", (0.27, 0, 1.56), (0.53, 0, 1.38), "Chest")
    bone("LeftLowerArm", (0.53, 0, 1.38), (0.66, -0.01, 1.17), "LeftUpperArm", True)
    bone("LeftHand", (0.66, -0.01, 1.17), (0.70, -0.035, 1.08), "LeftLowerArm", True)
    bone("RightUpperArm", (-0.27, 0, 1.56), (-0.53, 0, 1.38), "Chest")
    bone("RightLowerArm", (-0.53, 0, 1.38), (-0.66, -0.01, 1.17), "RightUpperArm", True)
    bone("RightHand", (-0.66, -0.01, 1.17), (-0.70, -0.035, 1.08), "RightLowerArm", True)

    bone("LeftUpperLeg", (0.14, 0, 1.02), (0.14, 0, 0.65), "Hips")
    bone("LeftLowerLeg", (0.14, 0, 0.65), (0.14, 0, 0.26), "LeftUpperLeg", True)
    bone("LeftFoot", (0.14, 0, 0.26), (0.14, -0.12, 0.10), "LeftLowerLeg", True)
    bone("LeftToes", (0.14, -0.12, 0.10), (0.14, -0.29, 0.08), "LeftFoot", True)
    bone("RightUpperLeg", (-0.14, 0, 1.02), (-0.14, 0, 0.65), "Hips")
    bone("RightLowerLeg", (-0.14, 0, 0.65), (-0.14, 0, 0.26), "RightUpperLeg", True)
    bone("RightFoot", (-0.14, 0, 0.26), (-0.14, -0.12, 0.10), "RightLowerLeg", True)
    bone("RightToes", (-0.14, -0.12, 0.10), (-0.14, -0.29, 0.08), "RightFoot", True)

    bpy.ops.object.mode_set(mode="OBJECT")
    rig.select_set(False)
    return rig


def make_vertex_color_material(mesh_obj, source_materials, glow_material):
    color_layer = mesh_obj.data.color_attributes.new(name="Color", type="BYTE_COLOR", domain="CORNER")
    for polygon in mesh_obj.data.polygons:
        polygon_material = source_materials[polygon.material_index]
        color = tuple(polygon_material.diffuse_color[:3])
        glow_mask = 1.0 if polygon_material.name == glow_material.name else 0.0
        for loop_index in polygon.loop_indices:
            color_layer.data[loop_index].color = (*color, glow_mask)

    vertex_mat = bpy.data.materials.new("EchoRunner_VertexColor")
    vertex_mat.use_nodes = True
    nodes = vertex_mat.node_tree.nodes
    links = vertex_mat.node_tree.links
    bsdf = nodes.get("Principled BSDF")
    bsdf.inputs["Metallic"].default_value = 0.05
    bsdf.inputs["Roughness"].default_value = 0.72
    vertex_color = nodes.new("ShaderNodeVertexColor")
    vertex_color.layer_name = "Color"
    emission_strength = nodes.new("ShaderNodeMath")
    emission_strength.operation = "MULTIPLY"
    emission_strength.inputs[1].default_value = 3.0
    links.new(vertex_color.outputs["Color"], bsdf.inputs["Base Color"])
    links.new(vertex_color.outputs["Color"], bsdf.inputs["Emission Color"])
    links.new(vertex_color.outputs["Alpha"], emission_strength.inputs[0])
    links.new(emission_strength.outputs[0], bsdf.inputs["Emission Strength"])

    for polygon in mesh_obj.data.polygons:
        polygon.material_index = 0
    mesh_obj.data.materials.clear()
    mesh_obj.data.materials.append(vertex_mat)
    return vertex_mat


def look_at(obj, target):
    direction = Vector(target) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def render_preview(body):
    body.hide_render = True
    preview_objects = []
    for x, rotation, label in ((-1.45, 0.0, "FRONT"), (0.0, math.radians(90), "SIDE"), (1.45, math.pi, "BACK")):
        duplicate = body.copy()
        duplicate.data = body.data.copy()
        duplicate.animation_data_clear()
        duplicate.parent = None
        duplicate.rotation_mode = "XYZ"
        duplicate.modifiers.clear()
        duplicate.location = (x, 0, 0)
        duplicate.rotation_euler = (0, 0, rotation)
        duplicate.hide_render = False
        duplicate.name = "Preview_" + label
        bpy.context.collection.objects.link(duplicate)
        preview_objects.append(duplicate)

        bpy.ops.object.text_add(location=(x, 0.18, -0.10), rotation=(math.radians(90), 0, 0))
        text_obj = bpy.context.object
        text_obj.data.body = label
        text_obj.data.align_x = "CENTER"
        text_obj.data.size = 0.105
        text_obj.data.extrude = 0.003
        text_obj.data.materials.append(material("Label_" + label, (0.12, 0.16, 0.23), roughness=0.8))
        preview_objects.append(text_obj)

    bpy.ops.mesh.primitive_plane_add(size=12, location=(0, 0, -0.015))
    ground = bpy.context.object
    ground.name = "Preview_Ground"
    ground.data.materials.append(material("PreviewGround", (0.68, 0.72, 0.78), roughness=0.88))
    preview_objects.append(ground)

    world = bpy.context.scene.world or bpy.data.worlds.new("World")
    bpy.context.scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.08, 0.105, 0.15, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.5

    for name, location, energy, size in (
        ("Key", (-3.5, -4.5, 6.0), 1150, 4.0),
        ("Fill", (4.0, -2.0, 3.5), 850, 3.0),
        ("Rim", (0.0, 3.5, 4.5), 1050, 3.0),
    ):
        data = bpy.data.lights.new(name, "AREA")
        data.energy = energy
        data.shape = "DISK"
        data.size = size
        obj = bpy.data.objects.new(name, data)
        obj.location = location
        look_at(obj, (0, 0, 1.05))
        bpy.context.collection.objects.link(obj)
        preview_objects.append(obj)

    camera_data = bpy.data.cameras.new("PreviewCamera")
    camera = bpy.data.objects.new("PreviewCamera", camera_data)
    bpy.context.collection.objects.link(camera)
    camera.location = (0, -8.5, 1.04)
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = 4.65
    look_at(camera, (0, 0, 1.04))
    bpy.context.scene.camera = camera
    preview_objects.append(camera)

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1536
    scene.render.resolution_y = 768
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.render.filepath = PREVIEW_PATH
    scene.render.image_settings.color_mode = "RGBA"
    scene.view_settings.look = "AgX - Medium High Contrast"
    bpy.ops.render.render(write_still=True)

    for obj in preview_objects:
        if obj and obj.name in bpy.data.objects:
            bpy.data.objects.remove(obj, do_unlink=True)
    body.hide_render = False


def export_model(body, rig):
    bpy.ops.object.select_all(action="DESELECT")
    body.select_set(True)
    rig.select_set(True)
    bpy.context.view_layer.objects.active = body
    bpy.ops.export_scene.fbx(
        filepath=FBX_PATH,
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z",
        axis_up="Y",
        add_leaf_bones=False,
        use_armature_deform_only=True,
        bake_anim=False,
        path_mode="AUTO",
        embed_textures=False,
    )


clear_scene()
parts = []

navy = material("NavyFabric", (0.025, 0.075, 0.14), roughness=0.78)
graphite = material("Graphite", (0.035, 0.055, 0.085), metallic=0.08, roughness=0.62)
mid_blue = material("JacketBlue", (0.025, 0.18, 0.28), roughness=0.7)
skin = material("Skin", (0.58, 0.30, 0.17), roughness=0.78)
hair = material("Hair", (0.018, 0.022, 0.03), roughness=0.8)
sole = material("Sole", (0.018, 0.022, 0.028), roughness=0.95)
orange = material("SignalOrange", (1.0, 0.24, 0.025), roughness=0.55)
cyan = material("CyanGlow", (0.0, 0.8, 0.95), metallic=0.1, roughness=0.3, emission=(0.0, 0.8, 1.0))
eye = material("Eyes", (0.012, 0.018, 0.025), roughness=0.35)

# Athletic legs and running shoes.
for side, x, upper_bone, lower_bone, foot_bone in (
    ("L", 0.14, "LeftUpperLeg", "LeftLowerLeg", "LeftFoot"),
    ("R", -0.14, "RightUpperLeg", "RightLowerLeg", "RightFoot"),
):
    limb(side + "_Thigh", (x, 0, 1.02), (x, 0.008, 0.65), 0.102, 0.078, navy, upper_bone, 14)
    ellipsoid(side + "_KneePad", (x, -0.067, 0.635), (0.086, 0.045, 0.105), graphite, lower_bone, 2)
    limb(side + "_Shin", (x, 0.005, 0.64), (x, -0.005, 0.24), 0.073, 0.058, mid_blue, lower_bone, 14)
    limb(side + "_Ankle", (x, -0.005, 0.25), (x, -0.015, 0.14), 0.059, 0.063, sole, lower_bone, 12)
    rounded_box(side + "_Shoe", (x, -0.075, 0.105), (0.105, 0.195, 0.068), graphite, foot_bone, 0.040)
    rounded_box(side + "_Sole", (x, -0.085, 0.045), (0.12, 0.215, 0.025), sole, foot_bone, 0.018)
    rounded_box(side + "_HeelGlow", (x, 0.105, 0.11), (0.07, 0.012, 0.018), cyan, foot_bone, 0.006)

# Slim pelvis, jacket and vest rather than a cylindrical robot body.
loft("Pelvis", ((0.91, 0.205, 0.13, 0.0), (1.03, 0.225, 0.14, 0.0), (1.12, 0.21, 0.135, 0.0)), navy, "Hips", 16)
loft("Jacket", ((1.08, 0.205, 0.14, 0.0), (1.24, 0.245, 0.15, 0.0), (1.45, 0.275, 0.155, 0.0), (1.59, 0.255, 0.145, 0.0)), mid_blue, "Chest", 18)
rounded_box("JacketZipper", (0, -0.157, 1.38), (0.014, 0.010, 0.15), graphite, "Chest", 0.006)
rounded_box("LeftVestStrap", (0.145, -0.158, 1.40), (0.026, 0.014, 0.19), graphite, "Chest", 0.012, (0, math.radians(-6), math.radians(-4)))
rounded_box("RightVestStrap", (-0.145, -0.158, 1.40), (0.026, 0.014, 0.19), graphite, "Chest", 0.012, (0, math.radians(6), math.radians(4)))
rounded_box("ChestStatus", (0, -0.196, 1.44), (0.045, 0.012, 0.055), cyan, "Chest", 0.012)
rounded_box("WaistGlow", (0, -0.158, 1.095), (0.22, 0.012, 0.012), cyan, "Hips", 0.005)
rounded_box("LeftBuckle", (0.205, -0.155, 1.28), (0.018, 0.010, 0.028), orange, "Chest", 0.006)
rounded_box("RightBuckle", (-0.205, -0.155, 1.28), (0.018, 0.010, 0.028), orange, "Chest", 0.006)

# Compact courier backpack, close to the back silhouette.
rounded_box("Backpack", (0, 0.17, 1.39), (0.17, 0.065, 0.23), graphite, "Chest", 0.038)
rounded_box("BackpackGlow", (0, 0.272, 1.43), (0.075, 0.012, 0.085), cyan, "Chest", 0.014)
rounded_box("BackpackOrange", (0, 0.284, 1.32), (0.055, 0.010, 0.015), orange, "Chest", 0.006)

# A-pose arms with fabric sleeves and light gloves.
for side, sign, upper_bone, lower_bone, hand_bone in (
    ("L", 1, "LeftUpperArm", "LeftLowerArm", "LeftHand"),
    ("R", -1, "RightUpperArm", "RightLowerArm", "RightHand"),
):
    limb(side + "_UpperArm", (0.27 * sign, 0, 1.55), (0.53 * sign, 0, 1.38), 0.075, 0.062, mid_blue, upper_bone, 14)
    ellipsoid(side + "_Shoulder", (0.275 * sign, 0, 1.535), (0.085, 0.082, 0.085), mid_blue, upper_bone, 2)
    limb(side + "_Forearm", (0.53 * sign, 0, 1.38), (0.66 * sign, -0.01, 1.16), 0.063, 0.048, navy, lower_bone, 14)
    rounded_box(side + "_WristGlow", (0.635 * sign, -0.018, 1.205), (0.045, 0.052, 0.012), cyan, lower_bone, 0.007, (0, 0, math.radians(-31 * sign)))
    ellipsoid(side + "_Hand", (0.685 * sign, -0.025, 1.10), (0.052, 0.046, 0.082), graphite, hand_bone, 2)
    rounded_box(side + "_ShoulderTab", (0.275 * sign, -0.105, 1.53), (0.038, 0.015, 0.024), orange, upper_bone, 0.007)

# Human neck and face with a light cap/visor treatment.
limb("Neck", (0, 0, 1.58), (0, 0, 1.72), 0.085, 0.09, skin, "Neck", 16)
ellipsoid("Head", (0, -0.012, 1.84), (0.148, 0.128, 0.195), skin, "Head", 3)
ellipsoid("HairCap", (0, 0.018, 1.942), (0.152, 0.130, 0.105), hair, "Head", 2)
rounded_box("Visor", (0, -0.130, 1.938), (0.105, 0.010, 0.016), cyan, "Head", 0.012)
ellipsoid("LeftEye", (0.052, -0.130, 1.86), (0.013, 0.007, 0.010), eye, "Head", 1)
ellipsoid("RightEye", (-0.052, -0.130, 1.86), (0.013, 0.007, 0.010), eye, "Head", 1)
ellipsoid("Nose", (0, -0.139, 1.825), (0.015, 0.012, 0.024), skin, "Head", 1)
ellipsoid("LeftEar", (0.148, -0.005, 1.845), (0.018, 0.030, 0.045), skin, "Head", 1)
ellipsoid("RightEar", (-0.148, -0.005, 1.845), (0.018, 0.030, 0.045), skin, "Head", 1)
rounded_box("Mouth", (0, -0.140, 1.775), (0.035, 0.006, 0.005), eye, "Head", 0.004)
rounded_box("LeftBrow", (0.052, -0.133, 1.885), (0.025, 0.005, 0.006), hair, "Head", 0.004, (0, 0, math.radians(-6)))
rounded_box("RightBrow", (-0.052, -0.133, 1.885), (0.025, 0.005, 0.006), hair, "Head", 0.004, (0, 0, math.radians(6)))
rounded_box("Collar", (0, -0.025, 1.665), (0.105, 0.075, 0.020), graphite, "Neck", 0.012)

# Join into one skinned mesh while keeping per-part rigid weights.
bpy.ops.object.select_all(action="DESELECT")
for part in parts:
    part.select_set(True)
bpy.context.view_layer.objects.active = parts[0]
bpy.ops.object.join()
body = bpy.context.object
body.name = "EchoRunner_Body"
bpy.context.view_layer.objects.active = body
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

source_materials = list(body.data.materials)
make_vertex_color_material(body, source_materials, cyan)

rig = create_armature()
body.parent = rig
modifier = body.modifiers.new("EchoRunner Armature", "ARMATURE")
modifier.object = rig

for polygon in body.data.polygons:
    polygon.use_smooth = True

body.data.calc_loop_triangles()
triangle_count = len(body.data.loop_triangles)
vertex_count = len(body.data.vertices)
bone_count = len(rig.data.bones)

render_preview(body)

# Keep the source file clean: only the body, rig and packed palette remain.
bpy.ops.object.select_all(action="DESELECT")
body.select_set(True)
rig.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
export_model(body, rig)

stats = {
    "model": "Echo Runner v1",
    "height_m": round(max(v.co.z for v in body.data.vertices) - min(v.co.z for v in body.data.vertices), 3),
    "vertices": vertex_count,
    "triangles": triangle_count,
    "materials": len(body.data.materials),
    "deform_bones": bone_count,
    "blend": BLEND_PATH,
    "fbx": FBX_PATH,
    "color_source": "FBX vertex colors (Color, alpha = cyan glow mask)",
    "preview": PREVIEW_PATH,
}
with open(STATS_PATH, "w", encoding="utf-8") as handle:
    json.dump(stats, handle, ensure_ascii=False, indent=2)

print("ECHO_RUNNER_BUILD_OK")
print(json.dumps(stats, ensure_ascii=False))
