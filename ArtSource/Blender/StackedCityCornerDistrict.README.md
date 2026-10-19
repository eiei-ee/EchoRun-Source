# Stacked City Corner District Tower

`CityCornerDistrictTower.fbx` is a bounded variant of the existing `CityCornerTower.fbx`, authored by `Tools/Art/create_stacked_city_corner_district.py`. The generator imports the existing continuity authoring helpers and calls the original `corner_tower()` with one temporary facade callback. It never exports over the original tower or foundation.

Only the front and back glazing of the 20 metre podium changes. Both facades retain a continuous 3.6 metre central strip of the original solid wall, spanning local X = -1.8 to +1.8. The eight original glass bands become sixteen closed left/right segments, and the two central vertical piers are omitted. The front and back structural wall planes remain Blender Y = -10.7 and +10.7, corresponding to Unity Z = +10.7 and -10.7. The strip provides a supported mounting surface for separately fitted 2.4 × 6.8 metre district plates. No extra floating wall or sign is part of this tower asset.

All 302 non-target components have matching SHA-256 geometry signatures against a freshly authored original tower. This includes the closed podium body, side windows and side piers, service spines, upper wings, galleries, terraces, planting and crown. The podium body is explicitly checked for a closed manifold and positive volume before merging.

The Unity outer bounds remain exactly `[-9, 0, -11]` to `[9, 83.6, 11]` metres, giving an unchanged 18 × 83.6 × 22 metre footprint and height. The tower attaches at local Y = 0. Placement, scaling, supporting foundations, gameplay lanes and clearance belong to the existing Unity prefabs and are not changed by the generator.

The existing `verify_and_export` checks passed: outward normals, positive signed volumes, five material meshes, and zero competing coplanar material pairs. The derivative contains 7,464 triangles, an increase of 72 over the original 7,392. Export size is 143,644 bytes. Existing SC_Concrete, SC_Glass, SC_Metal, SC_Orange and SC_Foliage materials are reused. Exact values and verification flags are recorded in `StackedCityCornerDistrict.manifest.json`.

This is a formal static mesh asset. It contains no runtime scripts, colliders, font, animation or light. Unity must preserve the imported root axis conversion, disable animation/collider import, and bind the existing district material variants during installation.

`StackedCityCornerDistrict.blend` contains the complete tower and inspection camera. `StackedCityCornerDistrict.preview.png` shows its unchanged overall silhouette; `StackedCityCornerDistrict.podium.png` shows the central solid mounting strip. Both are source-model inspection renders, not gameplay acceptance evidence.
