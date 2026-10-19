# Stacked City District Facades

Original meshes authored locally with `Tools/Art/create_stacked_city_district_facades.py`. No downloaded art, generated image texture, third-party model or font is included.

Four distinct forms: residential paired windows and ochre sun canopy; civic horizontal grouped glazing and deep reveal; transit tall glazing and projecting metal fins; heritage narrow divided sashes with a sloping rain hood.

Same centered wall-mount pivot and FBX convention as FacadeBay: Blender -Y outward, export -Z forward / Y up, unit scale metres. All back surfaces are at least 12 mm away from the nominal wall. Reveal rings have open apertures instead of coplanar glass overlays. Geometry is static, merged by existing SC material, opaque, with no collider or animation. Unity must remap material slots and choose the family in the architecture placement pass.

The source blend and PNG are an inspection arrangement; every FBX is exported at origin before this arrangement is applied. Preview order: residential / civic above, transit / heritage below.

| Asset | Unity XYZ bounds (m) | Triangles | Material meshes |
| --- | --- | ---: | ---: |
| FacadeResidential | 2.82 x 2.05 x 0.2885 | 224 | 4 |
| FacadeCivic | 2.98 x 2.0025 x 0.278 | 148 | 5 |
| FacadeTransit | 2.78 x 2.08 x 0.2985 | 160 | 4 |
| FacadeHeritage | 2.7 x 2.081 x 0.304 | 284 | 4 |

Budget reference: the existing FacadeBay has 864 triangles and four material meshes. This source preview verifies the asset family; actual gameplay acceptance is separate.
