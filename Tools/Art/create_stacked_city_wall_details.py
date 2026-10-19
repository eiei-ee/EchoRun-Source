"""Original static wall-sign, service and small story-detail meshes.

Run Blender --background --python Tools/Art/create_stacked_city_wall_details.py.
Blender -Y faces out; after the kit FBX conversion Unity +Z faces out. All
modules are centered in X/height, rear mount plane is Unity Z=0. Font outlines
are converted here; no font object, texture or runtime text is exported.
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
LATIN_PATH = kit.ROOT / "Assets/Resources/Fonts/EchoRunSansSC-Regular.otf"
CHINESE_PATH = Path("C:/Windows/Fonts/simhei.ttf")
TEXT_RECORDS = []
FONTS = {}


def text(root, wording, x, z, height, width, material="SC_Metal", depth=.128):
    """Fit actual glyph bounds, keeping font aspect ratio and centered alignment."""
    curve = bpy.data.curves.new("Lettering " + wording, "FONT")
    curve.body = wording
    chinese = any(ord(c) > 127 for c in wording)
    curve.font = FONTS["chinese" if chinese else "latin"]
    font_name = curve.font.name
    curve.size = 1.0
    curve.resolution_u = 4 if chinese else 8
    curve.extrude = 0.0
    curve.fill_mode = "BOTH"
    obj = bpy.data.objects.new("Lettering " + wording, curve)
    bpy.context.collection.objects.link(obj)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.convert(target="MESH")
    # Native font X/Y maps to Blender X/Z; actual triangles remain planar.
    for vertex in obj.data.vertices:
        p = vertex.co.copy()
        vertex.co = (p.x, -depth, p.y)
    low = [min(v.co[i] for v in obj.data.vertices) for i in range(3)]
    high = [max(v.co[i] for v in obj.data.vertices) for i in range(3)]
    factor = min(height / (high[2] - low[2]), width / (high[0] - low[0]))
    for vertex in obj.data.vertices:
        vertex.co.x = (vertex.co.x - (low[0] + high[0]) / 2) * factor + x
        vertex.co.z = (vertex.co.z - (low[2] + high[2]) / 2) * factor + z
    # This remapping preserves native +Z glyph-front normal as Blender -Y.
    obj.data.update()
    kit.finish(obj, root, material)
    TEXT_RECORDS.append({"asset": root.name, "text": wording, "font": font_name,
                         "height_m": round((high[2] - low[2]) * factor, 4)})


def bar(root, name, x, z, width, height, material="SC_Orange", depth=.125):
    return kit.box(root, name, (x, -depth, z), (width, .012, height), material, .003)


def plate(root, width, height, dark=False):
    kit.box(root, "Folded back edge", (0, -.042, 0), (width, .084, height), "SC_Metal", .022)
    kit.box(root, "Enamel face", (0, -.099, 0), (width - .065, .050, height - .065),
            "SC_Metal" if dark else "SC_Concrete", .016)
    for side in (-1, 1):
        for up in (-1, 1):
            # Square-headed fasteners deliberately avoid collectible-like circles.
            kit.box(root, "Recessed square fixing", (side * (width / 2 - .085), -.126,
                    up * (height / 2 - .09)), (.035, .007, .035),
                    "SC_Concrete" if dark else "SC_Metal", .002)


def district(name, number, title, subtitle):
    root = kit.group(name)
    plate(root, 2.4, 6.8)
    bar(root, "District top register", -.90, 2.86, .26, .26)
    text(root, "SKYWARD", .18, 2.85, .19, 1.3)
    text(root, number, 0, 1.11, 2.28, 1.97)
    bar(root, "Rule below district number", 0, -.29, 1.98, .022, "SC_Metal")
    text(root, title, 0, -.80, .45, 1.96)
    text(root, subtitle, 0, -1.40, .20, 1.96)
    text(root, "LEVEL " + number, 0, -2.21, .29, 1.93)
    bar(root, "Orange level register", 0, -2.76, 1.98, .20)
    return root


def transit():
    root = kit.group("WallTransit")
    plate(root, 3.2, 1.8, dark=True)
    # Original head-on rail pictogram, without luminous or circular components.
    kit.box(root, "Rail pictogram shell", (-1.03, -.130, .05), (.55, .012, .68), "SC_White", .075)
    kit.box(root, "Rail pictogram windshield", (-1.03, -.138, .13), (.40, .010, .24), "SC_Metal", .032)
    bar(root, "Rail pictogram left rail", -1.17, -.39, .055, .27, "SC_White", .14)
    bar(root, "Rail pictogram right rail", -.89, -.39, .055, .27, "SC_White", .14)
    bar(root, "Rail pictogram carriage stripe", -1.03, -.12, .37, .045, "SC_Orange", .14)
    text(root, "轻轨环线", .42, .22, .34, 1.85, "SC_White")
    text(root, "SKY LOOP", .42, -.28, .20, 1.73, "SC_White")
    bar(root, "Route registration line", .42, -.57, 1.74, .045)
    return root


def service():
    root = kit.group("WallService")
    plate(root, 1.4, 1.6)
    kit.box(root, "Vent dark recess", (0, -.127, .08), (1.08, .024, .92), "SC_Metal", .018)
    for i in range(7):
        kit.box(root, "Downward rain louvre %02d" % i, (0, -.184, .47 - i * .13),
                (1.015, .115, .066), "SC_Concrete", .011, (math.radians(13), 0, 0))
    text(root, "AIR / 07-B", -.12, -.61, .095, .81)
    bar(root, "Service orange registry", .48, -.61, .13, .055)
    return root


def memory():
    root = kit.group("WallMemory")
    plate(root, 2.6, 2.2)
    text(root, "昨日路线", .23, .52, .37, 1.78)
    text(root, "已存档", .23, -.06, .37, 1.60)
    text(root, "ECHO ARCHIVE", .23, -.69, .15, 1.78)
    # Two offset orthogonal route traces: a quiet echo of the game's premise.
    for off, mat in ((0, "SC_Orange"), (.075, "SC_Metal")):
        bar(root, "Archive route vertical", -.99 + off, .01, .030, 1.19, mat)
        bar(root, "Archive route head", -.87 + off, .60, .27, .030, mat)
        bar(root, "Archive route foot", -.86 + off, -.58, .29, .030, mat)
    return root


def ground():
    root = kit.group("WallGround")
    plate(root, 2.6, 2.2, dark=True)
    text(root, "下一层", 0, .51, .41, 1.83, "SC_White")
    text(root, "也是地面", 0, -.11, .37, 1.92, "SC_White")
    # Stacked floor datum marks; no arrow or suggestion of an alternate route.
    for i, width in enumerate((.57, .79, 1.01)):
        bar(root, "Stacked floor datum", -.66 + width / 2, -.60 - i * .13,
            width, .025, "SC_Orange")
    text(root, "+ 01", .67, -.74, .20, .50, "SC_White")
    return root


def prism(root, name, points, material, depth=.052):
    vertices = [(x, -depth, z) for x, z in points] + [(x, 0, z) for x, z in points]
    n = len(points)
    faces = [tuple(range(n)), tuple(reversed(range(n, 2 * n)))]
    for i in range(n):
        j = (i + 1) % n
        faces.append((i, i + n, j + n, j))
    return kit.mesh(root, name, vertices, faces, material, .004)


def cat():
    root = kit.group("WallCat")
    # Original sitting-cat silhouette: asymmetric curled tail, pointed ears,
    # and slim orange eyes. The full silhouette fits 0.9 x 1.0 metres.
    points = [(-.36, -.49), (.17, -.49), (.31, -.45), (.41, -.35), (.44, -.21),
              (.41, -.12), (.35, -.10), (.31, -.15), (.31, -.25), (.25, -.30),
              (.21, -.30), (.20, -.02), (.15, .12), (.19, .26), (.17, .49),
              (.02, .37), (-.10, .39), (-.26, .49), (-.29, .26), (-.23, .12),
              (-.30, -.04), (-.36, -.20), (-.39, -.41)]
    prism(root, "Sleeping ledge cat relief", points, "SC_Metal")
    bar(root, "Cat orange left eye", -.159, .236, .066, .018, "SC_Orange", .057)
    bar(root, "Cat orange right eye", .012, .236, .066, .018, "SC_Orange", .057)
    # Off-white whisker and paw score marks are small flat manufactured inlays.
    bar(root, "Cat left paw score", -.18, -.38, .014, .095, "SC_Concrete", .057)
    bar(root, "Cat right paw score", -.075, -.38, .014, .095, "SC_Concrete", .057)
    vertices = [v for obj in root.children_recursive if obj.type == "MESH" for v in obj.data.vertices]
    for axis, size in ((0, .9), (2, 1.0)):
        low, high = min(v.co[axis] for v in vertices), max(v.co[axis] for v in vertices)
        for vertex in vertices:
            vertex.co[axis] = (vertex.co[axis] - (low + high) / 2) * size / (high - low)
    return root


def preview(roots):
    offsets = [(-5.8, 0, 0), (-3.0, 0, 0), (.43, 0, 2.40), (4.05, 0, 2.40),
               (.23, 0, -.22), (3.42, 0, -.22), (1.84, 0, -2.57)]
    for root, offset in zip(roots, offsets):
        root.location = offset
    scene = bpy.context.scene
    bpy.ops.object.camera_add(location=(-.95, -32, 1.2))
    camera = bpy.context.object
    camera.name = "Wall details inspection camera"
    camera.rotation_euler = (Vector((-.95, 0, 0)) - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 13.4
    scene.camera = camera
    bpy.ops.object.light_add(type="AREA", location=(-4, -9, 7))
    bpy.context.object.data.energy = 3200
    bpy.context.object.data.size = 8
    bpy.context.object.rotation_euler = (Vector((0, 0, 0)) - bpy.context.object.location).to_track_quat("-Z", "Y").to_euler()
    if scene.world is None:
        scene.world = bpy.data.worlds.new("Wall details inspection world")
    scene.world.use_nodes = True
    scene.world.node_tree.nodes["Background"].inputs["Color"].default_value = (.17, .22, .28, 1)
    scene.world.node_tree.nodes["Background"].inputs["Strength"].default_value = .6
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 2400
    scene.render.resolution_y = 1500
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = str(kit.SOURCE_DIR / "StackedCityWallDetails.preview.png")
    scene.view_settings.view_transform = "AgX"
    # Font curves have all been converted and can be removed from the authored
    # scene. This avoids embedding or carrying an OS font in the blend file.
    for curve in list(bpy.data.curves):
        if curve.users == 0:
            bpy.data.curves.remove(curve)
    for font in list(bpy.data.fonts):
        if font.name != "Bfont" and font.users == 0:
            bpy.data.fonts.remove(font)
    bpy.ops.wm.save_as_mainfile(filepath=str(kit.SOURCE_DIR / "StackedCityWallDetails.blend"))
    bpy.ops.render.render(write_still=True)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.0
    FONTS["latin"] = bpy.data.fonts.load(str(LATIN_PATH))
    FONTS["chinese"] = bpy.data.fonts.load(str(CHINESE_PATH))
    kit.material("SC_Concrete", (.77, .75, .68), .0, .65)
    kit.material("SC_Metal", (.045, .069, .084), .5, .32)
    kit.material("SC_Orange", (.85, .28, .055), .05, .48)
    kit.material("SC_White", (.90, .92, .91), .08, .44)
    roots = [district("WallDistrictA", "07", "空中街区", "ELEVATED DISTRICT"),
             district("WallDistrictB", "12", "层间通行", "CONNECTED LEVELS"),
             transit(), service(), memory(), ground(), cat()]
    report = []
    for root in roots:
        entry = kit.export(root)
        low, high = entry["blender_min_xyz_m"], entry["blender_max_xyz_m"]
        entry["unity_min_xyz_m"] = [low[0], low[2], -high[1]]
        entry["unity_max_xyz_m"] = [high[0], high[2], -low[1]]
        assert entry["unity_max_xyz_m"][2] <= .28, entry
        assert entry["triangles"] < 10000, entry
        report.append(entry)
    manifest = {"orientation": "Unity +Z outward; +Y height; rear mount Z=0", "modules": report,
                "lettering": TEXT_RECORDS,
                "font_provenance": {"chinese": "Installed SimHei C:/Windows/Fonts/simhei.ttf, rendered mesh outlines only",
                                    "latin": "Existing project EchoRun Sans SC Regular (Noto Sans CJK SC 2.004 subset), SIL OFL 1.1"}}
    (kit.SOURCE_DIR / "StackedCityWallDetails.manifest.json").write_text(json.dumps(manifest, indent=2,
                                                                                           ensure_ascii=False), encoding="utf-8")
    preview(roots)
    print("STACKED_CITY_WALL_DETAILS_OK")


if __name__ == "__main__":
    main()
