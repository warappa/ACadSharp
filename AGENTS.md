# AGENTS.md

Guidance for AI coding agents working in this repository.

## Commit cadence (important)

After a **sound, reasonable change package** — a completed phase or a completed todo entry — make a **git commit** to reflect that committable increment and progress. Do not let changes accumulate unstaged across unrelated work.

But: If a user reported a bug and and a bugfix is being developed, then don't commit automatically but ask for user approval to check if the bug is actually fixed.

- Group by **coherent unit** (one logical change, verified green), not per tiny edit.
- A good commit is a self-contained, meaningful increment with a clear message (imperative subject + short body).
- Stage only the files belonging to the change (exclude editor/tooling dirs such as `.vscode/`).
- Build and test the change **green before** committing.

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
- `tools/` — preserved debug/research tools, each self-documenting in its own `README.md`: `dwg-rawdump` (bit-level DWG object-section decoder), `eval-probe` (dump a dynamic block's node values after evaluation), `eval-regression` (forward + reverse evaluation sweep over a whole DWG), `graph-dump` (dump the viewer's `GraphModel` display model), `ui-check` (layout audit of the graph display + screenshot pixel analysis). Throwaway scratch lives in git-ignored `.tmp-*` dirs; a tool that proves reusable gets moved to `tools/`.

## Deep-dive docs

Topic docs for the areas that need more than the above (read on demand):

| Topic | File | Open when you |
| --- | --- | --- |
| Evaluation engine — **our implementation** (value model, engine semantics, class hierarchy, value-semantics archetypes, per-class `Evaluate` formulas, the 8-touch-point recipe for adding a node type) | [`docs/agents/evaluation-engine.md`](docs/agents/evaluation-engine.md) | add or fix an evaluation node type, or reason about engine behavior |
| Headless UI verification (screenshot capture pipeline, the `tools/ui-check` + `tools/graph-dump` toolkit, gotchas) | [`docs/agents/ui-verification.md`](docs/agents/ui-verification.md) | render or capture the Viewer UI without a display |
| Evaluation graph — **the AutoCAD specification** (on-disk format, node/edge records, the connection model, the lookup table, 2008 history) | [`docs/articles/evaluation-graph.md`](docs/articles/evaluation-graph.md) | decode or validate file-format fields |

The two evaluation docs are complementary: the article documents **what AutoCAD writes**; `docs/agents/evaluation-engine.md` documents **what we do with it**.
