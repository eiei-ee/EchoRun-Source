# Runner motion: required local assets

## Checkout limitation

The runner version accepted as the current baseline on 2026-09-28 uses locally
generated `RunnerFlowFitJump` and `RunnerFlowFitLand` clips. Their animation data
is derived from Kevin Iglesias's **Human Basic Motions FREE**, together with the
existing Unity Standard Assets running motion. The reusable private source and
generated clips are deliberately excluded from this public repository.

**A fresh checkout does not contain the current Jump/Land motions and cannot
reproduce the accepted playable version until the dependency setup below is
completed.** The committed `EchoRunHuman.controller` records the accepted
bindings; it does not supply the referenced private animation data. There is no
automatic public fallback. Do not interpret successful C# compilation alone as
a complete playable build or a successful animation test run.

On the development machine that already has these assets, leave them in place.
The source commit does not require rebuilding or replacing that machine's
accepted clips. Other developers must obtain their own permitted copy from the
author and review its applicable terms.

## Sources and boundaries

| Material | Source and applicable notice | Repository treatment |
| --- | --- | --- |
| Human Basic Motions FREE whole-body jump | [Author download](https://kevdev.itch.io/basic-motions-free), [author license section](https://www.keviniglesias.com/#license) | Required local input; neither original reusable assets nor derived Flow clips are distributed here. The author's terms saved during the 2026-09-26 acquisition identify the Standard Unity Asset Store EULA, including free downloads. Recheck the terms when obtaining the asset. |
| Existing `HumanRunForwards.fbx` and `HumanIdle.fbx` | Unity Standard Assets Characters; license and notice in `Assets/ThirdParty/UnityStandardAssets` | Already included under the Unity Companion License. Their use in generated clips does not turn the source motion into MIT-licensed material. |
| ReadyIdle contribution from Quaternius Universal Animation Library Standard | [Author pack](https://quaternius.com/packs/universalanimationlibrary.html); the downloaded pack includes CC0 1.0 | `RunnerReadyIdle.anim` combines this contribution with the existing HumanIdle support pose. See `THIRD_PARTY_NOTICES.md`; retain the applicable source notices. |
| Athlete garment mesh and materials | Project-authored adaptation of the existing fitted courier garment | Included in `Assets/Art/RunnerAthleteCandidate`; uses the existing character rig and shaders. |

The local ignore rules cover both:

```text
ArtSource/AnimationReferences/KevinBasicPrivate/
Assets/Animations/HumanMotion/KevinBasicPrivate/
Assets/Animations/HumanMotion/KevinBasicPrivate.meta
```

Do not force-add the original package, extracted model/FBX/mask files, private
metas, or generated standalone `.anim` files. These exclusions also apply to
copies under other paths. The authored C# recipe is included; it reads animation
data from separately obtained sources and does not embed that private data.

## Prepare the private source

Use the project editor from `ProjectSettings/ProjectVersion.txt`: Tuanjie 1.9.0,
editor **2022.3.62t8**. Save work and leave Play mode. Run only one editor on this
project at a time.

1. Obtain **Human Basic Motions FREE** from the author. The locally reviewed
   Unity package was 15,902,319 bytes with SHA-256
   `DD5AAF1DFCB7CB907945190ACBC6D34AA1BE40D0EF38E7CF0CD86F2918D74339`.
   This identifies the reviewed input, not a guarantee about future downloads.
2. Extract or import the package in a separate local workspace. Preserve original
   `.meta` files. Copy only the following asset/meta pairs into the ignored
   `Assets/Animations/HumanMotion/KevinBasicPrivate/` folder of this project:

   | Source path relative to the package's `Assets/Kevin Iglesias/Human Animations/` | Destination filename |
   | --- | --- |
   | `Animations/Male/Movement/Jump/HumanM@Jump01.fbx` | `HumanM@Jump01.fbx` |
   | `Models/HumanM_Model.fbx` | `HumanM_Model.fbx` |
   | `Models/Avatar Masks/Human Body Full Mask.mask` | `Human Body Full Mask.mask` |

3. Import the model and mask dependencies before preparing the jump source.
   Preserve the original Humanoid rig mapping, model Avatar and full-body mask.
   The original animation meta uses **Copy From Other Avatar**; omitting the
   model or replacing the meta with a newly generated one breaks that contract.
   The reviewed model GUID was `2faa610713d3b3c439473daa55e8c60a`, and the mask
   GUID was `89527f5525238ee44b3182458d85143a`.
4. Run `RunnerBasicMotionReview.InspectCandidates`. In the editor this is
   **Tools → Echo Runner → Athletic Clips → Review Basic Whole Body Source Jump**.
   It prepares the importer and checks that the complete clip is named
   `HumanM@Jump01`, with the matching take, a valid source Avatar, and Humanoid
   animation. It also samples the actual scene character for an isolated review.

The reviewed complete Jump source has 46 frames at 30 FPS (about 1.533333 seconds)
and SHA-256
`F91337E8B1BC09CBD98AE05CB8AF82BF31AA1BA6D4D9EE42FD96919E01E5CC94`.
Do not substitute the separate Begin/Land files or a root-motion variant without
reviewing and adapting the recipe.

## Generate and bind the accepted motion

After source preparation, execute these methods in order:

1. `RunnerFlowJumpAuthor.BuildFittedCandidates`
2. `RunnerFlowPreviewInstaller.InstallReviewedLocal`

The first method generates the full-body curves, bakes root channels, bounds the
observed right-leg twist extremes, and calls `RunnerFlowRigFit.FitJumpContact`
and `RunnerFlowRigFit.FitLandingContact` against the actual scene character's skinned
geometry. The second binds the locally generated clips to the existing Jump and
Land states. The generated assets may receive different GUIDs on another
machine; the installer updates the controller through the Editor API. Do not
manually copy GUID text or edit controller YAML to reconstruct the binding.

Alternatively, after source preparation, run
`RunnerFlowPreviewInstaller.BuildGroundedTimingPreview`. It sets the SampleScene
player's height to **2.4** and duration to **0.78**, then calls both generation and
installation methods above. It opens and saves SampleScene, so save any other
editor work first. The committed scene and `PlayerController` defaults already
contain these gameplay values.

These entry points can be called with the editor's `-executeMethod` option. For
example, with all editor windows for this project closed:

```powershell
$runnerEditor = 'D:\unity\tuanjie\2022.3.62t8\Editor\Tuanjie.exe'
$runnerProject = (Resolve-Path '.').Path
$runnerLogDirectory = Join-Path $runnerProject 'TestResults\RunnerMotionSetup'
New-Item -ItemType Directory -Path $runnerLogDirectory -Force | Out-Null
$runnerMethods = @(
    'RunnerBasicMotionReview.InspectCandidates',
    'RunnerFlowPreviewInstaller.BuildGroundedTimingPreview'
)
foreach ($runnerMethod in $runnerMethods) {
    $runnerLog = Join-Path $runnerLogDirectory ($runnerMethod + '.log')
    $runnerArguments = @(
        '-batchmode', '-quit',
        '-projectPath', ('"{0}"' -f $runnerProject),
        '-executeMethod', $runnerMethod,
        '-logFile', ('"{0}"' -f $runnerLog)
    )
    $runnerProcess = Start-Process -FilePath $runnerEditor -ArgumentList $runnerArguments `
        -WindowStyle Hidden -Wait -PassThru
    if ($runnerProcess.ExitCode -ne 0) { throw "Setup failed; read $runnerLog" }
}
```

Adjust the editor executable path to the installed location. Do not add
`-nographics`: source review renders the actual skinned character. Check the logs
for `BASIC_SOURCE_REVIEW_CAPTURED` and `GROUNDED_JUMP_TIMING_READY`; a process
starting is not proof the operation completed.

The public helper files required for these steps are:

```text
Assets/Editor/RunnerBasicMotionReview.cs
Assets/Editor/RunnerFlowJumpAuthor.cs
Assets/Editor/RunnerFlowRigFit.cs
Assets/Editor/RunnerFlowPreviewInstaller.cs
```

Keep their `.meta` files. The scene must include the current character mesh,
materials, valid Humanoid Avatar and controller, and the existing
`HumanRunForwards.fbx` must be available. The recipe does not depend on the
experimental Quaternius2 library, JumpCandidates, or additional VISVISE clips.

## Verify after local setup

The resulting private clips are:

```text
Assets/Animations/HumanMotion/KevinBasicPrivate/Generated/RunnerFlowFitJump.anim
Assets/Animations/HumanMotion/KevinBasicPrivate/Generated/RunnerFlowFitLand.anim
```

Jump is a 0.9-second authored sampling timeline controlled by
`RunnerJumpPhase`; physical airtime is configured separately as 0.78 seconds.
Land lasts 0.12 seconds. `InstallReviewedLocal` preserves the existing Idle, Run
and Slide states. Check both actual bindings and rear/side playback before
accepting a reconstruction on another machine.

The 2026-10-11 local tempo revision brings the tucked pose in earlier, releases
the legs earlier during descent, and limits the source arm blend to retain the
running elbow carriage. Landing source weight decreases continuously instead
of adding a second compression pulse. The phase-driven Jump entry blend is
0.04 seconds; Land enters in 0.025 seconds, recovers over 0.12 seconds and blends
back to Run in 0.04 seconds. Physical jump height (2.4 metres),
airtime (0.78 seconds), movement and collision rules are unchanged.
The follow-up foot pass reduces repeated ankle/toe flexion in the six foot
muscle channels while preserving their exact entry and exit values. Every leg,
root and foot-goal channel remains as authored before contact fitting. The bake
samples the actual Humanoid rig to reject changed hip/knee rotations and require
reduced ankle/toe rotation travel; shoe-contact fitting then runs as before.
`RunnerFlowPreviewInstaller.RebuildAndInstallJump` runs generation, root baking,
shoe-contact fitting and binding in order without editing the scene. Set
`ECHORUN_JUMP_REPORT`, `ECHORUN_JUMP_FIT_REPORT` and
`ECHORUN_JUMP_INSTALL_REPORT` to fresh output folders for each review run.
Contact fitting now includes the Jump clip as well as Land, because the outgoing
airborne pose remains visible during the landing blend. The continuous review
measures actual skinned soles against the road through both transitions.
The unchanged Run loop still shows brief sole penetration in the before/after
captures; it is a separate gait issue, outside this jump revision.

The 2026-09-28 local baseline passed 55 focused EditMode checks and three PlayMode
checks, including actual coin pickups and obstacle clearance. The full EditMode
run was **909/913**, with three tests still asserting the previous garment mesh
path and one existing turn-scene camera-clearance failure. Those results were
obtained with the licensed local assets present; they are not a clean-checkout
test result and do not claim the entire suite is green. Fresh reconstruction and
phone acceptance remain separate verification steps.

After the 2026-09-28 repository organization, the full EditMode run passed
**917/918** checks, with zero skipped. The three garment tests now verify the
accepted Athlete mesh and actual deformation, including phase-driven Jump
sampling; five legacy-installer protection tests were added. The remaining
turn-scene camera-clearance failure is documented in
[KnownIssues.md](KnownIssues.md). That run compiled the current project with the
local licensed assets present; it is still not a clean CI reconstruction or a
new phone acceptance result.
