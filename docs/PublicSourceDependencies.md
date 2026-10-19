# Public source resource dependencies

This is a source-review snapshot with intentionally omitted reusable character and motion assets. Scene, prefab and controller references may be unresolved until you supply compatible permitted assets. Do not interpret a successful C# compile as complete playable content.

## Character data

The original Mixamo ExoGray input and reusable OrangeEcho/MemoryCourier files containing retained character components are not distributed here. Obtain a suitable character under terms allowing your intended use, or use a character you author. Review rig, skeleton, mesh, material and prefab references before replacing dependencies.

## Motion data

Unity Standard Assets motion inputs, derived ReadyIdle data, and a Visvise motion without a recorded public redistribution basis are omitted. The project-authored code and configuration remain for inspection. The CC0 Quaternius library retains its included license.

The accepted private FlowFit Jump/Land motions were already excluded from source control. Their documented setup is in [RunnerMotionDependencies.md](RunnerMotionDependencies.md); its original helper recipes may require additional omitted motion inputs. You must obtain the appropriate inputs and permissions or author replacements.

## Build and CI

GitHub Actions is initially disabled for the public snapshot. No self-hosted runner is attached. Complete the resource setup and review every workflow and credential boundary before enabling automation. No engine build or end-user validation was run for this publication-only snapshot.

The complete excluded path list is recorded in [PUBLIC_SOURCE_SNAPSHOT.md](../PUBLIC_SOURCE_SNAPSHOT.md).
