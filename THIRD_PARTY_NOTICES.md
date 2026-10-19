# Third-Party Notices

This public source snapshot omits the reusable Mixamo character and derived mesh files, Unity Standard Assets motion data and derived clips, and unconfirmed Visvise motion sources listed in [PUBLIC_SOURCE_SNAPSHOT.md](PUBLIC_SOURCE_SNAPSHOT.md). Notices below also preserve the development project's dependency history; a named historical path does not mean its asset is distributed in this snapshot. See [public resource setup](docs/PublicSourceDependencies.md).

This file records third-party material that is included in the repository.
The MIT license at the repository root applies to the project's source code,
not to the assets listed below. Each listed asset remains subject to its own
license or terms.

| Asset | Path | Source | License / permission |
| --- | --- | --- | --- |
| EchoRun Sans SC Regular (project subset) | `Assets/Resources/Fonts/EchoRunSansSC-Regular.otf` | [Noto Sans CJK 2.004 official release](https://github.com/notofonts/noto-cjk/releases/tag/Sans2.004), `Sans/OTF/SimplifiedChinese/NotoSansCJKsc-Regular.otf` | Derived from Noto Sans CJK SC Regular 2.004 and renamed for this project. SIL Open Font License 1.1; the license text is included at `Assets/Resources/Fonts/OFL.txt`. |
| Tense Future Loop | `Assets/Resources/Audio/bgm_transit.ogg` | [OpenGameArt](https://opengameart.org/content/tense-future-loop) | CC0. |
| Impact Sounds | `Assets/Resources/Audio/footstep_01.ogg`, `Assets/Resources/Audio/footstep_02.ogg`, `Assets/Resources/Audio/collision.ogg` | [Kenney](https://kenney.nl/assets/impact-sounds) | CC0. |
| RPG Audio | `Assets/Resources/Audio/coin.ogg` | [Kenney](https://kenney.nl/assets/rpg-audio) | CC0. |
| Interface Sounds | `Assets/Resources/Audio/ui_click.ogg`, `Assets/Resources/Audio/ui_confirm.ogg`, `Assets/Resources/Audio/ui_error.ogg` | [Kenney](https://kenney.nl/assets/interface-sounds) | CC0. |
| Exo Gray humanoid character | `Assets/Models/Mixamo/ExoGray` | [Adobe Mixamo](https://helpx.adobe.com/creative-cloud/faq/mixamo-faq.html) | Adobe permits royalty-free use of Mixamo characters and animations in personal, commercial, and non-profit video-game projects. The character remains subject to Adobe's terms and is not covered by this repository's MIT license. |
| Standard Assets humanoid animations | `Assets/Animations/HumanMotion` | [Unity Standard Assets Characters](https://github.com/Unity-Technologies/Standard-Assets-Characters) | Unity Companion License; the source notice and license text are included at `Assets/ThirdParty/UnityStandardAssets`. |
| WeChat Mini Game Tuanjie adapter SDK | Git package `com.qq.weixin.minigame` and the tracked `Assets/WX-WASM-SDK-V2` compatibility mirror | [wechat-miniprogram/minigame-tuanjie-transform-sdk v0.1.32](https://github.com/wechat-miniprogram/minigame-tuanjie-transform-sdk/tree/v0.1.32) | MIT License; the upstream copyright and license text are included at `Assets/ThirdParty/WeChatMiniGameSDK`. |

## Assets created for this project

Project-authored assets (for example, scripts, prefabs, materials, scene data,
and `Assets/Resources/Art/EchoSky.png`) are intended to be covered by the
repository's MIT license only where the copyright holder is entitled to grant
that license. If an asset is later found to incorporate third-party work, add
its attribution and terms to this file before redistributing it.

## EchoRun Sans SC provenance

- Upstream source: official `notofonts/noto-cjk` tag `Sans2.004`, static
  Simplified Chinese Regular OTF.
- Upstream SHA-256:
  `2C76254F6FC379FDDFCE0A7E84FB5385BB135D3E399294F6EEB6680D0365B74B`.
- Bundled subset SHA-256:
  `0CB2A65839604754D09BAFA67E7F447750032DF4038B04B3B7897B1B033D6623`.
- Bundled subset: 457,256 bytes, 1,053 Unicode characters, including the
  asynchronous challenge panel, invitations, results and leaderboard text.
- The subset retains the upstream Adobe copyright and Google trademark
  acknowledgement, but its user-facing family and PostScript names are
  `EchoRun Sans SC` and `EchoRunSansSC-Regular` so it cannot be mistaken for
  an unmodified upstream font.
- Rebuild with FontTools 4.55.0:
  `python Tools/Fonts/build_echorun_font.py <official-2.004-regular.otf>`.
  The pinned input hash, Unicode set, naming, weight and static-font checks are
  enforced by the script.
- Verify the bundled font against the pinned Unicode list and actual WeChat
  C# string literals after changing UI copy:
  `python Tools/Fonts/check_echorun_font.py`.

## Release checklist

Before publishing a Release, verify that every bundled asset has an entry
above or is original work whose rights you control. Remove or replace any
asset with missing or incompatible redistribution terms. Package-manager
dependencies remain subject to the licenses distributed with their packages.

## Historical source notice

This repository begins with a new parentless source snapshot. The historical `Assets/Fonts/arial.ttf` file and the development repository's old Git/PR references are not migrated. Older source-repository audit records are retained only as historical documentation.

## Runner motion sources and local-only dependencies

### Quaternius contribution to ReadyIdle

`Assets/Animations/HumanMotion/RunnerAthletic/RunnerReadyIdle.anim` is generated
from the Quaternius **Universal Animation Library Standard** Idle motion and the
existing `Assets/Animations/HumanMotion/HumanIdle.fbx` support pose. The
[author's pack](https://quaternius.com/packs/universalanimationlibrary.html)
contains a **CC0 1.0 Universal** license. The unmodified license is retained at
`Assets/Animations/HumanMotion/Quaternius/License.txt`. The HumanIdle contribution
remains subject to the Unity Companion License recorded above and included at
`Assets/ThirdParty/UnityStandardAssets`; the combined clip must not be described
as wholly original MIT-only animation data.

The included `RunnerAthleticClipAuthor.cs` recipe can regenerate the public
athletic clips when its documented source inputs are present. Any included
`RunnerRunningJump.anim` or `RunnerRunningLand.anim` uses the same Quaternius
library and existing Unity Standard Assets reference/support data. These are
earlier candidates, not the accepted local FlowFit jump/landing bindings.

### Human Basic Motions FREE: required but not redistributed

The accepted local `RunnerFlowFitJump` and `RunnerFlowFitLand` motions are derived
from **Human Basic Motions FREE** by Kevin Iglesias and the existing Unity
Standard Assets running motion. Obtain the private source directly from the
[author's download page](https://kevdev.itch.io/basic-motions-free) and review the
[author's license section](https://www.keviniglesias.com/#license). The terms
recorded when the project acquired the asset on 2026-09-26 identify the Standard
Unity Asset Store EULA, including free downloads, and restrict distribution of
reusable standalone assets. Free download does not grant the repository an MIT
relicensing or reusable-source redistribution right.

The original package, extracted model/FBX/mask files and generated reusable Flow
animation clips are therefore excluded from this public repository, including
their private `.meta` files. Each developer must obtain a permitted copy and
perform the local setup. Game-product distribution remains subject to the
applicable asset terms; this notice does not grant additional rights.

The committed controller records the accepted private Jump/Land references. A
fresh checkout is **not animation-complete** without that local setup; there is
no automatic fallback. The project-authored recipe is included without the
private motion curves. See [Runner motion dependencies and setup](docs/RunnerMotionDependencies.md)
for exact inputs, helper methods, reconstruction steps and verification limits.
