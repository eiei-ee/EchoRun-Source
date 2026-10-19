# Contributing to ECHO//RUN

Thanks for helping improve ECHO//RUN. Contributions that strengthen gameplay
reliability, Tuanjie compatibility, accessibility, documentation, testing, or
the reusable AI systems are welcome.

## Before opening a change

1. Search existing issues before filing a duplicate.
2. Use an issue for behavior changes or larger designs so scope can be agreed
   before implementation.
3. Keep pull requests focused. Separate refactors from behavior changes where
   practical.

## Development environment

- Tuanjie Engine `2022.3.62t8`
- Git with LFS disabled; this repository does not currently require Git LFS
- Windows for the repository's self-hosted three-platform CI

See [docs/BUILDING.md](docs/BUILDING.md) for setup, tests, and build commands.

## Engineering rules

- Preserve at least one valid route through every generated obstacle pattern.
- Learned AI may influence pacing and choices, but deterministic validators
  must retain final authority over track safety.
- Preserve keyboard, mouse-drag, and touch input unless a change explicitly
  replaces all affected paths.
- Prefer pooled runtime objects for recurring track content.
- Do not hand-edit Tuanjie `.meta` identifiers.
- Do not commit `Library`, `Temp`, `Builds`, logs, credentials, licenses,
  signing keys, or local training exports.
- Add or update EditMode/PlayMode coverage for regressions and behavior changes.

## Asset and dependency policy

Every new font, image, model, audio file, native plugin, DLL, package, or other
third-party artifact must include:

- its original source URL;
- the author or publisher;
- a license that permits repository and build redistribution; and
- an entry in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) when bundled.

Do not submit assets copied from proprietary games or assets with unknown
provenance. Project-authored assets should be identified as such in the pull
request.

## Pull request checklist

- Explain the user-visible or maintainer-visible outcome.
- Link the related issue when one exists.
- Run EditMode and PlayMode tests.
- State which of WebGL, Windows, and Android were built or manually tested.
- Include screenshots or a short capture for visual changes.
- Confirm that no secrets, generated build output, or unlicensed assets are
  included.

By contributing, you confirm that you have the right to submit the work and
agree that your contribution is licensed under this repository's MIT License,
except for third-party material explicitly documented under its own terms.
