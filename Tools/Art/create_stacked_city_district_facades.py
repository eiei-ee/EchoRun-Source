"""Four authored district window families for EchoRun, metres, wall pivot at zero.

Run Blender --background --python Tools/Art/create_stacked_city_district_facades.py.
Blender -Y faces outward (Unity +Z under the existing import convention).
Only new district FBXs and their source/inspection artifacts are written.
"""
import importlib.util
import json
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location("district_stacked_kit", HERE / "create_stacked_city_kit.py")
kit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(kit)


def block(root, name, center, size, material="SC_Metal"):
    # Flat, small structural pieces avoid the old bay's bevel on every hidden edge.
    return kit.box(root, name, center, size, material, 0)


def outline(width, height, chamfer=.035):
    x, z = width / 2, height / 2
    c = min(chamfer, x * .25, z * .25)
    return [(-x + c, -z), (x - c, -z), (x, -z + c), (x, z - c),
            (x - c, z), (-x + c, z), (-x, z - c), (-x, -z + c)]


def frame(root, name, center, width, height, border, depth=.19,
          material="SC_Concrete", chamfer=.04):
    """Closed chamfered reveal ring, with no solid slab behind its opening."""
    cx, cz = center
    outer = outline(width, height, chamfer)
    inner = outline(width - 2 * border, height - 2 * border, chamfer * .5)
    rings = [(outer, -.012), (outer, -depth), (inner, -.012), (inner, -depth)]
    vertices = [(cx + x, y, cz + z) for contour, y in rings for x, z in contour]
    faces = []
    n = len(outer)
    for i in range(n):
        j = (i + 1) % n
        faces.extend([(i, j, n + j, n + i),
                      (n + i, n + j, 3 * n + j, 3 * n + i),
                      (3 * n + i, 3 * n + j, 2 * n + j, 2 * n + i),
                      (2 * n + i, 2 * n + j, j, i)])
    return kit.mesh(root, name, vertices, faces, material)


def pane(root, name, x, z, width, height):
    return block(root, name, (x, -.054, z), (width, .030, height), "SC_Glass")


def rain_hood(root, name, width, top_z, material="SC_Concrete"):
    # A real falling profile: the front lip is 9 cm lower than the wall fixing.
    x = width / 2
    vertices = [(-x, -.016, top_z), (x, -.016, top_z),
                (x, -.30, top_z - .09), (-x, -.30, top_z - .09),
                (-x, -.016, top_z - .065), (x, -.016, top_z - .065),
                (x, -.30, top_z - .155), (-x, -.30, top_z - .155)]
    faces = [(0, 1, 2, 3), (7, 6, 5, 4), (0, 4, 5, 1),
             (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)]
    return kit.mesh(root, name, vertices, faces, material)


def residential():
    root = kit.group("FacadeResidential")
    for side in (-1, 1):
        x = side * .65
        frame(root, "Paired domestic white surround", (x, -.04), 1.15, 1.76,
              .10, .16, "SC_White")
        pane(root, "Domestic glazing", x, -.04, .95, 1.56)
        block(root, "Opening window midrail", (x, -.102, .10), (.95, .045, .062))
        block(root, "Canopy end bracket", (side * 1.17, -.14, .81), (.075, .18, .20), "SC_White")
    rain_hood(root, "Ochre residential sun canopy", 2.82, 1.04, "SC_Orange")
    block(root, "Continuous projecting sill", (0, -.158, -.955), (2.65, .285, .11), "SC_White")
    return root


def civic():
    root = kit.group("FacadeCivic")
    frame(root, "Deep civic picture-window reveal", (0, .25), 2.98, 1.48,
          .16, .29, "SC_Concrete", .065)
    pane(root, "Horizontal public-hall glazing", 0, .25, 2.66, 1.16)
    for x in (-.89, 0, .89):
        block(root, "Broad glazing mullion", (x, -.132, .25), (.060, .125, 1.16), "SC_White")
    block(root, "Civic lower stone spandrel", (0, -.067, -.785), (2.64, .11, .43), "SC_Concrete")
    block(root, "Spandrel shadow reveal", (0, -.144, -.98), (2.71, .11, .065))
    block(root, "Small civic enamel badge", (-1.04, -.146, -.735), (.21, .04, .10), "SC_Orange")
    return root


def transit():
    root = kit.group("FacadeTransit")
    # The silver rails occupy Z=[.93,1.04] and [-1.04,-.93]. End the metal
    # reveal at their inner faces; extending it to +/-1.04 creates 0.07155 m2
    # coplanar dark/white top and bottom patches after city meshes are combined.
    frame(root, "Transit portal frame", (0, 0), 2.78, 1.86,
          .085, .145, "SC_Metal", .025)
    pane(root, "Tall station glazing", 0, 0, 2.61, 1.91)
    for x in (-1.18, -.40, .40, 1.18):
        # Recess the fin tips 10 mm from both rail inner edges. Their visible
        # end caps then have one material owner, including distant views.
        block(root, "Deep vertical metal sun fin", (x, -.178, 0), (.055, .265, 1.84))
    block(root, "Station silver head rail", (0, -.173, .985), (2.65, .11, .11), "SC_White")
    block(root, "Station silver sill rail", (0, -.173, -.985), (2.65, .11, .11), "SC_White")
    block(root, "Route-colour lintel tab", (-.86, -.241, .985), (.50, .02, .068), "SC_Orange")
    return root


def heritage():
    root = kit.group("FacadeHeritage")
    for side in (-1, 1):
        x = side * .65
        frame(root, "Narrow masonry window surround", (x, -.06), .94, 1.78,
              .115, .185, "SC_Concrete", .09)
        pane(root, "Narrow divided glazing", x, -.06, .71, 1.55)
        block(root, "Vertical heritage sash", (x, -.11, -.06), (.046, .052, 1.55), "SC_White")
        for z in (-.325, .205):
            block(root, "Three-light sash crossbar", (x, -.11, z), (.71, .052, .047), "SC_White")
        block(root, "Separate masonry sill", (x, -.171, -.986), (1.06, .29, .11), "SC_Concrete")
    rain_hood(root, "Continuous sloping rain hood", 2.70, 1.04)
    for side in (-1, 1):
        block(root, "Rain hood support iron", (side * 1.14, -.15, .765), (.045, .15, .15))
    return root


def validate_and_export(root):
    for obj in root.children_recursive:
        if obj.type != "MESH":
            continue
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        bm.to_mesh(obj.data)
        bm.free()
        obj.data.update()
    entry = kit.export(root)
    width, height, depth = entry["unity_size_xyz_m"]
    assert width <= 3.05 and height <= 2.20 and depth <= .32, entry
    assert entry["blender_max_xyz_m"][1] <= -.011, entry
    assert entry["triangles"] <= 864, entry
    entry["wall_pivot"] = "Centred at opening; Blender -Y / Unity +Z faces outward"
    entry["wall_clearance_m"] = round(-entry["blender_max_xyz_m"][1], 4)
    entry["colliders"] = 0
    return entry


def preview(roots):
    # Two rows keep both shape and projecting profile legible in a source preview.
    for root, position in zip(roots, [(-1.85, 0, 1.46), (1.85, 0, 1.46),
                                       (-1.85, 0, -1.46), (1.85, 0, -1.46)]):
        root.location = position
    scene = bpy.context.scene
    bpy.ops.object.camera_add(location=(4.4, -22, 6.7))
    camera = bpy.context.object
    camera.name = "District facade inspection camera"
    camera.rotation_euler = (Vector((0, 0, .12)) - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 8.45
    scene.camera = camera
    for name, location, energy, size in [("Key softbox", (-4, -8, 9), 1700, 7),
                                        ("Fill softbox", (5, -5, 1), 700, 5)]:
        bpy.ops.object.light_add(type="AREA", location=location)
        light = bpy.context.object
        light.name = name
        light.data.energy, light.data.size = energy, size
        light.rotation_euler = (Vector((0, 0, 0)) - light.location).to_track_quat("-Z", "Y").to_euler()
    scene.world.use_nodes = True
    background = scene.world.node_tree.nodes["Background"]
    background.inputs["Color"].default_value = (.20, .235, .26, 1)
    background.inputs["Strength"].default_value = .65
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x, scene.render.resolution_y = 1400, 1100
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = str(kit.SOURCE_DIR / "StackedCityDistrictFacades.preview.png")
    scene.view_settings.view_transform = "AgX"
    bpy.ops.wm.save_as_mainfile(filepath=str(kit.SOURCE_DIR / "StackedCityDistrictFacades.blend"))
    bpy.ops.render.render(write_still=True)


def main():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.0
    kit.MODEL_DIR.mkdir(parents=True, exist_ok=True)
    kit.SOURCE_DIR.mkdir(parents=True, exist_ok=True)
    # Preview swatches follow the existing SC material family. Unity remaps slots.
    kit.material("SC_Concrete", (.77, .75, .68), .0, .65)
    kit.material("SC_Metal", (.078, .11, .13), .50, .32)
    kit.material("SC_Glass", (.019, .065, .09), .44, .17)
    kit.material("SC_White", (.88, .89, .85), .10, .45)
    kit.material("SC_Orange", (.85, .28, .055), .05, .48)
    roots = [residential(), civic(), transit(), heritage()]
    report = [validate_and_export(root) for root in roots]
    (kit.SOURCE_DIR / "StackedCityDistrictFacades.manifest.json").write_text(
        json.dumps(report, indent=2), encoding="utf-8")
    rows = "\n".join(f"| {r['asset']} | {' x '.join(str(v) for v in r['unity_size_xyz_m'])} | {r['triangles']} | {r['mesh_renderers']} |" for r in report)
    (kit.SOURCE_DIR / "StackedCityDistrictFacades.README.md").write_text(
        "# Stacked City District Facades\n\n"
        "Original meshes authored locally with `Tools/Art/create_stacked_city_district_facades.py`. "
        "No downloaded art, generated image texture, third-party model or font is included.\n\n"
        "Four distinct forms: residential paired windows and ochre sun canopy; civic horizontal "
        "grouped glazing and deep reveal; transit tall glazing and projecting metal fins; heritage "
        "narrow divided sashes with a sloping rain hood.\n\n"
        "Same centered wall-mount pivot and FBX convention as FacadeBay: Blender -Y outward, "
        "export -Z forward / Y up, unit scale metres. All back surfaces are at least 12 mm away "
        "from the nominal wall. Reveal rings have open apertures instead of coplanar glass overlays. "
        "Geometry is static, merged by existing SC material, opaque, with no collider or animation. "
        "Unity must remap material slots and choose the family in the architecture placement pass.\n\n"
        "The source blend and PNG are an inspection arrangement; every FBX is exported at origin "
        "before this arrangement is applied. Preview order: residential / civic above, transit / heritage below.\n\n"
        "| Asset | Unity XYZ bounds (m) | Triangles | Material meshes |\n"
        "| --- | --- | ---: | ---: |\n" + rows + "\n\n"
        "Budget reference: the existing FacadeBay has 864 triangles and four material meshes. "
        "This source preview verifies the asset family; actual gameplay acceptance is separate.\n",
        encoding="utf-8")
    preview(roots)
    print("STACKED_DISTRICT_FACADES_OK")


if __name__ == "__main__":
    main()
