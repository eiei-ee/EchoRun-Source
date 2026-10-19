# Echo experience and browser-playable update

This development snapshot turns EchoRun's prototype menus into one coherent
AI-rival experience and publishes the same build as a browser-playable demo.

## Player-facing changes

- The home screen now presents a clear first-run calibration goal, a single
  primary action, and focused routes to runner, supply, settings and reports.
- Menus, HUD, pause and result screens share a consistent visual system,
  readable Simplified Chinese typography and keyboard/controller focus.
- Touch targets, safe areas and camera composition respond to desktop window
  resizing and the supported mobile layouts.
- Runner appearance, supply purchase/equip actions and the AI training report
  have dedicated screens with clearer state and feedback.
- The echo rival has a more legible shader treatment, diagnostic trace and
  safety-aware behavior while preserving the player's learned style.
- Reduced motion, high contrast and frame-rate settings are applied without
  per-frame preference reads.

## Build and asset reliability

- WebGL builds start from an empty, project-contained output directory and
  restore editor quality/vSync settings even if a build fails.
- The bundled Chinese font is a reproducible, project-renamed static Regular
  subset of Noto Sans CJK SC 2.004 with pinned source and output hashes.
- WeChat SDK discovery uses registered package information instead of a
  timing-sensitive asset lookup immediately after switching platforms.

## Validation

- EditMode: 128/128 passed.
- PlayMode: 15/15 passed.
- Clean WebGL build completed successfully with no stale hashed resources.
- The generated WebGL build loaded over local HTTP with no browser console
  warnings or errors, and the primary action entered a live run.

Browser-playable URL: <https://eiei-ee.github.io/EchoRun/>.
