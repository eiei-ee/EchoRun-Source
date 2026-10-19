# ECHO//RUN — Current requirements

This document describes the current public-alpha scope. It supersedes the
original prototype notes and avoids presenting ECHO//RUN as an imitation of
another game.

## Product loop

The player calibrates a local style model, receives a human-readable Echo
Contract generated from that frozen style, then races the corresponding
non-colliding AI shadow. Each contract changes a playable rule and names the
counter-behaviour required to break it. A run is won only when the player both
completes the counter-objective and finishes ahead of the echo; score is a
process metric, not the primary victory condition. A contextual-bandit director
adapts pacing while deterministic validators retain final authority over route
safety.

## Required player experience

- Three-lane movement, jumping, sliding, obstacle avoidance, collectibles,
  scoring, death, restart, pause, and persistent local progress.
- Keyboard, mouse-drag, and touch input, with responsive WebGL and mobile
  layouts.
- A locally executed behavior-cloning shadow that trains from player actions
  and is frozen for the next run.
- A per-generation Echo Contract derived from lane, vertical-action, or rhythm
  habits. It must alter track rewards or obstacle composition and expose a
  measurable counter-objective.
- A challenge must stage detection, reveal, resistance, counterattack, rewrite,
  and finale as distinct states. Initial contract completion must survive a
  counterattack and a second stability objective before the contract is broken.
- Pre-run learned-trait/rule/objective disclosure, live contract progress and
  pressure/recovery feedback, and post-run learning/next-generation analysis.
- An adaptive track director whose choices cannot produce an impossible route.
- Clear calibration, contract progress, win/loss, and error feedback.

## Engineering requirements

- Route generation must preserve at least one valid route; learned AI can
  influence pacing but cannot bypass deterministic safety checks.
- Distance lead without contract completion must never produce a challenge win.
- Current-run observations may train only the next generation; the active
  opponent, style snapshot, and contract stay frozen for the duel.
- A challenge loss, collision, abandonment, or same-generation retry must not
  mutate active policy weights, action-sequence data, style, pace, clarity, or
  contract input.
- Contract-modified track plans must not train the director policy. Director
  observations are settled by physical route distance, and turn segments must
  remain obstacle-free under all pacing states.
- Ordinary coins must not advance a lane contract; only explicit contract
  markers may count. Lane style must be measured relative to the route offer so
  generated incentives do not become a false player preference.
- Recurring runtime track content must use pooling where practical.
- EditMode and PlayMode regression suites must cover gameplay and restart
  behavior.
- WebGL, Windows x64, and Android are build targets. A target may be described
  as an official download only after its release artifact has been tested and
  attached to GitHub Releases.
- Source, dependencies, and bundled assets must have documented redistribution
  rights. Signing keys, credentials, editor licenses, generated builds, and
  local training exports must not enter the repository.

## Out of scope for the public alpha

- Online leaderboards, rewards, or any client-trusted competitive scoring.
- Cloud inference or hosted player telemetry.
- Claims of platform availability without a tested release artifact.

## Source of truth

- [README.md](README.md) for the public project overview and supported
  downloads.
- [PRODUCT.md](PRODUCT.md) for the product intent.
- [docs/ROADMAP.md](docs/ROADMAP.md) for planned work.
- [docs/BUILDING.md](docs/BUILDING.md) for reproducible setup, tests, and
  builds.
