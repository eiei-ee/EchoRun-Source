# Architecture

ECHO//RUN is an offline, client-side Tuanjie/Unity game. Runtime systems are
implemented in C# and the playable WebGL build does not call a hosted AI API.

## Runtime flow

1. `GameManager` owns menu, play, pause, result, and restart state.
2. `TrackManager` maintains pooled track segments and plans safe lanes before
   placing obstacles and rewards.
3. `PlayerController` normalizes keyboard, pointer-drag, and touch gestures into
   lane, jump, and slide actions.
4. `AIShadowRunner` loads one immutable `EchoGenerationSnapshot` as the active
   opponent and trains a separate pending policy, sequence model, style, and
   pace candidate during the run.
5. `EchoDuelFlow` and `EchoContractEvaluator` coordinate the detection, reveal,
   resistance, counterattack, rewrite, and finale phases.
6. `AITrackDirector` uses contextual-bandit feedback to adjust later track
   pacing and composition. Its observation window is bound to generated route
   distances, and plans changed by the active contract are excluded from
   director reward updates.
7. Deterministic spawn rules retain final authority over reachability even when
   learned systems request a harder layout.
8. Save, progression, power-up, audio, UI, and telemetry systems remain local.

## Generation transaction

The active generation contains behavior weights, sequence transitions, the
normalized player-style snapshot, target pace, clarity, and generation number.
It is deep-cloned at the challenge boundary and cannot be mutated by current-run
observations. A completed challenge promotes the pending state only when the
player reaches the finish, breaks the contract, and leads the echo. Collision,
abandonment, timeout, loss, and same-generation retry retain the exact active
snapshot.

Legacy profiles are normalized on load. A partial first-run calibration can
seed a fuzzy echo when it meets the minimum evidence threshold; it records
reduced clarity instead of pretending that the model is fully trained.

## Duel and course boundary

Calibration and challenge courses are finite time-targeted runs. The default
balance targets are 75 and 190 seconds, converted into course distance from the
same accelerating speed curve used at runtime. A challenge moves through six
visible phases. The contract must first reach full stability, survives a
counterattack reset to 55%, and must reach full stability again before it can
be considered broken. The finale starts only in the final time window.

Contract lane actions use explicitly tagged marker coins. Ordinary pooled
coins reset that marker state and cannot advance a lane contract. The first
player collision triggers a slowdown/recovery state; the second ends the run.

## Safety boundary

The learned systems may select among bounded plans, but they cannot bypass
lane-count limits, route continuity, obstacle compatibility, or lifecycle
reset rules. Turn segments remain obstacle-free; pressure changes how often a
turn is selected rather than making turns unsafe. Tests cover these
deterministic rules independently of model state.

## Persistence

Settings, progression, training state, and telemetry are stored through local
player storage. WebGL synchronizes persistent data to IndexedDB. Local data is
not authoritative for shared scores or competitive rewards.

## Build boundary

`BuildConfig` applies platform settings and generates WebGL, Windows, and
Android outputs. GitHub Actions uses a self-hosted Tuanjie runner, runs both
test modes, then builds all three targets sequentially.

## Extension points

- Add bounded track plans through the balance/configuration layer.
- Add power-ups through `PowerUpId`, balance definitions, inventory, runtime
  activation, and UI as one reviewed unit.
- Add new media only after documenting provenance and redistribution terms.
- Treat server-backed leaderboards or shared rewards as a separate trust
  domain with server-side validation.
