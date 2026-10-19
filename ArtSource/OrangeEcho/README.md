# Orange Echo authored art

Historical source kit for the retained roads, head/hand fragments and material
bindings. The accepted runner now uses the Athlete garment and Flow motions;
see `docs/RunnerMotionDependencies.md` for the current recovery procedure.
Orange names are retained for asset and runtime binding compatibility.

`Tools/Blender/build_orange_echo.py` authors the outfit and three road modules
offline in Blender 5.2.1. Editable `.blend` sources are retained here; exported
FBXs are in `Assets/Art/OrangeEcho/Models`. No Blender mesh generation runs in
the player. See `asset-statistics.json` for generated garment and road counts.

The outfit adds new garment geometry to the existing Mixamo skeleton. Original
head and hand fragments are retained from Exo Gray. These fragments and the
armature retain the Adobe Mixamo provenance documented in
`THIRD_PARTY_NOTICES.md`; the combined outfit FBX is not wholly original MIT
content. The new road meshes and garment construction script are project art.

Historical Unity installation used `OrangeEchoArtInstaller.Install` to attach
the original outfit to the scene skeleton and save meshes/materials through the
Editor API. That superseded installer was removed on 2026-09-28; its source is
available in Git history. The source art, generated roads, retained head/hand
fragments and their provenance remain because the current project uses them.

Road art is instantiated once per existing pooled segment by
`OrangeEchoRoadVisuals`. The art has no colliders. It replaces only visible
surfaces and rail renderers. Straight geometry spans x=[-5.5,5.5], z=[-10,10].
Turns enter at z=0, bend at z=10, and exit at x=+/-10, matching the existing
route contract. Blender's X axis is compensated in the exported road geometry.

The historical baseline snapshots, logs and player captures were written to
`TestResults/OrangeEcho-20260917`; local generated artifacts are not guaranteed
to remain after cleanup and are not distributed with this source kit. Static editor
clip samples are not evidence of real running/jumping/sliding; the optional
`-echo-orange-art-review` flag extends the existing isolated development-player
visual diagnostic with queued controller inputs and labelled frame captures.

Current work is a Windows visual iteration. WeChat export, device performance
and human acceptance must be recorded separately; no such claim follows from
a Windows build or asset statistics.

## Historical outfit and current recovery

`Tools/Blender/build_orange_echo.py` authors the historical base kit, including
its original wider outfit. Both `OrangeEchoArtInstaller` and
`LayeredMemoryRunnerInstaller` are retired; their old installation instructions
do not restore the accepted runner. Keep the committed Athlete mesh, materials
and scene bindings, and use `docs/RunnerMotionDependencies.md` to prepare the
licensed local inputs and restore the accepted animation bindings.

Superseded slim candidates and their local review captures are not runtime
dependencies and are not part of this delivery. Historical character source,
reproduction records and validation limits are documented in
`ArtSource/Blender/LayeredMemory/MemoryCourier.md` and
`docs/2026-09-24-memory-courier-form.md`.
