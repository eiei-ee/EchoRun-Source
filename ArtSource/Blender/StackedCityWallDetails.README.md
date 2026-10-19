# Stacked City wall details

Seven formal static meshes add readable district identity, transit information,
maintenance detail and small environmental jokes to EchoRun's suspended city.
They are architecture dressing, with no navigation/collision or reward behavior.

## Authoring and exports

- Source generator: `Tools/Art/create_stacked_city_wall_details.py`
- Editable inspection source: `ArtSource/Blender/StackedCityWallDetails.blend`
- Model exports: `Assets/Art/StackedCity/Models/Wall*.fbx`
- Exact bounds, lettering and geometry counts: `StackedCityWallDetails.manifest.json`
- Authored asset preview: `StackedCityWallDetails.preview.png`

Run the generator with Blender 5.2.1 in background mode. It reuses the existing
Stacked City kit's mesh, bevel, material merge and FBX export helpers. Each model
exports at the origin in metres; the `.blend` moves the models only for its
inspection sheet. Unity local +Z faces out, +Y is vertical, X is horizontal.
All local rear mount planes are Z=0; bounds are centered horizontally/vertically.

| Module | Size X/Y/Z in metres | Triangles | Mesh renderers |
| --- | --- | ---: | ---: |
| WallDistrictA | 2.4 / 6.8 / 0.131 | 2354 | 3 |
| WallDistrictB | 2.4 / 6.8 / 0.131 | 2440 | 3 |
| WallTransit | 3.2 / 1.8 / 0.146 | 2354 | 4 |
| WallService | 1.4 / 1.6 / 0.245 | 1856 | 3 |
| WallMemory | 2.6 / 2.2 / 0.131 | 2547 | 3 |
| WallGround | 2.6 / 2.2 / 0.131 | 1710 | 4 |
| WallCat | 0.9 / 1.0 / 0.063 | 944 | 3 |

All seven exports together contain 14205 triangles. Their four material names
are the existing shared `SC_Concrete`, `SC_Metal`, `SC_Orange`, `SC_White`.
Separate objects have been combined by material. The Unity placement/baking
step can batch placed modules again; these counts are source asset counts, not
a runtime draw-call or frame-rate claim. No textures, colliders, animation rigs,
scripts or runtime text objects are embedded in these exports.

## Content and provenance

- The district plates display `07 / 空中街区` and `12 / 层间通行`.
- The rail sign displays `轻轨环线 / SKY LOOP` and an original rail pictogram.
- The service cassette has angled rain louvres, dark recesses, fixings and the
  small manufactured serial `AIR / 07-B`.
- `昨日路线 / 已存档` refers to EchoRun's recorded previous route.
- `下一层 / 也是地面` is a quiet joke about the city's stacked floor levels.
  Its floor datum marks are deliberately not arrows or gameplay instructions.
- The cat is an original asymmetrical sitting-cat relief with an orange eye
  accent. It has no luminous orb shape, collectible behavior or copied mascot.

Sign layouts, rail icon, route/floor motifs, cassette and cat silhouette were
authored in this task. Chinese glyph outlines were rendered offline using the
installed **SimHei** font at `C:/Windows/Fonts/simhei.ttf`. Latin letters/numbers
use the repository's **EchoRun Sans SC Regular**, a Noto Sans CJK SC 2.004 subset
under SIL OFL 1.1; its existing license is `Assets/Resources/Fonts/OFL.txt`.
The existing subset lacks the characters 昨 and 街, so it cannot render all these
signs on its own. Glyphs are converted to planar static mesh faces. No new font
binary is copied or packed into either the `.blend` or FBX assets.

## Verification boundary

The Blender export checks assert less than 10000 triangles per module and no
outward extent above 0.28 m. The generated preview has been visually inspected
for the exact Chinese text, glyph completeness, contrast and motif shapes.
This preview proves asset appearance, not gameplay placement/readability.
Fresh Unity captures and player-build verification belong to the integration
handoff and must be used for claims about the playable game.
