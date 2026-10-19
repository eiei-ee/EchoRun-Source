# Stacked City Finish Gate

Original formal model authored by `Tools/Art/create_stacked_city_finish_gate.py`. Two segmented mineral columns carry a deep graphite lintel and weather canopy. White Chinese 终点 / FINISH lettering, narrow cyan fittings and orange registration tabs use the existing Stacked City material family. The back carries readable FINISH lettering.

Metres, ground-centre pivot. Unity size **11.30 × 5.78 × 1.08 m**, front **-Z**, column centres X=±5.08 m. A **9.30 × 4.30 m** aperture stays completely open. No floor bar, suspended low part, rig, animation, collider, light, runtime text or font file.

Budget: **7383 triangles**, **5 material meshes**, **149788 FBX bytes**. Signal geometry is merged separately as `CityFinishGate_SC_Cyan`; only that mesh should receive optional signal animation. Materials: SC_Concrete, SC_Metal, SC_White, SC_Orange, SC_Cyan.

The generator checks solid component manifoldness and outward normals, both lettering directions, empty passage and competing coplanar material faces before exporting. Text is 27 mm ahead of the lintel face. The preview is an asset inspection, not gameplay acceptance. Unity import, binding, collision preservation and real game-camera verification remain the integrator's responsibility.

Chinese text uses outlines of the installed SimHei font. Latin text uses the existing EchoRun Sans SC (Noto Sans CJK SC subset, SIL OFL 1.1). No font files, textures or external models are redistributed.

Unity integration: run `CityFinishGateInstaller.InstallAndCapture`. The delivered resource is `Assets/Resources/CityV7/FinishGate.prefab`; it preserves the FBX axis conversion and uses the current city's pale mineral surface plus dedicated graphite and cyan materials. `FinishGatePresentation` drives only the independent signal renderer via a property block. Two authored, shadow-free point lights have 5.5 m range and activate within the existing 12 m / high-quality rule. TrackManager retains its 30 m visibility window and course-based placement; GameManager still owns completion.

Verification artifacts live in `TestResults/CityFinishGate`: 18 fresh city compositions cover three distances and all three lanes. EditMode checks include clipped-triangle clearance, persistent asset/material binding and signal isolation; the PlayMode smoke test checks actual hiding, light deactivation, reuse and forward placement. Consult that directory's README for the final player and runtime evidence.
