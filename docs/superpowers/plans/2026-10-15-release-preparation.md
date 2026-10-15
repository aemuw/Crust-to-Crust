# Release preparation implementation plan

> **For agentic workers:** Execute inline with superpowers:executing-plans; request independent final review.

**Goal:** Preserve the current complete developer version on main and create a clean source-release branch without altering gameplay.
**Architecture:** Build an audited staging tree from runtime resource dependencies. Create the release commit using a separate Git index so the developer checkout never loses documents or tools.
**Tech Stack:** Godot 4.7.2 .NET, .NET 8, Python standard library, Git.
**Spec:** User request in this chat, 2026-10-15: keep full developer files on main; release branch excludes documentation, superpowers, tools and unnecessary files; preserve behavior.

- [x] Audit static and dynamic resource references; preserve all runtime code/scenes, import metadata, UID sidecars and required font licenses.
- [x] Add a repeatable release staging/audit utility under developer Tools. Keep its audit report in ignored artifacts.
- [x] Build and verify current developer version; commit latest HUD, font and lighting changes plus developer release tooling to main.
- [x] Stage a source-only release with a compact README. Exclude docs, test/generation tools, source audio, caches and only unreferenced assets.
- [x] Restore/build staging in ExportRelease configuration and verify real runtime scenarios using temporary copies of the developer smoke harness. Remove harness afterward.
- [x] Verify every retained gameplay source/resource is byte-identical to main; check no required dependency is missing; request independent review.
- [x] Create codex/release from main using the staged tree and archive the clean commit. Keep the checkout on main. Publish the resulting refs as the final delivery step.

## Execution ledger

- Ruling: deliver a clean Godot source project, not an executable GitHub Release — no platform/version/tag was requested and export templates are absent.
- Ruling: optimize release contents/import workload; do not refactor working runtime code without profiling evidence, because behavior preservation is mandatory.
- Ruling: use a separate Git index instead of switching/deleting files in the developer checkout; main keeps all developer materials throughout.
- Full staging validation: all 38 smoke scenes passed. Six stale fixtures were corrected only in developer Tools: upgrade-aware battery refill, relative insulation measurement, current HazardWarning type/state, and explicit projectile/heat isolation for timing scenarios. Runtime source was not changed for release preparation.
- Independent review confirmed the 288-file dependency closure and unchanged runtime blobs. The refreshed audit includes both required font licenses.
- Runtime-only Debug/ExportRelease builds and headless startup passed after removing temporary test files. ZIP inventory and every archive blob were verified against the release commit.
