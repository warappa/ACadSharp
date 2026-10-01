# AGENTS.md

Guidance for AI coding agents working in this repository.

## Commit cadence (important)

After a **sound, reasonable change package** — a completed phase or a completed todo entry — make a **git commit** to reflect that committable increment and progress. Do not let changes accumulate unstaged across unrelated work.

**Always do**

- Group by **coherent unit** (one logical change), not per tiny edit.
- Write a clear message: imperative subject + short body.
- **Build and test the change green before** committing.
- Keep `AGENTS.md` and the docs it references current — update them in the same change that makes them stale (a stale AGENTS.md is worse than none).

**Ask first**

- A bugfix for a user-reported bug: do not commit automatically — present the fix and ask the user to confirm the bug is actually fixed.
- Restructuring `AGENTS.md` or large documentation edits: present the change and ask for review before committing.

**Never do**

- Stage editor/tooling dirs (`.vscode/`, `.tmp-*/`).
- Commit a red (failing) build or test.

## Personal overlay

Machine-specific or personal preferences that are not for the team go in `AGENTS.local.md` at the repo root — it is gitignored and loaded alongside `AGENTS.md` by DeepSeek Harness. (Claude Code uses `CLAUDE.local.md` instead and does not read `AGENTS.local.md`.)

## Build & test

- .NET SDK 11 (C# `latest`, set in `src/Directory.Build.props`).
- Target frameworks — main lib `src/ACadSharp`: `net8.0;net9.0;net10.0;net48;netstandard2.1;netstandard2.0`; tests `src/ACadSharp.Tests`: `net9.0;net48`; examples `src/ACadSharp.Examples`: `net6.0`.
- Build one framework: `dotnet build src/ACadSharp/ACadSharp.csproj -f net9.0 -c Debug`
- Run tests (net9.0): `DOTNET_ROLL_FORWARD=Major dotnet test src/ACadSharp.Tests/ACadSharp.Tests.csproj -f net9.0 -c Debug --filter "FullyQualifiedName~<Name>"`
  - `DOTNET_ROLL_FORWARD=Major` is **required** on this machine: only the .NET 10 and 11 runtimes are installed (no 9.0/6.0 runtime).
- The `net48` target is for .NET Framework (Windows) and does not build/run on Linux; use `net9.0` for local verification.
- The main library has **no `#nullable` context** — never use `?` on reference types (CS8632); tool projects in `tools/` are self-contained and may enable it.

**Writing tests:** the test project has `InternalsVisibleTo` access, so `internal` members (e.g. `AddCadObject`, `internal set` properties) are testable. `CadDictionary.Add` triggers `OnAdd` → `CadDocument.AddCadObject` — do not call `AddCadObject` first (double-add throws). The DXF writer only writes objects reachable from the root dictionary; a standalone test object must be referenced (e.g. added to `document.RootDictionary`) to be written.

**Pre-existing** (not caused by new work): test failures `ArcTests.CreateFromBulgeTest`, `ArcTests.GetCenter`; benign warnings CS1658/CS1001 in `MultiLeaderPropertyOverrideFlags.cs`.

## Project layout

- **ACadSharp** — a .NET library for reading/writing AutoCAD files (DXF/DWG) and evaluating dynamic-block graphs.
- `src/ACadSharp` — the main library (objects, entities, IO, evaluations).
- `src/ACadSharp.Tests` — xUnit tests.
- `src/ACadSharp.Examples` — runnable examples (`eval` / `evalgraph` subcommands drive the evaluation engine).
- `src/ACadSharp.Viewer` — the Avalonia viewer (incl. the headless `--screenshot` capture mode, see the deep-dive docs).
- `docs/` — articles and plans; `reference/` — research material; `samples/` — test DWG/DXF files (incl. `samples/dynamic-blocks/`).
- `tools/` — preserved debug/research tools, each self-documenting in its own `README.md`: `dwg-rawdump` (bit-level DWG object-section decoder, hard-coded to one linear-parameter object), `evalgraph-rawdump` (bit-level decoder for **all six evaluation-graph classes** — the general tool; strong-name signed to reach ACadSharp internals), `eval-probe` (dump a dynamic block's node values after evaluation), `eval-regression` (forward + reverse evaluation sweep over a whole DWG), `graph-dump` (dump the viewer's `GraphModel` display model), `ui-check` (layout audit of the graph display + screenshot pixel analysis), `x11-chrome-probe` (live X11/XWayland window hints: is the WM decorating the window, or the app?). Throwaway scratch lives in git-ignored `.tmp-*` dirs; a tool that proves reusable gets moved to `tools/`.
- `.agents/skills/` — trigger-driven skills (self-contained procedures; see "Skills" below).

## Deep-dive docs

Topic docs for the areas that need more than the above (read on demand):

| Topic | File | Open when you |
| --- | --- | --- |
| Evaluation engine — **our implementation** (value model, engine semantics, class hierarchy, value-semantics archetypes, per-class `Evaluate` formulas, verified sample results) | [`docs/articles/evaluation-engine.md`](docs/articles/evaluation-engine.md) | reason about engine behavior or a node's `Evaluate` formula |
| Evaluation graph — **the AutoCAD specification** (on-disk format, node/edge records, the connection model, the lookup table, 2008 history) | [`docs/articles/evaluation-graph.md`](docs/articles/evaluation-graph.md) | decode or validate file-format fields |
| UI design language — **Fluent 2 spec + local FluentAvalonia 3.0.2 tokens** (design principles, the seven signature experiences with concrete values, taste rules for a dense dev tool; the verified color table, core globals, accent mechanism, `FASymbol` catalog, gotchas; **§10** = the app-drawn titlebar / Avalonia 12 client-side decorations on Linux, **§10.5** = the child dialogs' quieter strip) | [`docs/design/`](docs/design/index.md) | make any deliberate UI decision (color, size, weight, spacing, corner, shadow, motion, hierarchy) — or touch the window chrome |

The two evaluation docs are complementary: the article documents **what AutoCAD writes**; `docs/articles/evaluation-engine.md` documents **what we do with it**.

## Skills (procedures)

Self-contained, trigger-driven procedures in `.agents/skills/`. Harnesses with a skill system load them on demand; if yours does not, open the file directly:

| Skill | Open when you |
| --- | --- |
| [`ui-verification`](.agents/skills/ui-verification/SKILL.md) | render or capture the Viewer UI without a display (headless `--screenshot` pipeline, the `tools/ui-check` + `tools/graph-dump` toolkit, the 4 mandatory gotchas) |
| [`add-eval-node-type`](.agents/skills/add-eval-node-type/SKILL.md) | add or fix an evaluation node type (the 8-touch-point recipe) |
| [`dwg-forensics`](.agents/skills/dwg-forensics/SKILL.md) | a DWG/DXF parses wrong, or you must separate "what AutoCAD wrote" from "what we computed" |
| [`verify-derived-property`](.agents/skills/verify-derived-property/SKILL.md) | inferring a property (value type, shape, default) of one item by analogy to a sibling, or locking in a "systematic" uniform value across many similar items — derive it from the data's type codes, not the class |

**Location policy:** the harness-agnostic files are canonical — `AGENTS.md` + `.agents/`. A harness-specific file (`.claude/CLAUDE.md`, `.github/copilot-instructions.md`, …) is allowed only as a **one-line redirect** to these, never as a content copy. As of 2026 none are needed: Claude Code ≥ 2.1.277, Copilot, Cursor and DeepSeek Harness all read `AGENTS.md` natively, and DSH reads `.agents/skills/` directly.
