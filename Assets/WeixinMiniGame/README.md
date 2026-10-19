# WeChat MiniGame v0

This directory contains the WeChat-only presentation layer and the saved
Tuanjie Build Profile. The gameplay and AI implementations remain in
`Assets/Scripts` and compile from the shared `TempleRun.Runtime` assembly.

## Required official platform package

Tuanjie 2022.3.62t8 requires the official WeChat conversion package
`com.qq.weixin.minigame` (`WX-WASM-SDK-V2`). This project pins the version
required by the installed Tuanjie module (`0.1.32`) from WeChat's official
GitHub repository. The build also accepts the legacy SDK layout under
`Assets/WX-WASM-SDK-V2`.

## Build

```powershell
& 'D:\unity\tuanjie\2022.3.62t8\Editor\Tuanjie.exe' `
  -batchmode -quit `
  -projectPath 'C:\Users\zzz\Desktop\TempleRun' `
  -buildTarget WeixinMiniGame `
  -minigamesubplatform weixin `
  -executeMethod BuildConfig.BuildWeixinMiniGameV0
```

The converted output path is `Builds/WeixinMiniGameV0-Clean`.
Import its `minigame` subdirectory into WeChat DevTools.

Set a valid Mini Game AppID only for the build process when IDE validation is
required:

```powershell
$env:WECHAT_MINIGAME_APPID = 'wx...'
```

For WeChat DevTools, pass the token at runtime through
`WECHAT_DEVTOOLS_CLI_TOKEN`; do not commit it to this repository.

## Resource loading and local preview

The current city version exceeds the bundled SDK's package-size budget.
The SDK falls back to external resource loading even when the saved profile
requests an embedded data package. Conversion success alone does not mean
the exported game can start.

For a deployed build, set `WECHAT_MINIGAME_CDN` to the HTTPS resource directory
before building. The resource file emitted directly under `webgl/` must be
served at that URL with its generated filename. Configure the corresponding
WeChat download domain and gzip/Brotli transport compression before phone
testing. The build does not upload resources or configure hosting.

CloudBase storage can host these static resources alongside the existing cloud
function. Use a separate, versioned prefix such as
`echorun/releases/<build-id>/`; upload only the generated runtime data file,
not the repository, symbols, project configuration or deployment credentials.
Keep older prefixes available while older game packages still reference them.
This storage access is separate from the three private echo database collections.

Before setting `WECHAT_MINIGAME_CDN`, verify that the resource prefix provides
stable HTTPS access without a temporary signature or login. A `cloud://` file ID
or the CLI's temporary download URL is not a release resource URL. The SDK appends
the generated filename, so configure the directory URL, not the full file URL.
Check anonymous retrieval, decoded size and SHA-256 against the local export,
the environment's storage/traffic allowance, and actual phone startup.

Upload original data bytes first. HTTP gzip/Brotli is optional for initial
connectivity verification, but should be configured and checked for release
loading performance. Compressed bytes require the matching `Content-Encoding`
response header at the runtime URL; renaming a `.gz` file to the original filename
without that header corrupts the response seen by the loader. Keep hosting and
header checks separate from the SDK's data-package compression setting.

The locally installed official storage CLI has a 30-second wrapper timeout.
An upload or download can report timeout while its underlying transfer continues.
Before retrying an upload, query the exact object and verify its size/hash; do not
overwrite an already correct release object solely because the task reported a
timeout. For downloads, check completion and the full hash before using the file.
Authenticated management access does not prove anonymous CDN access: test the
unsigned HTTPS URL separately before writing it into a game package.

For local DevTools verification only:

1. Export and configure the existing Mini Game AppID in the generated
   `minigame/project.config.json` (never in tracked source).
2. Run `node Tools/WeChat/local-preview.cjs` from the repository root.
3. Keep the server running and compile the exported project in DevTools.

The helper binds only to `127.0.0.1:18765`, serves the exported data file,
and sets the generated game's resource URL. The existing DevTools project
must permit local development URLs. This address is not reachable from a
phone and must not be used for upload or release. Rebuilding replaces the
generated configuration; rerun the helper after rebuilding.

For same-network phone debugging (including a computer connected to the
phone's hotspot), run `node Tools/WeChat/local-preview.cjs --host <PC-WLAN-IPv4>`.
The address must belong to this computer and be a private IPv4 address.
The helper binds only to that interface. Check `http://<PC-WLAN-IPv4>:18765/health`
from the phone, then regenerate the DevTools remote-debug QR code and scan it.
An earlier QR code can retain the old resource URL. Use the existing DevTools
development-domain setting for this HTTP preview; this is not a release setup.
Do not disable the firewall: if it blocks the phone, use a narrowly scoped
temporary inbound rule for the phone address and port 18765.

The WeChat runtime uses the shared responsive UI, forces touch layouts,
and fixes portrait orientation. `WeChatSafeArea` converts WeChat viewport
coordinates to Unity pixels and reserves space below the menu capsule.
Inset covers prevent the 3D scene from leaking above menu backgrounds.
Browser-only frame scheduling is excluded; the WeChat bootstrap sets its
preferred frame rate separately.

## Package notes

- The bundled OFL-licensed EchoRun Sans SC font is a project-named subset of
  Noto Sans CJK SC 2.004. `OFL.txt` remains beside it; the pinned source hash,
  Unicode set and reproducible build script are recorded in
  `THIRD_PARTY_NOTICES.md` and `Tools/Fonts/`.
- The September 18 city export has an approximately 74 MiB external data file.
  Debug symbols are excluded by `project.config.json`. Re-measure package
  sizes for each export; older v0 size figures do not describe this city build.
- The platform presentation layer adds no AI dependency. The SDK downloads
  the external resource package when package-size fallback is active.

## Async frozen-shadow challenges

The optional challenge layer lives in `TempleRun.WeixinMiniGame`, which references
the shared runtime and the pinned official `Wx` SDK. The runtime does not reference
the SDK. Windows, ordinary WebGL and Editor use an unavailable transport and do not
initialize cloud services. No custom jslib is required: the adapter uses the existing
`WX.cloud.Init/CallFunction`, `ShareAppMessage`, launch options and `OnShow/OffShow`.

Cloud setup is **not enabled by default**. Copy `AsyncEchoCloudSettings.example.json`
to the local, untracked `Assets/Resources/Weixin/AsyncEchoCloudSettings.json`, keep
`functionName` as `echo`, and set `cloudEnvironmentId` to the test environment.
Set `featureEnabled` to true only after deployment and real cloud calls pass.
Do not commit real deployment configuration.
Missing/disabled/invalid configuration hides the optional entry; normal single-player
gameplay continues. AppID remains a build-time setting described above. No token or
server credential belongs in the client. Rebuild after changing this resource.

Deploy the self-contained function under `Tools/CloudFunctions/echo/`; see its
README for the pinned `wx-server-sdk`, Node runtime, lockfile installation, commands
and exact permissions. The function uses the cloud-context OPENID, never a client
identity claim. Create `echo_shadows`, `echo_results` and `echo_scores`, each with
schemaVersion 1 and **no client direct read/write permission**. In `echo_scores`,
configure and verify the composite index `boardId ASC, distanceMeters DESC,
playerLeadMeters DESC, achievedAt ASC, _id ASC`. Function code, collections and
index must all be deployed before enabling the client. `cloudfunctionRoot` must
point to `Tools/CloudFunctions/` through a durable deployment project/configuration,
not a one-off edit to generated files that a Unity rebuild overwrites.
For Windows deployment, follow the cloud README's current packaging guidance.
The locally inspected DevTools full-deploy implementation preserves backslashes in
nested ZIP paths. Its full upload returned success while function initialization
failed; uploading the same locked dependencies through the official normalized
incremental path restored expected API validation. No original cloud
`MODULE_NOT_FOUND` log was obtained, and this finding is not a claim about every
DevTools version. Do not treat `cloud_fn_deploy --remote-npm-install=false` success
alone as proof that dependencies are usable.

From the repository root, `python Tools/CloudFunctions/package_echo.py --output
Builds/echo-NEW.zip` produces a portable ZIP and SHA-256 manifest, checks required
SDK files and slash-separated paths, and refuses existing output files. The output
directory must exist. Prefer that package with a verified full ZIP replacement
deployment entry; the exact console ZIP upload UI has not been verified here.
Packaging does not deploy. For the current official CLI alternative, first create
`echo` in the console with Nodejs20.19 and a 5-second timeout, then use
`cloud_fn_inc_deploy --appid <appid> --env <env> --path <absolute-echo-directory>
--file .` with the authorized client. See the cloud README for the complete command.
The current full-deploy create default is Nodejs16.13 / 3 seconds; local engines or
config fields do not replace the explicit cloud configuration check.

Incremental deployment adds/replaces files and **does not delete old entries**.
The current environment recovered after `--file node_modules`; malformed entries
from the earlier full upload may remain. File deletion/renaming requires a separately
verified replacement procedure. Honor DevTools write confirmations: pending/taskId
is not success, and should not trigger a duplicate upload. After the final result,
verify function settings and actual SDK calls before enabling/rebuilding the client.
Single-identity publish/get/repeated-report/minimal-board checks have succeeded;
Unity gameplay, sharing delivery and two-identity acceptance remain separate gates.
For CLI smoke scripts, use ASCII source with Unicode escapes for Chinese literals
and `--args-file` for complex arguments: this Windows path previously changed a
Chinese comparison constant, causing a false leaderboard assertion.

Ordinary SingleContract settlement publishes the player's committed, challenge-ready
`ActiveEchoIdentity.ToJson()` string (maximum 16,384 UTF-8 bytes). A successful cloud
receipt enables sharing with `inviter=<openid>&shadow=<identityId>&rules=1`.
Snapshots are immutable by owner/identity/rules; publishing a newer identity leaves
old cards intact. A share call only requests the WeChat share UI; it does not prove
delivery. The player must press Share; background publish does not open the UI.

Cold launch and foreground `OnShow` use the same parser. The launch/onShow duplicate
is merged; a consumed card does not repeatedly open a modal when returning from
the background. **Recent invitation** remains available in the menu to accept the
same card again. Invitations received while running or paused keep only the newest
invitation until the menu. Choosing single-player invalidates pending gets. Accepting
an invitation requires a complete, strictly validated payload and a separate Start
Challenge action; no server response can switch an already-started run.

AsyncChallenge uses the frozen opponent without changing the local player's identity,
training, wallet or records. The result's immutable board and challenge ID determine
the report, even after a scene reload or another invitation arrives. Each new run
has a new ID; automatic retry and **Confirm upload again** reuse the original ID
and fields. The async result page does not publish/share the opponent. Its challenge
entry shows upload status. The board is an anonymous list of participants challenging
that exact frozen shadow, **not a WeChat contacts leaderboard**. Empty data and errors
have distinct displays; there is no polling or reward, and client scores are not
authoritative. Different local director baselines can produce different obstacle
layouts, so this MVP is not an equal-course competitive ranking.

Each cloud action has at most two attempts, 5 seconds each within one 10-second total
budget. The clock ignores game timeScale. On returning from an OS suspension the
original deadline is checked before queued callbacks, and an exhausted total budget
does not start another request. Permanent validation/configuration errors do not
retry. Late/duplicate responses cannot change a newer invitation or run. A network
timeout means “unconfirmed”, not “the server never received it”; users can explicitly
confirm the latest result again. There is no persistent outbox, no local identity
write, and no infinite background retry. Cloud errors produce at most one short
nonblocking notice per operation; a running async challenge continues offline and
retains async settlement semantics.

Use `AsyncEchoPlatformTests` for fake-transport lifecycle, timeout, query, publish
immutability, reporting and empty/error leaderboard checks. Tests need no live cloud.
The existing three-platform CI means Windows/WebGL/**Android**; the Weixin build
and real sharing are separate acceptance gates.

Before claiming end-to-end acceptance, use two authorized test WeChat identities:

1. A completes normal calibration/challenge, publishes and explicitly opens Share.
2. B without a local identity cold-starts from the card, confirms the frozen opponent,
   finishes a challenge and sees the report/board. Repeat with B already running.
3. Retry the same report and verify one result document; run again and verify a new ID.
4. A publishes a new generation; verify the old card still fetches the old snapshot.
5. Exercise offline, missing cloud configuration, get/report/board failure, background
   longer than 10 seconds, recent-invitation re-entry and normal single-player recovery.

Both phones need access to the exported game resources; localhost-only DevTools
preview is insufficient. Real cloud deployment, permissions/index checks, two-user
sharing and phone behavior are **not established by source compilation or fake tests**.
Record their actual evidence separately. Disable the client feature flag/cloud entry
to stop new cloud operations; do not delete local saves or convert in-flight Async
runs to promotion runs. MVP collections have no automatic TTL: monitor database size,
function errors and cost, and retain result/idempotency records together.

## Friend challenge UI authoring

`AsyncEchoPanel` presents cloud state through the serialized
`Assets/Resources/UI/AsyncEchoSheet.prefab`; it does not build the full page at
runtime. Edit the prefab in Unity for visual tuning. The explicit editor command
**Tools > EchoRun > Rebuild Friend Challenge UI** recreates the baseline layout
from `Editor/AsyncEchoPanelPrefabBuilder.cs`; running it overwrites prefab layout
edits, so keep lasting baseline changes in that builder as well. The editor
assembly references the platform layer; the core runtime does not reference it.

The warm-paper surface is opaque and intercepts underlying menu input. The
challenge and leaderboard have separate pages, a fixed challenge action area,
and scrollable content for small screens. Publishing/sharing, invitation retry,
and report retry remain bound to the existing service and settlement lifecycle.
Leaderboard rows display untrusted player labels as plain text and are reused
within the service's top-50 limit. No visual preview performs cloud requests.

The new illustration is `Resources/Art/UI/EchoChallengeDuo.png`. Its built-in
image-generation prompt and provenance are in `ArtSource/UI/EchoChallengeDuo.md`.
The importer caps the UI texture at 1024 pixels, disables mipmaps and read/write,
and uses platform compression. Chinese copy in both the presenter and prefab
builder is covered by `Tools/Fonts/check_echorun_font.py`; extend the existing
pinned font subset when new characters are introduced.

`AsyncEchoPanelVisualTests` uses the real prefab with a fake transport and
renders portrait, compact portrait, and landscape states in a graphics-enabled
PlayMode run. These captures validate Unity layout, state presentation and UI
hit testing; they do not stand in for WeChat phone/share acceptance.
