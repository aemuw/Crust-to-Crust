# Release preparation implementation plan

> **For agentic workers:** Execute inline with superpowers:executing-plans; request independent final review.

**Goal:** Preserve the current complete developer version on main and create a clean source-release branch without altering gameplay.
**Architecture:** Build an audited staging tree from runtime resource dependencies. Create the release commit using a separate Git index so the developer checkout never loses documents or tools.
**Tech Stack:** Godot 4.7.2 .NET, .NET 8, Python standard library, Git.
**Spec:** User request in this chat, 2026-10-15: keep full developer files on main; release branch excludes documentation, superpowers, tools and unnecessary files; preserve behavior.

- [ ] Audit static and dynamic resource references; preserve all runtime code/scenes, import metadata, UID sidecars and required font licenses.
- [ ] Add a repeatable release staging/audit utility under developer Tools. Keep its audit report in ignored artifacts.
- [ ] Build and verify current developer version; commit latest HUD, font and lighting changes plus developer release tooling to main.
- [ ] Stage a source-only release with a compact README. Exclude docs, test/generation tools, source audio, caches and only unreferenced assets.
- [ ] Restore/build staging in Release configuration and verify real runtime scenarios using temporary copies of the developer smoke harness. Remove harness afterward.
- [ ] Verify every retained gameplay source/resource is byte-identical to main; check no required dependency is missing; request independent review.
- [ ] Create codex/release from main using the staged tree, archive the clean commit, and publish the requested branches if access permits. Leave the checkout on main.

## Execution ledger

- Ruling: deliver a clean Godot source project, not an executable GitHub Release — no platform/version/tag was requested and export templates are absent.
- Ruling: optimize release contents/import workload; do not refactor working runtime code without profiling evidence, because behavior preservation is mandatory.
- Ruling: use a separate Git index instead of switching/deleting files in the developer checkout; main keeps all developer materials throughout.
