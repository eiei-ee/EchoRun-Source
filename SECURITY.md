# Security Policy

## Supported Builds

Only release builds produced from the current default branch are supported.
Windows, Android, and iOS release builds use IL2CPP. Development builds,
script debugging, and unsigned test artifacts must not be distributed as
official releases.

## Trust Model

EchoRun is currently an offline, client-authoritative game. A player who owns
the device can modify local saves, process memory, assemblies, WebAssembly, or
game files. Client-side checks can raise the cost of tampering, but they cannot
make local scores or AI state authoritative.

If scores, rewards, tournaments, or leaderboards become shared between users,
the server must own score calculation, run validation, rewards, and replay
acceptance. Client-provided totals must be treated only as claims.

## Build And Dependency Rules

- Pin GitHub Actions to audited full commit SHAs.
- Keep workflow permissions at the least privilege required by the job.
- Do not persist checkout credentials into third-party build steps.
- Preserve reviewed package lock files during CI builds.
- Never commit editor, licensing, signing, or build logs.
- Never store signing keys, store credentials, API secrets, or anti-cheat
  secrets in the client or repository.
- Review every new native plugin, managed DLL, package registry, and dynamic
  content source before it enters a release build.

## Runtime Rules

- Keep deterministic track safety independent of learned AI state.
- Treat PlayerPrefs, save data, telemetry, and imported model state as
  untrusted input.
- Do not load arbitrary native libraries, managed assemblies, scripts, or
  executable content from player-writable locations.
- Release hashes help players verify downloads, but code signing is required
  to establish publisher identity.

## Reporting

Do not open a public issue for an undisclosed vulnerability. Use
[GitHub private vulnerability reporting](https://github.com/eiei-ee/EchoRun-Source/security/advisories/new)
and include the affected revision, platform, reproduction steps, expected
impact, and any suggested mitigation. The maintainer will acknowledge a valid
report as soon as practical and coordinate disclosure after a fix is ready.
