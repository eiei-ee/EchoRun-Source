# Stacked City Continuity

Original static architectural meshes authored locally in Blender by
`Tools/Art/create_stacked_city_continuity.py`. No downloaded model, texture,
font, or animation is included. Existing project SC_* materials are reused.

Blender +Z is height, FBX uses -Z forward / +Y up, metre units. Unity importer
must remap materials, disable animation and colliders, and preserve local root.
CityDeepBase attaches by its top face at local Y=0 and extends down 96 metres.
Its inhabited core is 18.5 metres square with a closed 20-metre foundation foot.
Horizontal X/Z fitting is supported; retain its full vertical depth. It is
intended to meet another building, not to be the sole visible building top.
CityCornerTower attaches by its base at Y=0; stepped wings, attached galleries,
four occupied terraces, and a setback crown form a distinct tall silhouette.
All galleries overlap their own solid support wings. No geometry or scripts
extend a bridge into a track corridor; placement and clearance are Unity-owned.

Geometry is merged into one mesh per shared material. All component normals
were recalculated outward and signed volumes checked positive before export.
An offline equivalent of CitySurfaceReview checks same-facing triangle plane
overlap across materials, including bottom faces; both models have zero pairs.
Foundation metal corner caps terminate inside the coping/foot assembly instead
of sharing the concrete core's top and bottom planes.
Windows are opaque dark glazing with continuous bands and raised masonry piers.
The final five-material budget includes a small amount of rooftop foliage.

The Blender source contains an offset inspection arrangement and lighting;
FBX files were exported at their own origins before that arrangement. The
preview is a source-model inspection image, not gameplay acceptance evidence.

| Asset | Unity minimum XYZ (m) | Unity maximum XYZ (m) | Triangles | Meshes/materials |
| --- | --- | --- | ---: | ---: |
| CityDeepBase | [-10.0, -96.0, -10.0] | [10.0, 0.0, 10.0] | 4152 | 3 |
| CityCornerTower | [-9.0, 0.0, -11.0] | [9.0, 83.6, 11.0] | 7392 | 5 |

Exact bounds, materials, and byte counts are in the manifest.
