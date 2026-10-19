# Stacked City architectural modules

Original close-range facade meshes authored locally with Blender by
`Tools/Art/create_stacked_city_architecture.py`. No downloaded models or imagery.
Sign lettering is converted to geometry using Blender's bundled Bfont.

The five exported FBX modules are reusable production meshes. Each mesh is merged
by its shared material; the blend file includes an inspection layout and camera.
Exported modules stay at origin. Metres, Blender Z up / -Y outward, imported Unity
Y up / +Z outward. Keep the imported FBX axis rotation when placing or baking.

| Module | Width x height x depth, metres | Triangles | Source renderers |
| --- | --- | ---: | ---: |
| FacadeBay | 2.82 x 2.08 x 0.26 | 864 | 4 |
| FacadeCornice | 1.00 x 0.21 x 0.29 | 216 | 2 |
| Wayfinding07 | 2.75 x 5.15 x 0.084 | 1828 | 3 |
| Wayfinding12 | 2.75 x 5.15 x 0.084 | 1824 | 3 |
| Wayfinding21 | 2.75 x 5.15 x 0.084 | 1824 | 3 |

`StackedCityArchitecture.Prepare()` imports these meshes. The editor-only
`DecorateBuilding(building, stableVariant)` reads the real opaque wall faces,
fits a sparse set of shadow-casting modules to supported surfaces, excludes
existing glazing, and saves combined meshes under `ArchitectureMeshes`.
The detail child is replaced on repeated calls; no runtime geometry or collider
is created. The installer should call it on source buildings before lower-city
scaling and on trackside buildings after their outward placement.

Each building is limited to 18 window bays, 24 cornices, 28 pairs of subtle panel
reveals, and one district sign, for a theoretical ceiling of about 23.3k extra
triangles. Actual placement is usually much lower because support/glazing checks
reject crowded walls. Up to six renderers share existing SC_Concrete, SC_Glass,
SC_Metal, SC_Warm plus SC_Orange and SC_Joint. When parent-baked these add only two
new material batches beyond the existing palette. Largest outward offset is
0.302m (cornice plus placement gap); there is no change to gameplay meshes.

Blender preview validates the modules only. Real-game capture is required to
accept fitting, scale, lighting, repetition, and readability from the running
camera.
