# AGENTS.md

Guidance for AI coding agents working in this repository.

## Commit cadence (important)

After a **sound, reasonable change package** — a completed phase or a completed todo entry — make a **git commit** to reflect that committable increment and progress. Do not let changes accumulate unstaged across unrelated work.

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
- The `net48` target is for the .NET Framework (Windows) and does not build/run on Linux; use `net9.0` for local verification.

## Project layout

- **ACadSharp** — a .NET library for reading/writing AutoCAD files (DXF/DWG) and evaluating dynamic-block graphs.
- `src/ACadSharp` — the main library (objects, entities, IO, evaluations).
- `src/ACadSharp.Tests` — xUnit tests.
- `src/ACadSharp.Examples` — runnable examples (`eval` / `evalgraph` subcommands drive the evaluation engine).
- `docs/` — articles and plans; `reference/` — research material; `samples/` — test DWG/DXF files (incl. `samples/dynamic-blocks/`).
