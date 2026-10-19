> Public source snapshot: GitHub Actions is initially disabled. Historical branches, CI, builds and release records in this document describe the development repository. Additional reusable character/motion sources are omitted; see [PublicSourceDependencies.md](PublicSourceDependencies.md).

# Building and testing ECHO//RUN

## Requirements

- Tuanjie Engine `2022.3.62t8`
- Android modules for APK builds
- Windows IL2CPP modules for Windows builds
- Git

Use the exact editor version recorded in `ProjectSettings/ProjectVersion.txt`.
Package versions are pinned in `Packages/manifest.json` and the package lock
files.

## Open the project

1. Clone the repository.
2. Open the repository root in Tuanjie Engine.
3. Allow the editor to restore packages.
4. Open the enabled scene from Build Settings if it is not already open.

Do not commit generated `Library`, `Temp`, `Logs`, `Builds`, root-level test
result XML, IDE files, or local training exports.

## Tests

In the editor, open **Window → General → Test Runner** and run both EditMode
and PlayMode suites.

The CI workflow runs the same suites before any platform build. PlayMode tests
exercise runtime bootstrap, progression UI, gameplay assets, power-ups, and
the first-run-to-next-generation restart path. EditMode tests also cover active
generation snapshot immutability, failed-retry invariance, duel timing,
contract-marker isolation, director attribution, lane-style normalization, and
the two-hit collision rule.

## Builds

Use the editor menu:

| Target | Menu | Output |
| --- | --- | --- |
| WebGL | `Tools/Build WebGL` | `Builds/WebGL` |
| Windows x64 | `Tools/Build Windows 64-bit` | `Builds/Windows/EchoRun.exe` |
| Android | `Tools/Build Android` | `Builds/Android/EchoRun.apk` |

Equivalent batch entry points are `BuildConfig.BuildWebGL`,
`BuildConfig.BuildWindows`, and `BuildConfig.BuildAndroid`.

The WebGL shell enables IndexedDB persistence, responsive 16:9 sizing, mobile
safe-area behavior, and a capped device-pixel ratio. Windows and Android use
IL2CPP release builds.

## Release evidence gates

Treat the following as separate gates:

1. test suites completed with zero failures;
2. the requested platform build completed successfully;
3. the built artifact loaded in its real runtime;
4. core inputs and visible gameplay were exercised; and
5. published artifacts and checksums match the tested files.

A successful editor test does not prove a player build, and a successful build
log does not prove visible gameplay. Do not publish a target as an official
download until every applicable gate has concrete evidence.

## Continuous integration

`.github/workflows/three-platform-ci.yml` runs on the project's manually
started, self-hosted Windows runner with Tuanjie Engine installed. It performs:

1. EditMode tests;
2. PlayMode tests;
3. WebGL, Windows x64, and Android builds; and
4. artifact and log uploads.

The workflow intentionally contains no editor license or signing credentials.
Do not place local Tuanjie credentials, Android signing keys, or release
secrets in repository files or build logs.
