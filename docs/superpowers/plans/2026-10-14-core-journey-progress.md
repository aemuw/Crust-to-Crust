# SDD ledger — plan: docs/superpowers/plans/2026-10-14-core-journey.md

User approved spec, plan and in-place branch codex/core-journey. Base: c178530.

Pre-flight: Tasks 1/3 share warning visibility; accessibility must preserve static warning. Tasks 2/4 share early water; tutorial overrides encounter hazards. Tasks 2/5 share RunDirector and zone weighting; deterministic zone choice does not consume decorative RNG. Tasks 3/4/5 share SaveData; all added fields use backwards-compatible defaults. Interfaces consistent.

Ruling: Use this Windows-compatible persistent ledger instead of POSIX helper scripts — the bundled helpers require a Unix harness; evidence remains reviewable — cost: bookkeeping is manual.

Task 1: in progress. Baseline build, SafePassageSmoke and ShopSmoke pass.

Task 1: JourneySmoke RED (5 failures) → GREEN (6 behavior checks); C# build clean; SafePassageSmoke PASS.
Task 1: Ruling: CaveSmoke injected logical Keycode while production InputMap uses PhysicalKeycode — set both in synthetic test events — cost: this improves test fidelity, not physical keyboard coverage.

Task 2: Director tests RED (dedicated director absent) → GREEN (seed independence, varied plans, bounded lane); ProgressionSmoke PASS including next lap and pause. C# build clean.
Task 2: Ruling: distance-anchored encounter lengths use entry speed to convert the specified durations — prevents frame-count dependence — cost: actual seconds vary while acceleration changes within an encounter.
Task 3: RED: Y toggle, Start pause and independent accessibility settings absent. Implementation built clean; scene tests running.

Task 3: ShopSmoke PASS for legacy defaults and comfort persistence; VisualSmoke PASS for mouse aim and frozen shaders. Controller buttons, neutral aim, idempotence and zero shake pass. Rotated stick test exposed a transform mismatch; local lantern rotation now explicitly subtracts player world rotation, pending fresh regression.
Task 4: onboarding RED (action-based learning absent) → GREEN for ordered actions, no timer completion and progress at 0/20k/40k.
Task 5: RED (depth zones absent); implemented zones, core signal/respite and cosmetic trails; C# build clean. Full rendered suite running.
