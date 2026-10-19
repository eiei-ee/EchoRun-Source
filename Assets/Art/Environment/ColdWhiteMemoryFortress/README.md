# Cold White Memory Fortress

Original Blender-authored environment kit for EchoRun's 60 m visual sample.
The direction uses cold white concrete and ceramic masses, dark structural
recesses, a graphite road, and one sparse phase-emission layer.

## Source and generation

- Generator: `Tools/Art/create_cold_white_memory_fortress.py`
- Source: `ArtSource/Blender/ColdWhiteMemoryFortress_60m.blend`
- Kit preview: `ArtSource/Previews/ColdWhiteMemoryFortress_Kit.png`
- Gameplay preview: `ArtSource/Previews/ColdWhiteMemoryFortress_Gameplay56.png`
- Portrait preview: `ArtSource/Previews/ColdWhiteMemoryFortress_Portrait62.png`
- Blender version verified: 5.2.0 LTS

Run from the repository root:

```powershell
blender --background --factory-startup `
  --python Tools/Art/create_cold_white_memory_fortress.py -- `
  --output-root <project-root>
```

`--output-root` is mandatory so a dry run can target an isolated directory.

## FBX contract

| FBX | Mesh batches | Triangles | Blender bounds X/Y/Z (m) |
| --- | ---: | ---: | --- |
| `CantileverSlab_A.fbx` | 5 | 352 | 28.000 / 12.000 / 11.700 |
| `MemorySilo_A.fbx` | 5 | 2,284 | 8.000 / 8.000 / 16.000 |
| `ArchiveTower_A.fbx` | 5 | 440 | 8.989 / 6.888 / 22.000 |
| `ScanRing_A.fbx` | 5 | 3,024 | 17.800 / 2.590 / 12.093 |
| `BrokenOverpass_A.fbx` | 5 | 440 | 24.277 / 8.240 / 10.118 |
| `MechanicalFacility_A.fbx` | 5 | 352 | 5.000 / 4.000 / 3.176 |
| `MechanicalFacility_B.fbx` | 5 | 352 | 5.000 / 4.000 / 3.176 |
| `RoadStraight_A.fbx` | 3 | 252 | 11.000 / 20.000 / 0.293 |
| `RoadTurnRight_A.fbx` | 3 | 1,428 | 15.500 / 15.500 / 0.385 |

Architecture assets use a bottom-center pivot. Road assets use an entry-center
surface pivot, a visual width of 11 m, lane centers at -3/0/3 m, and a 9 m
playable-width contract. FBX export retains the established `-Z Forward / Y Up`
settings. Axis correction belongs on the Unity Prefab's visual child, not on the
gameplay or connector root.

The right-turn graphite mesh is a solid 15.5 m corner platform. Its outer white
guards leave the entry and exit open, and never trace an interior L-shaped hole.

## Shared material names

Architecture uses exactly five semantic batches:

- `MF_ColdConcrete`
- `MF_CeramicLight`
- `MF_MetalDark`
- `MF_RecessBlack`
- `MF_PhaseEmitter`

Roads use exactly three semantic batches:

- `MF_RoadGraphite`
- `MF_RoadInset`
- `MF_RoadEdgeWhite`

`MF_PhaseEmitter` remains separate from the base structure so Unity can swap its
calibration, challenge, intrusion, and finale state without replacing the city.
`ScanRing_A` is an open upper arch rather than a closed torus, and keeps the
road plane clear while retaining the scan emitter as a separately named mesh.
The assets contain no colliders, cameras, lights, animation, or third-party
geometry/textures.

The gameplay preview uses the landscape game camera contract: vertical FOV 56
degrees, player-relative offset `(0, 4.6, -8.2)`, and a five-meter forward look.
The portrait preview adds a 720 x 1280, 62-degree mobile framing check. Both are
framing aids only; acceptance still requires a real Unity/Tuanjie player capture
after integration.
