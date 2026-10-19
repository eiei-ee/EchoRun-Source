# Stacked city material authoring

The architecture uses a warm off-white mineral finish, muted blue-grey structure,
dark teal glazing and sparse orange navigation marks. Gameplay road and pickup
materials retain their existing ownership.

`Assets/Editor/StackedCityMaterials.cs` is the reproducible editor authoring source.
It produces two original 1024 × 1024 seamless textures: panel albedo and packed
surface data (RG surface slopes, B local joint occlusion, A smoothness variation).
Each repeat represents 4.8 metres with 2.4-metre mineral panels. Mipmaps and
trilinear filtering limit shimmer in motion. These are production material maps,
not a replacement image rendered over the game.

The shared `EchoRun/MineralCladding` shader projects those textures in world space
so imported buildings and combined meshes have the same scale. Six texture
samples per fragment replace the former three-sample albedo-only material. Mesh
tangent frames are retained for normal relief. Glass and train paint continue
to use Unity Standard with separately authored smoothness and metal response.

`BakeSkyReflection()` uses Unity's editor reflection baker and Specular cubemap
convolution. The generated EXR is retained in `Assets/Art/StackedCity/Textures/`;
the filtered 128-pixel, eight-mip RGBAHalf cubemap is serialized at
`Assets/Resources/CityV7/StackedCityReflection.cubemap`. The temporary probe is
destroyed and the prior scene sky is restored. Runtime samples this static sky
reflection; it does not reflect nearby moving trains or local buildings.

Only near-city instances restore former SkyMiddle/SkyFar facade substitutions.
The separate distant skyline keeps its simplified materials. Existing Unity
material and mesh GUIDs are preserved by editor updates.

Rebuild with `StackedCityInstaller.InstallPolishAndCapture` in the project's
Tuanjie editor with graphics enabled. Review output is separate from the earlier
stacked-city baseline under `TestResults/StackedCityPolish/After/Composition`.
