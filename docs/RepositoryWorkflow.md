> Public source snapshot: GitHub Actions is initially disabled. Historical branches, CI, builds and release records in this document describe the development repository. Additional reusable character/motion sources are omitted; see [PublicSourceDependencies.md](PublicSourceDependencies.md).

# Repository workflow

## Current branch and evidence

The current submitted development source is integrated into `master` during
the 2026-10-12 maintenance. `codex/single-contract-prototype` remains the development
branch. See `docs/releases/repository-maintenance-20261012.md` for local evidence.
Full remote CI still requires an approved, provisioned self-hosted runner; a
branch trigger does not prove an engine run or publish a playable package.

- **Repository health** runs on pushes to `master` and `codex/**`, and PRs targeting
  those branches. GitHub-hosted workers check public files, private-motion
  exclusions, and the CI scripts with synthetic fixtures. They need no engine,
  licensed motion, credentials, or self-hosted runner access.
- **Three-platform Tuanjie CI** runs for pushes to `master` and the current
  development branch, or a maintainer's manual dispatch. It runs EditMode and
  PlayMode before WebGL, Windows and Android builds. It deliberately does not run
  untrusted pull-request code on the private self-hosted runner.
- Only one full CI workflow runs at a time. Use a dedicated Windows runner with
  Tuanjie **2022.3.62t8**, its platform modules, a valid license, and a functioning
  graphics session. The preparation and PlayMode checks require rendering.

## Private motion on the self-hosted runner

A clean checkout cannot reproduce the current Jump/Land animations on its own.
Follow [RunnerMotionDependencies.md](RunnerMotionDependencies.md) to obtain and
prepare a permitted copy of the source. Do not put reusable private assets in
GitHub secrets, public storage, repository commits, caches, or CI artifacts.

Before enabling the first full CI run, the runner administrator must prepare a
folder **outside `GITHUB_WORKSPACE` and its checkout directories**, for example
`D:\EchoRunPrivate\HumanBasicMotions`, containing these six files:

```text
HumanM@Jump01.fbx
HumanM@Jump01.fbx.meta
HumanM_Model.fbx
HumanM_Model.fbx.meta
Human Body Full Mask.mask
Human Body Full Mask.mask.meta
```

Set the runner service's `ECHORUN_PRIVATE_MOTION_ROOT` environment variable to that
folder, then restart the runner service so it inherits the value. Keep original
importer metadata and its Avatar/mask references. The checked-in
`Tools/CI/private-motion-manifest.json` pins the three reviewed asset payloads;
metadata is checked for structure and then validated by Tuanjie's actual importer.
Original metadata and Tuanjie-rewritten metadata can differ in serialization.
Source-version mismatches fail before copying; update the manifest only after a
separate source/license/motion review.

The workflow uses a disposable **`ci-project` subdirectory**, with checkout
cleaning enabled. It does not clean the old root checkout or move/delete the
external private source. This supports migration from the previous `clean: false`
workflow: copy its licensed originals into the external folder first, retain the
old originals until verified, and never use `ci-project` for manual work or as
private storage. If multiple runner installations are used, provision each one.

For every test/build job:

1. Cleanly check out the requested commit into `ci-project`.
2. Validate source hashes, importer metadata and paths; copy only the six files.
   Links/junctions and a private source inside the workspace are rejected. A
   differing existing private file is never overwritten by the restore script.
3. Run `RunnerBasicMotionReview.InspectCandidates` to import and validate the full
   source, then `RunnerFlowJumpAuthor.BuildFittedCandidates` and
   `RunnerFlowPreviewInstaller.InstallReviewedLocal` to regenerate and bind it
   through the Editor API. Idle/Run/Slide and scene gameplay values are preserved.
4. Require successful editor exit, preparation completion markers, generated
   clips, and nonempty passing test XML/build output. Missing dependencies fail
   explicitly; stale files or successful C# compilation do not count as success.

Only test XML/logs and finished player builds are uploaded. `Assets`, the external
bundle, and `TestResults` (which can contain sampled source curves) are not uploaded.
These workflow changes have local script verification; an actual clean runner
reconstruction and successful three-platform run still need to be observed after
runner provisioning. Enabling a branch trigger alone does not prove CI is green.

## Local use and scope

Run the lightweight checks without private assets or the engine:

```powershell
./Tools/CI/Test-CIScripts.ps1
```

For a **disposable clone** with the licensed external source configured and all
editor windows closed:

```powershell
$env:TUANJIE_EDITOR = 'D:\unity\tuanjie\2022.3.62t8\Editor\Tuanjie.exe'
$env:ECHORUN_PRIVATE_MOTION_ROOT = 'D:\EchoRunPrivate\HumanBasicMotions'
./Tools/CI/Invoke-TuanjieCI.ps1 -Mode Prepare -Workspace (Get-Location).Path
./Tools/CI/Invoke-TuanjieCI.ps1 -Mode EditMode -Workspace (Get-Location).Path
```

Do not run `Prepare` over the accepted working copy just to tidy files: it
regenerates animation clips and updates controller bindings in the selected
checkout. Full CI intentionally reports every existing failure; no test is
skipped or weakened to make the status look green. Visual and phone acceptance
remain separate from automated test success.
