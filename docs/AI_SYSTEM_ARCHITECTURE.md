# AI System Architecture

## Responsibility boundary

The runtime has two AI layers with one-way authority:

1. `AITrackDirector` owns session-level intent: observe, recovery, flow,
   pressure, or record push.
2. `AIShadowRunner` owns the shadow's concrete movement decisions.

The director must not choose `Left`, `Right`, `Jump`, or `Slide`. It publishes a
`ShadowAIDirective` through `IShadowDirectiveSource`. The shadow may use that
directive to tune style strength, risk, and decision noise, but its safety layer
always has final authority over physically required actions.

This contract is the replacement seam for a future global AI. A new global AI
only needs to implement `IShadowDirectiveSource` and be supplied through
`AIShadowRunner.SetDirectiveSource`; the shadow scorer and animation code do not
need to change.

## Player model

`PlayerStyleData` is the persistent, explainable player model. Version 3 tracks:

- aggressiveness: `0..1`
- jump timing: `-1..1`, early to late
- slide frequency: `0..1`
- lane preference: `-1..1`, left to right
- rhythm stability: `0..1`
- recovery style: `0..1`, conservative to urgent

Every value carries sample counts through the same object. `Confidence` is
derived from evidence, so a new profile remains close to the existing behavior
cloning policy until enough observations exist. `StyleTracker` is the only
runtime writer and `EchoRunSaveSystem` is the persistence boundary.

Lane preference is learned as residual choice relative to the route's offered
incentive center. This prevents safe-lane placement and coin trails from being
misread as an intrinsic left/right preference. Legacy raw-lane profiles are
normalized during migration instead of carrying the biased measurement into a
new contract.

`EchoGenerationSnapshot` is the versioned challenge boundary. It deep-copies
policy weights, sequence transitions, pair count, normalized style JSON, pace,
clarity, and generation number. Inputs from the current run train a separate
pending generation and cannot alter the active opponent. Only a finish with a
broken contract and player lead promotes the pending state; retrying a loss
reconstructs the same active generation byte-for-byte.

## Echo Contract pipeline

`EchoContractPolicy` converts the frozen style snapshot into one bounded,
explainable rule primitive:

1. `BreakLaneHabit` moves the high-value safe route away from the learned lane;
2. `ChangeVerticalHabit` creates a rewarded challenge lane whose obstacle type
   requires the less-used jump or slide action;
3. `DisruptRhythm` alternates high and low obstacle requirements so a repeated
   action sequence is no longer optimal.

`EchoContractEvaluator` measures only successful, relevant counter-behaviour.
Counter-actions add stability; repeating the learned habit reduces it and adds
pressure to the echo. Stability must reach 100% during resistance, is reset to
55% when the echo counterattacks, and must reach 100% again before the contract
is broken. `AITrackDirector.ApplyEchoContract` writes the selected rule into the
next `AITrackPlan`, and `TrackManager` realizes it while keeping an independent
safe lane open. Lane contracts count only coins explicitly tagged as contract
markers, not ordinary rewards. The learned model proposes the personal rule,
but deterministic route validation still owns physical safety.

`EchoDuelFlow` makes the confrontation time-aware:

1. Detection (`20s`) observes without allowing a premature break;
2. Reveal (`8s`) discloses the learned habit and rule;
3. Resistance opens the first stability objective;
4. Counterattack applies the 55% reset and a stronger track response;
5. Rewrite remains the long pursuit phase; only its first `32s` boosts learning;
6. Finale starts in the last `25s`, preserving a genuine closing race.

Challenge victory is the conjunction `contract completed && player lead >= 0`.
Distance, coins, dodges, and score remain feedback signals but cannot bypass the
AI-generated objective. The UI exposes the same contract before the run, during
the duel, and in the post-run learning report. During play it also exposes duel
phase, prediction, stability, and lead. The diagnostic dashboard remains
available separately for technical inspection.

The first calibration aims for complete action coverage. If an interrupted run
has enough valid time and samples to seed a model but misses full coverage, the
next run may use a fuzzy echo with reduced clarity. This preserves continuity
without representing partial evidence as a fully learned player.

## Decision pipeline

`AIShadowPolicy` and `AIShadowSequencePolicy` produce contextual base scores.
`ShadowDecisionMaker` then applies the frozen style profile and current global
directive:

1. contextual base scores;
2. lane, vertical-action, rhythm, risk, and recovery style modifiers;
3. feasibility filtering;
4. emergency safety override;
5. deterministic run-seeded weighted selection.

Obstacle reaction remains event-sensitive between regular decision ticks.
Jump timing changes the reaction distance, while an emergency window prevents a
learned late style from creating unavoidable failures.

Telemetry schema 4 stores the base, style-adjusted, and final candidate scores,
the feasible-action mask, safety override, active directive, and frozen style
snapshot for every opponent decision. The optional live diagnostic panel shows
the same pipeline in play mode and refreshes at four times per second.

## Platform budget

- Regular shadow decisions use the existing fixed interval (`0.35s` by default).
- Obstacle reaction is a small per-frame query and contains no model training.
- Style lane sampling is reduced to two samples per second.
- The model is pure managed C# plus `Mathf`; it has no desktop-only API and uses
  the same code path on desktop, mobile, WebGL, and the mini-game target.

## Invariants for future work

- Global AI publishes intent; it does not issue character actions.
- Style modifies valid choices; it never bypasses feasibility or safety.
- Player and shadow observations must not be mixed within the active generation.
- A failed or abandoned retry must preserve policy, sequence, style, pace,
  clarity, generation number, and contract source data.
- Director rewards must not train on plans that the active contract rewrote.
- Director observations mature by physical route distance, not generation call
  count; turns remain obstacle-free under every pacing state.
- A challenge cannot be won by distance or score without completing its frozen
  Echo Contract.
- Add new persistent fields through a save-data version bump and normalization.
- Keep decision randomness run-seeded so telemetry and replays are reproducible.
