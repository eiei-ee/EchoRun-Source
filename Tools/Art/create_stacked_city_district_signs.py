"""Author a varied neighbourhood sign catalogue as static, wall-mounted meshes.

Blender --background --python Tools/Art/create_stacked_city_district_signs.py
Uses the existing wall-kit font conversion and FBX coordinate convention.
No font file, runtime text, decal shader, light or gameplay direction is exported.
"""
import importlib.util
import json
from pathlib import Path

import bpy
from mathutils import Vector


HERE = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location("wall_details", HERE / "create_stacked_city_wall_details.py")
wall = importlib.util.module_from_spec(spec)
spec.loader.exec_module(wall)
kit = wall.kit

DISTRICTS = [
    ("DistrictSignXixia", "03", "栖霞里", "住宅街区", "住在风里", False, "SC_Orange"),
    ("DistrictSignYunting", "16", "云庭公社", "公共街区", "邻里共用", False, "SC_Foliage"),
    ("DistrictSignQinglan", "28", "青岚枢纽", "交通办公", "城市换乘", True, "SC_White"),
    ("DistrictSignZhexiang", "09", "折巷老街", "旧街生活", "老街新日常", False, "SC_Orange"),
]
NOTICES = [
    ("CityNoticeNoodle", "晚风面馆", "热汤供应", "街坊小店", 0),
    ("CityNoticeBookshop", "云间书屋", "今日有新书", "街坊小店", 1),
    ("CityNoticeTailor", "衣物修补", "旧物有新用", "邻里手作", 2),
    ("CityNoticePost", "层间邮局", "信会准时到", "邮政服务", 1),
    ("CityNoticeGarden", "屋顶菜园", "请留一片绿", "共同照料", 2),
    ("CityNoticeNightBus", "夜班车站", "末班也等你", "夜间服务", 0),
    ("CityNoticeWind", "晒被之前", "请看风向", "生活提醒", 2),
    ("CityNoticeNeighbours", "楼上楼下", "都是邻居", "邻里之间", 1),
    ("CityNoticeYesterday", "昨日走过", "今日相逢", "街角留言", 0),
    ("CityNoticeCat", "这层有猫", "小声经过", "邻里提醒", 2),
    ("CityNoticeBreakfast", "早安蒸铺", "热气刚刚好", "街坊小店", 1),
    ("CityNoticeLostAndFound", "失物招领", "也收迷路的伞", "邻里服务", 0),
]


def text(root, wording, x, z, height, width, material="SC_Metal", depth=.145):
    # A small but deliberate separation from the enamel prevents coincident
    # lettering surfaces; no depth bias or transparent material is needed.
    wall.text(root, wording, x, z, height, width, material, depth)


def district(entry):
    name, number, title, use, subtitle, dark, accent = entry
    root = kit.group(name)
    wall.plate(root, 2.4, 6.8, dark)
    ink = "SC_White" if dark else "SC_Metal"
    text(root, use, .12, 2.87, .19, 1.47, ink)
    wall.bar(root, "District registration square", -.93, 2.87, .18, .18, accent)
    text(root, number, 0, 1.29, 2.22, 1.97, ink)
    wall.bar(root, "District name rule", 0, -.13, 1.94, .035, accent)
    text(root, title, 0, -.84, .58, 1.98, ink)
    text(root, subtitle, 0, -1.57, .29, 1.95, ink)
    text(root, "街区 " + number, 0, -2.39, .24, 1.63, ink)
    # Distinct register pattern identifies each neighbourhood without arrows.
    register = int(number) % 4 + 1
    for i in range(register):
        width = 1.91 / register - .04
        wall.bar(root, "Neighbourhood datum %d" % i,
                 -.955 + (i + .5) * 1.91 / register, -2.89, width, .15, accent)
    return root


def notice(entry):
    name, title, subtitle, category, style = entry
    root = kit.group(name)
    dark = style == 0
    wall.plate(root, 2.6, 2.2, dark)
    ink = "SC_White" if dark else "SC_Metal"
    if style == 0:
        # Painted shop fascia: strong title, narrow enamel rules and a small label.
        text(root, category, 0, .76, .16, 1.76, ink)
        text(root, title, 0, .23, .43, 2.18, ink)
        text(root, subtitle, 0, -.40, .29, 2.15, ink)
        wall.bar(root, "Shop lower enamel line", 0, -.80, 1.93, .045, "SC_Orange")
    elif style == 1:
        # Public counter sign: dark header and larger light field below.
        wall.bar(root, "Public service heading strip", 0, .69, 2.30, .48, "SC_Metal", .137)
        text(root, category, 0, .69, .20, 1.94, "SC_White", .165)
        text(root, title, 0, .06, .43, 2.17, ink)
        text(root, subtitle, 0, -.53, .29, 2.13, ink)
        wall.bar(root, "Counter registry left", -.93, -.89, .31, .036, "SC_Orange")
        wall.bar(root, "Counter registry right", .93, -.89, .31, .036, "SC_Orange")
    else:
        # Neighbourhood note: asymmetrical margin, spacious two-line lettering.
        wall.bar(root, "Notice margin", -1.05, .02, .065, 1.69, "SC_Foliage")
        text(root, category, .09, .76, .16, 1.86, ink)
        text(root, title, .09, .22, .42, 1.94, ink)
        text(root, subtitle, .09, -.43, .29, 1.94, ink)
        wall.bar(root, "Notice footer", .10, -.86, 1.83, .028, "SC_Metal")
    return root


def preview(roots):
    for index, root in enumerate(roots[:4]):
        root.location = (-5.0 + index * 3.33, 0, 4.30)
    for index, root in enumerate(roots[4:]):
        root.location = (-5.0 + (index % 4) * 3.33, 0, -.50 - (index // 4) * 2.66)
    scene = bpy.context.scene
    bpy.ops.object.camera_add(location=(0, -35, .65))
    camera = bpy.context.object
    camera.name = "District sign catalogue inspection camera"
    camera.rotation_euler = (Vector((0, 0, .65)) - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 16.8
    scene.camera = camera
    bpy.ops.object.light_add(type="AREA", location=(-3, -10, 9))
    light = bpy.context.object
    light.data.energy = 4500
    light.data.size = 12
    light.rotation_euler = (Vector((0, 0, 1)) - light.location).to_track_quat("-Z", "Y").to_euler()
    scene.world = bpy.data.worlds.new("District catalogue inspection world")
    scene.world.use_nodes = True
    scene.world.node_tree.nodes["Background"].inputs["Color"].default_value = (.13, .17, .19, 1)
    scene.world.node_tree.nodes["Background"].inputs["Strength"].default_value = .7
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1200
    scene.render.resolution_y = 1400
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = str(kit.SOURCE_DIR / "StackedCityDistrictSigns.preview.png")
    scene.view_settings.view_transform = "AgX"
    for curve in list(bpy.data.curves):
        if curve.users == 0:
            bpy.data.curves.remove(curve)
    for font in list(bpy.data.fonts):
        if font.name != "Bfont" and font.users == 0:
            bpy.data.fonts.remove(font)
    bpy.ops.wm.save_as_mainfile(filepath=str(kit.SOURCE_DIR / "StackedCityDistrictSigns.blend"))
    bpy.ops.render.render(write_still=True)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.0
    kit.MODEL_DIR.mkdir(parents=True, exist_ok=True)
    kit.SOURCE_DIR.mkdir(parents=True, exist_ok=True)
    wall.FONTS["latin"] = bpy.data.fonts.load(str(wall.LATIN_PATH))
    wall.FONTS["chinese"] = bpy.data.fonts.load(str(wall.CHINESE_PATH))
    kit.material("SC_Concrete", (.77, .75, .68), 0, .65)
    kit.material("SC_Metal", (.045, .069, .084), .5, .32)
    kit.material("SC_Orange", (.85, .28, .055), .05, .48)
    kit.material("SC_White", (.90, .92, .91), .08, .44)
    kit.material("SC_Foliage", (.095, .245, .112), 0, .82)
    roots = [district(entry) for entry in DISTRICTS] + [notice(entry) for entry in NOTICES]
    report = []
    for index, root in enumerate(roots):
        entry = kit.export(root)
        low, high = entry["blender_min_xyz_m"], entry["blender_max_xyz_m"]
        entry["unity_min_xyz_m"] = [low[0], low[2], -high[1]]
        entry["unity_max_xyz_m"] = [high[0], high[2], -low[1]]
        assert entry["unity_max_xyz_m"][2] <= .20, entry
        assert entry["triangles"] < 10000, entry
        assert entry["mesh_renderers"] <= 4, entry
        if index >= 4:
            assert entry["unity_size_xyz_m"][0] <= 2.6 and entry["unity_size_xyz_m"][1] <= 2.2, entry
        report.append(entry)
    manifest = {
        "orientation": "Unity +Z outward; +Y height; rear mount Z=0",
        "districts": [{"model": e[0], "number": e[1], "title": e[2], "use": e[3], "subtitle": e[4]} for e in DISTRICTS],
        "notices": [{"model": e[0], "title": e[1], "subtitle": e[2], "category": e[3], "layout": e[4]} for e in NOTICES],
        "modules": report,
        "lettering": wall.TEXT_RECORDS,
        "font_provenance": {
            "chinese": "Installed SimHei C:/Windows/Fonts/simhei.ttf, rendered mesh outlines only; no font file embedded",
            "latin": "Existing project EchoRun Sans SC Regular (Noto Sans CJK SC 2.004 subset), SIL OFL 1.1; mesh outlines only"
        },
        "scope": "Original environmental shop and neighbourhood lettering. No gameplay arrows, lighting, colliders or runtime text."
    }
    (kit.SOURCE_DIR / "StackedCityDistrictSigns.manifest.json").write_text(
        json.dumps(manifest, indent=2, ensure_ascii=False), encoding="utf-8")
    preview(roots)
    print("STACKED_CITY_DISTRICT_SIGNS_OK assets=%d triangles=%d" % (len(report), sum(e["triangles"] for e in report)))


if __name__ == "__main__":
    main()
