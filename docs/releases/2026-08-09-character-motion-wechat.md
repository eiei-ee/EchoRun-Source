# Character motion and WeChat stability update

This development snapshot replaces the procedural T-pose-like run with
retargeted Humanoid animation and improves the WeChat Mini Game export.

## Player-facing changes

- The Exo Gray player and AI shadow now share authored idle, run, and airborne
  animation.
- Arms bend and swing opposite the legs while running.
- Hips and spine stay centered without lateral translation or side roll.
- Jumping no longer raises both arms rigidly overhead.
- The AI shadow keeps its feet above the track while using the same animation.

## WeChat Mini Game changes

- Clean builds replace the unresolved compression-package template value.
- Every SDK dependency imports a cache-busted version-check module.
- A startup bootstrap defines the compression setting before SDK evaluation.
- Generated JavaScript and JSON are rejected when an unresolved token or an
  unversioned version-check import remains.

When testing a newly exported package in WeChat DevTools, import the new build
directory instead of reusing an older project entry whose module cache may
still contain `check-version.js`.

## Validation

- EditMode: 83/83 passed.
- PlayMode: 11/11 passed.
- Clean WeChat Mini Game build completed successfully.
- Generated WeChat package: 0 unresolved compression tokens, 0 legacy
  version-check imports, and 0 JavaScript syntax failures.

The browser build is published at <https://eiei-ee.github.io/EchoRun/>.
