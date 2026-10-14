# Core Journey Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make defeat understandable, encounters varied, and the complete game usable with a controller.

**Architecture:** Extend the existing pooled Godot/C# game. Keep encounter selection and zone classification in a small deterministic `RunDirector`; keep input actions in `InputSetup`, persistent preferences/rewards in `SaveData`, and presentation in `Hud`. Existing collision constraints remain authoritative.

**Tech Stack:** Godot 4.7.2 .NET, C#, .NET 8 target, GL Compatibility.

**Spec:** `docs/superpowers/specs/2026-10-14-core-journey-design.md` (approved by user on 2026-10-14).

## Global Constraints

- Preserve core at 20 000 m, exit at 40 000 m, acceleration, emergence and camera turn.
- Preserve 64×64 player collisions and `SafePassage` radius/width/amplitude constants.
- Preserve old saves, upgrade/skin prices, bounded pools and 6/3 crystal-light budgets.
- Ukrainian player-facing copy; no rasterized UI text.
- Work inline in this chat. Restore the real save after test runs.
- Generate extra PNGs only if existing resources cannot distinguish zones.

## Review Focus

- Reused pooled dash obstacles must reset warning state/alpha and retain clearance.
- A controller disconnect or neutral right stick must not snap the lantern or block keyboard input.
- The same seed with cosmetic RNG activity must preserve encounter choices; full physics determinism is not claimed.
- Scene reload/next lap must reset one-shot presentation, preserve wallet and avoid duplicate rewards.
- A small viewport and additional settings must keep menus usable and the central route visible.

## Task 1: Fairness and defeat feedback

**Files:** Modify `Scripts/Player.cs`, `Scripts/Hud.cs`, `Scripts/Obstacle.cs`, `Scripts/LevelGenerator.cs`; create `Tools/JourneySmoke.cs` and `Tools/JourneySmoke.tscn`.

**Interfaces:** Add `public enum DeathCause { Collision, Overheat }`; `Hud.ShowGameOver(DeathCause cause = DeathCause.Collision)`. Expose legitimate hazard state `Obstacle.WarningActive` and `Obstacle.WarningLeadSeconds = 0.9f`; internal death method becomes `Die(DeathCause cause = DeathCause.Collision)`.

- [ ] Write behavioral assertions in JourneySmoke: overheat and collision display different reasons; shield collision does not finish a run; dash warning leads actual dash by at least 0.9 s at 1078 px/s and resets on reuse; hot water can retain an interval above 4 s after entering the zone. Use reflection only to trigger private behavior or inspect an existing timer, not production test helpers.
- [ ] Build/run before implementation. Expected RED: missing new feedback/hazard state (first use reflection/name lookups to avoid compilation errors).
- [ ] Implement reason propagation and text, upright unshaded warning anchored to hazard, lead-time trigger based on relative travel time plus 0.25 s reserve, warning reset, and hot-zone edge tracking. Keep post-motion `ProtectPassage`. Reserve clear water approaches based on 380 px/s lateral travel and remaining vertical time.
- [ ] Build; run JourneySmoke, SafePassageSmoke and CaveSmoke. Expected GREEN: reason/shield/warning/timer assertions pass, corridor remains clear.
- [ ] Record changed files and results in the execution ledger.

## Task 2: Deterministic encounters and seed

**Files:** Create `Scripts/RunDirector.cs`; modify `Scripts/LevelGenerator.cs`, `Scripts/Obstacle.cs`, `Scripts/Water.cs`, `Scripts/Hud.cs`; extend JourneySmoke.

**Interfaces:** `public enum EncounterKind { Slalom, Debris, CoinTrail, WaterApproach, Rest }`; `public sealed class RunDirector` with `RunDirector(int seed)`, `int Seed`, `EncounterKind EncounterAt(int index)`, `float LaneAt(float distance)`, `int NextInt(int min, int max)`, `float NextFloat(float min, float max)`. LevelGenerator exposes `int RunSeed`, `EncounterKind Encounter`, `RunDirector Director`; static `int? RequestedSeed` is consumed once on scene creation. `Obstacle.ConfigureVariant` and `Water.GenerateRandomCaveGeometry` consume `level.Director` when present, preserving standalone-scene fallback.

- [ ] Add real behavior tests: compare encounter sequences and lane values for equal/different seeds, consume cosmetic `GD.Rand*` between calls, and force pool exhaustion without changing later encounter choices. Expected RED: director absent / sequences not reproducible.
- [ ] Implement dedicated gameplay RNG and a stateless seeded encounter sequence separate from decorative RNG. Anchor encounter boundaries to travelled distance, choose duration 6–10 s and 3–5 s rest converted using entry speed; do not advance during pause. Suppress dashes in first 8 s. Reuse pools for patterns and optional coin trails; keep water higher priority than hazards. Preserve ±60 px lane, enforce <=80 px/s lane motion at maximum speed.
- [ ] Add seed input in main menu, seed display in pause/game over, and explicit same-seed replay button. Invalid input remains in menu with a short Ukrainian error, ordinary restart chooses a new seed.
- [ ] Build; run JourneySmoke, SafePassageSmoke, ProgressionSmoke. Expected GREEN: deterministic decisions, safe corridor, next lap and bounded pools.
- [ ] Record results in ledger.

## Task 3: Complete controller input and accessibility

**Files:** Modify `Scripts/InputSetup.cs`, `Scripts/Player.cs`, `Scripts/Hud.cs`, `Scripts/SaveData.cs`, `Scripts/Obstacle.cs`; extend JourneySmoke and ShopSmoke.

**Interfaces:** InputSetup adds actions `ToggleLantern`, `Pause`, `Back`, `AimLeft/Right/Up/Down`; ensure bindings add missing events even to existing actions without duplicate registration. SaveData adds `float CameraShake = 1f`, `float FlashIntensity = 1f` persisted in `accessibility`. Input handling moves to `_UnhandledInput` for keyboard and joypad events.

- [ ] Write assertions for injected Y/Start/B events, right-stick aim in rotated world, neutral-stick retention, repeated Ensure without duplicate events, old-save defaults and 0% shake/flash values. Expected RED: actions/preferences absent or events ignored.
- [ ] Bind F/Y, Esc/Start and B; map right stick with deadzone 0.2. Track latest active aim device and keep last stick direction; transform by camera rotation. Preserve standard Godot ui_accept/ui navigation and explicit focus recovery. Add independently persisted settings, scale continuous/death shake, soften flash/invulnerability/warning modulation at 0 without hiding hazard markers.
- [ ] Ensure pause content can scroll on short viewports. Build; run JourneySmoke, ShopSmoke, VisualSmoke and GameplaySmoke. Expected GREEN: controller input, persistence, mouse aim, pause and restart.
- [ ] Record results in ledger.

## Task 4: Progress HUD and action-driven onboarding

**Files:** Modify `Scripts/Hud.cs`, `Scripts/Player.cs`, `Scripts/LevelGenerator.cs`, `Scripts/SaveData.cs`, `Tools/GameplaySmoke.cs`; extend JourneySmoke.

**Interfaces:** SaveData adds `bool TutorialComplete`; Hud exposes `void NotifyTutorialMove()`, `void NotifyTutorialLight()`, `void NotifyTutorialWater()`, `void BeginTutorial()`. LevelGenerator exposes `bool TutorialWaterPending` from HUD onboarding state.

- [ ] Test that elapsed time alone cannot complete onboarding, actions complete steps in order, skip/repeat work, and progress handles 0/20 000/40 000 plus next lap. Expected RED: no action-based tutorial/progress.
- [ ] Replace COINS with МОНЕТИ and update existing coin assertion. Build a reused sidebar progress bar with core marker and clear destination text; preserve bonus/cooling timers. Add first-run tutorial, skip/replay buttons and early safe water encounter. Store completion with backwards compatible default false.
- [ ] Build; run JourneySmoke, GameplaySmoke, ShopSmoke. Inspect HUD/menu at 1280×720 and a narrow viewport using renderer screenshots. Expected GREEN: progression and tutorial behaviors, unclipped UI.
- [ ] Record results in ledger.

## Task 5: Zones, core event and mastery rewards

**Files:** Extend `Scripts/RunDirector.cs`; modify `Scripts/LevelGenerator.cs`, `Scripts/Hud.cs`, `Scripts/AudioManager.cs`, `Scripts/Player.cs`, `Scripts/WindTrails.cs`, `Scripts/SaveData.cs`; update README.md, CHANGELOG.md, ART_ASSETS.md; extend JourneySmoke and CoreBonusSmoke.

**Interfaces:** `public enum CaveZone { Basalt, Crystal, Magma, Core }`; `RunDirector.ZoneAt(float depth, float coreDepth)` uses distances >12000, >8000, >3000, otherwise Core. `Hud.AnnounceCore()`; `AudioManager.SetZone(CaveZone zone)` and `PlayCoreAccent()` use existing sounds. SaveData exposes `int MasteryMask`, `bool UnlockMastery(int bit)` and selected trail setting; one-time unlocks never credit coins. Player counts actual burning extinguishes in one run.

- [ ] Test zone boundaries in both directions, core announcement once per lap, 2 s respite without reducing speed, one-time unlocks, unchanged wallet/skin hitbox and old-save defaults. Expected RED: absent event/zones/rewards.
- [ ] Recompose existing basalt/crystal/magma resources and ambient sound by zone; add brief core banner/audio accent and hazard respite. Unlock trail colors for first core, first lap and three actual extinguishes in one run. Add menu trail selection for unlocked colors. Keep decoration/light budgets unchanged.
- [ ] Measure planned/collected coin counts and gameplay time across reproducible test runs; document the difference between automated route samples and human earnings. Leave prices unchanged until human playtest evidence exists. Inspect actual rendering; generate/document the specified crystal background only if needed.
- [ ] Build; run all eight smoke scenes sequentially with save restoration: Gameplay, Cave, Progression, Shop, SafePassage, CoreBonus, Visual, Journey. Capture/inspect menus, cold cave, warning, core and defeat. Verify a complete traversal with collisions disabled only for the traversal regression, and report that distinction.
- [ ] Update docs with actual checks and remaining manual tests. Review full diff with a fresh reviewer per executing-plans skill, fix important findings, and leave changes reviewable without merge/push.

## Commands and baseline

Use `dotnet build <absolute project path> --nologo`. Engine is `C:/Users/User/Documents/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe`. Pass `--path <absolute game directory>` and `res://Tools/<Name>Smoke.tscn`. Geometry/shop/logic can use `--headless`; screenshot scenes need the renderer. Always read pass/fail output and process exit code; a frame timeout is not a passing test.

Baseline 2026-10-14: build 0 warnings/errors; SafePassageSmoke 16 440 positions PASS; ShopSmoke all assertions PASS, save restored by its finally block. Renderer gameplay remains to be run. Import reported duplicate UID warnings for root copies of existing audio/font assets.

Plan self-review: all five approved stages have corresponding tasks; test input classes from Review Focus are assigned above. Interfaces and constants align with spec. No new prices or physics changes are planned.
