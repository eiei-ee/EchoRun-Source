# Roadmap

This roadmap distinguishes verified current behavior from planned work. It is
not a promise of delivery dates.

## Current public alpha

- Playable WebGL build and Windows/Android build pipelines
- Keyboard, mouse-drag, and touch input
- Pooled procedural tracks with deterministic safe-route validation
- Local behavior-cloning shadow with cross-run generations
- Contextual-bandit track direction with delayed feedback
- Persistent progression, four consumable power-ups, audio, and settings
- EditMode and PlayMode regression coverage

## Published `v0.2.0-alpha.1` pre-release

- Immutable active-generation snapshots and separately trained pending state
- Retry-invariant policy, sequence, style, pace, clarity, and contract input
- Fuzzy echo fallback for interrupted but minimally useful calibration
- Finite 75-second calibration and 190-second challenge targets
- Six-stage, time-aware duel with stability, counterattack, prediction, rewrite,
  and a final 25-second closing race
- Contract-only marker coins, residual lane-style learning, distance-based
  director attribution, contract-plan reward isolation, and obstacle-free turns
- Two-hit player collision flow with a first-hit recovery window
- 193 EditMode and 19 PlayMode tests passing, plus a clean WebGL build,
  byte-for-byte public deployment verification, and live-browser gameplay
  validation

The reviewed tag, GitHub pre-release, and verified WebGL deployment are public.
Windows and Android packages remain outside this release until separately built
and validated.

## Near term

- Playtest the full 190-second dramatic curve with new and existing save data
- Tune first-generation echo lead without weakening retry invariance
- Add deterministic run seeds and shareable diagnostic reports
- Expand accessibility options and mobile device coverage
- Publish performance budgets for WebGL, Windows, and representative Android
  hardware
- Add focused contribution issues with acceptance criteria

## Before 1.0

- Stabilize save and balance configuration formats
- Document the AI training data schema and evaluation methodology
- Add reproducible release packaging and checksums for Windows and Android
- Complete an asset-provenance audit for every distributable file
- Establish compatibility and deprecation policies

## Possible future work

- Reusable sample packages for the shadow runner and safe track generator
- Seeded replay tooling for bug reports and AI comparisons
- Opt-in, privacy-reviewed aggregate telemetry
- Server-authoritative leaderboards only if a secure backend is introduced

Community proposals are welcome, but new scope must preserve route safety,
offline playability, platform performance, and license clarity.
