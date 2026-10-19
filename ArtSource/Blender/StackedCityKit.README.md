# Stacked City Kit

Original authored meshes generated locally in Blender by `Tools/Art/create_stacked_city_kit.py`.
No downloaded or third-party model, texture, font, or decal is included.

All units are metres. FBX export: -Z forward, Y up, apply unit scale, no animation.
Train nose points toward Blender -Y (Unity +Z under the project import convention).
Train/planter roots are at pavement height. Bridge and terrace deck surface is Y=0;
their supporting structures extend downwards. Bridge length is 24m with a single
central line, steel rails at X=-0.85m and +0.85m, and two inspection walkways.
Rail top is Y=0.16m: place train root at Y=0.16m.
The stair flight rises 6m over 9m horizontal length toward Unity +Z; the railing
continues to ~7.2m above its lower entry. The terrace has 4m entrance openings.

Every mesh is merged by its SC_* material. Unity should remap slots to project
materials, keep model scale at one metre, and disable generated colliders for
these background presentation modules. Windows are intentionally opaque dark
glazing: no alpha sorting, transparency pass, or texture dependency is required.

The blend file includes an inspection arrangement and lighting. Each exported
FBX is centred independently; do not use the inspection arrangement as a level.

| Asset | Unity XYZ bounds (m) | Mesh renderers | Triangles |
| --- | --- | ---: | ---: |
| SkyTrainCar | 2.796 x 2.9325 x 14.0225 | 5 | 13824 |
| SkyPlanter | 5.0 x 2.9266 x 2.024 | 4 | 3568 |
| SkyViaduct | 7.07 x 2.832 x 24.0 | 3 | 12164 |
| SkyTerrace | 18.0 x 2.232 x 12.0 | 4 | 12520 |
| SkyStair | 3.84 x 7.7162 x 9.0007 | 2 | 11032 |

The manifest records exact bounds, material names, and export byte counts.
