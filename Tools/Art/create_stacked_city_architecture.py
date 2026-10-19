"""Authored close-range facade modules. Blender -Y faces outward (Unity +Z).

Run Blender --background --python Tools/Art/create_stacked_city_architecture.py.
Placement/fitting and material remapping are handled by StackedCityArchitecture.
"""
import importlib.util
import json
import math
from pathlib import Path
import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location("stacked_kit", HERE / "create_stacked_city_kit.py")
kit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(kit)


def lettering(root, text, x, z, size, material="SC_Metal"):
    curve = bpy.data.curves.new("Wayfinding lettering", "FONT")
    curve.body = text
    curve.align_x = "LEFT"
    curve.size = size
    curve.extrude = .003
    curve.resolution_u = 3
    obj = bpy.data.objects.new("Sign " + text, curve)
    bpy.context.collection.objects.link(obj)
    obj.location = (x, -.065, z)
    obj.rotation_euler = (math.pi / 2, 0, 0)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.convert(target="MESH")
    kit.finish(obj, root, material)


def bay():
    root = kit.group("FacadeBay")
    kit.box(root, "Shadow pocket", (0, -.029, 0), (2.65, .052, 1.87), "SC_Metal", .012)
    kit.box(root, "Recessed glass", (0, -.063, 0), (2.48, .024, 1.70), "SC_Glass", .009)
    kit.box(root, "Stone hood", (0, -.13, .955), (2.82, .26, .17), "SC_Concrete", .025)
    kit.box(root, "Stone sill", (0, -.13, -.955), (2.82, .26, .17), "SC_Concrete", .025)
    for side in (-1, 1):
        kit.box(root, "Return jamb", (side * 1.35, -.091, 0), (.12, .18, 1.82), "SC_Concrete", .018)
    kit.box(root, "Central mullion", (0, -.093, 0), (.055, .075, 1.72), "SC_Metal", .007)
    kit.box(root, "Interior ceiling strip", (0, -.085, .82), (2.42, .024, .025), "SC_Warm", .004)
    return root


def cornice():
    root = kit.group("FacadeCornice")
    kit.box(root, "Chamfered cornice", (0, -.145, 0), (1, .29, .18), "SC_Concrete", .018)
    kit.box(root, "Underside reveal", (0, -.038, -.10), (.985, .07, .04), "SC_Metal", .006)
    return root


def sign(number):
    root = kit.group("Wayfinding" + number)
    kit.box(root, "Porcelain enamel sign", (0, -.031, 0), (2.75, .06, 5.15), "SC_Concrete", .035)
    lettering(root, number, -1.07, .57, 1.82)
    lettering(root, "SKYWAY", -1.00, -.20, .28)
    lettering(root, "DISTRICT", -1.00, -.57, .23)
    lettering(root, "LEVEL  +04", -1.00, -1.15, .19)
    kit.box(root, "Orange destination strip", (0, -.075, -1.67), (2.10, .02, .20), "SC_Orange", .006)
    kit.box(root, "Divider", (-.12, -.071, .17), (1.82, .018, .016), "SC_Metal", .001)
    for side in (-1, 1):
        kit.box(root, "Inset mounting slot", (side * 1.21, -.068, 2.22), (.06, .018, .12), "SC_Metal", .006)
    return root


def main():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.context.scene.unit_settings.system = "METRIC"
    kit.material("SC_Concrete", (.77, .75, .68), .0, .65)
    kit.material("SC_Metal", (.078, .11, .13), .50, .32)
    kit.material("SC_Glass", (.019, .065, .09), .44, .17)
    kit.material("SC_Warm", (.95, .68, .31), .0, .38, .3)
    kit.material("SC_Orange", (.85, .28, .055), .05, .48)
    roots = [bay(), cornice(), sign("07"), sign("12"), sign("21")]
    report = [kit.export(root) for root in roots]
    (kit.SOURCE_DIR / "StackedCityArchitecture.manifest.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    for i, root in enumerate(roots):
        root.location = ((i - 2) * 4.0, 0, 0)
    bpy.ops.object.camera_add(location=(8, -24, 10))
    camera = bpy.context.object
    camera.rotation_euler = (Vector((0, 0, 0)) - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 21
    scene = bpy.context.scene
    scene.camera = camera
    bpy.ops.object.light_add(type="AREA", location=(-3, -6, 8))
    bpy.context.object.data.energy = 2600
    bpy.context.object.data.size = 10
    bpy.context.object.rotation_euler = (Vector((0, 0, 0)) - bpy.context.object.location).to_track_quat("-Z", "Y").to_euler()
    scene.world.use_nodes = True
    scene.world.node_tree.nodes["Background"].inputs["Strength"].default_value = .6
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1400
    scene.render.resolution_y = 750
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = str(kit.SOURCE_DIR / "StackedCityArchitecture.preview.png")
    scene.view_settings.view_transform = "AgX"
    bpy.ops.wm.save_as_mainfile(filepath=str(kit.SOURCE_DIR / "StackedCityArchitecture.blend"))
    bpy.ops.render.render(write_still=True)
    print("STACKED_ARCHITECTURE_MODELS_OK")


if __name__ == "__main__":
    main()
