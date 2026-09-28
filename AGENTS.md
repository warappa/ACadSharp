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
- The `net48` target is for the .NET Framework (Windows) and does not build/run on Linux; use `net9.0` for local verification.

## Project layout

- **ACadSharp** — a .NET library for reading/writing AutoCAD files (DXF/DWG) and evaluating dynamic-block graphs.
- `src/ACadSharp` — the main library (objects, entities, IO, evaluations).
- `src/ACadSharp.Tests` — xUnit tests.
- `src/ACadSharp.Examples` — runnable examples (`eval` / `evalgraph` subcommands drive the evaluation engine).
- `docs/` — articles and plans; `reference/` — research material; `samples/` — test DWG/DXF files (incl. `samples/dynamic-blocks/`).
- `tools/` — preserved debug/research tools, each self-documenting in its own `README.md`: `dwg-rawdump` (bit-level DWG object-section decoder), `eval-probe` (dump a dynamic block's node values after evaluation), `eval-regression` (forward + reverse evaluation sweep over a whole DWG), `graph-dump` (dump the viewer's `GraphModel` display model — edges with wire index/kind/label, ports), `ui-check` (layout audit of the graph display + screenshot pixel analysis). Throwaway scratch lives in git-ignored `.tmp-*` dirs; a tool that proves reusable gets moved to `tools/`.

## UI verification (headless)

The viewer can be run **headless** (no display/X11/GPU) to render and capture the real UI — the pipeline lives in the repo:

- **Capture**: the Viewer's built-in `--screenshot <out.png> [file.dwg|file.dxf] [mode]` (`src/ACadSharp.Viewer/Screenshot.cs`, wired in `Program.cs`). Modes: `dialog` (open the node viewer for the first dynamic block), `tip` (first-run teaching tip), `flyout` (settings flyout), `hc`/`accent` (high-contrast theme / red accent), `interact` (simulate wheel zoom, drag pan, click select, hover through the real input pipeline), `zoombug`/`minimap` (mini-map rectangle tracking + navigation), `fingerprint` (deterministic layout fingerprint: node positions, edge labels + multiplicity, scale/pan, visible rect — a cheap regression check without pixels). It writes a `diag.txt` (setup, load, dialog, capture timestamps) next to the output.
- **Analyze**: `tools/ui-check` (`--analyze` scans a screenshot for the node colors + bounding boxes; default mode audits the `GraphModel` layout for provider-right-of-consumer anomalies; `--diag`/`--graph` dump the parsed connections/raw edges).
- **Inspect the display model directly (no rendering)**: `tools/graph-dump` (per-parameter `GraphModel` ancestor subgraph: edge lines with wire index + `lookup`/`value` kind + label, input/output ports with wire index).

Gotchas learned while building this:

- **`UseHeadlessDrawing=true` (the default) produces no pixels**: it installs a no-op render-interface stub that never locks the window's framebuffer, so `GetLastRenderedFrame()` is always null. Instead build the app manually with `UseHeadlessDrawing=false` + the real Skia renderer (the `Avalonia.Skia` package): Skia's `FramebufferRenderTarget` locks the headless window's framebuffer (`Lock()`) and draws real pixels that `CaptureRenderedFrame()` can read back.
- **Everything must run on one dedicated thread**: the `Dispatcher` created during `SetupWithoutStarting()` binds to that thread and becomes the UI thread.
- **Pump the dispatcher to let work run**: `Dispatcher.UIThread.RunJobs()` + a short sleep (the `Pump(n)` helper in `Screenshot.cs`) so posted continuations and background-priority work (e.g. the dialog auto-fit) complete before capturing.
- `Avalonia.Headless` 12.1.3 (and `Avalonia.Skia` 12.1.3) restore fine from the normal feed into `.tmp-nuget`; the hand-assembled `headless.nupkg` that used to sit in `.tmp-headless/` was a throwaway workaround.

## Evaluation engine (dynamic-block graphs)

The evaluation engine lives in `src/ACadSharp/Objects/Evaluations/`.

### Value model
- `EvaluationValue` (sealed) is a shape-agnostic value holder. `EvaluationValueType` has **8 shapes**: `None`, `Double`, `Point` (3D), `Point2d`, `String`, `Int`, `Char`, `ObjectId`.
- `EvaluationValue` factories: `None`/`FromDouble`/`FromPoint(XYZ)`/`FromPoint2d(XY)`/`FromString`/`FromInt`/`FromChar`/`FromObjectId(long)`; safe accessors (`DoubleValue`, `PointValue`, `Point2dValue`, `StringValue`, `IntValue`, `CharValue`, `ObjectIdValue`); `As<T>()` throws on a shape mismatch.
- `EvaluationContext` is a `Dictionary<int, Dictionary<string, EvaluationValue>>` (node id → port name → value). `SetValue(int, string, EvaluationValue)` + `double`/`string` overloads (there are **no** `int`/`long`/`char` overloads — use the `EvaluationValue` overload for those).
- `EvaluationExpression` (base node) has `CurrentValue` (`EvaluationValue`, `internal set`, not serialized) and `virtual bool Evaluate(EvaluationContext)` (default no-op). A leaf with one value shape adds a typed `new EvaluationValue<T> CurrentValue => base.CurrentValue.As<T>()` override. It also has `protected abstract GetDefaultValue()`: when a node has not yet been evaluated, `CurrentValue` falls back to this, so **every concrete node must explicitly declare a default** (a stateful node → its initial value, e.g. a grip's zero `Displacement`; a stateless one → `EvaluationValue.None`). The abstract method makes a missing default a compile error (CS0534).

### Graph structure
- `EvaluationGraph` holds `Nodes` (each wraps an `EvaluationExpression`) and `Edges`.
- An `Edge` connects `FromNodeIndex` (source/output) → `ToNodeIndex` (target/input).
- A `Node` has `FirstInEdge`/`LastInEdge` (incoming-edge list) and `FirstOutEdge`/`LastOutEdge` (outgoing-edge list); an `Edge` is linked via `PrevInEdge`/`NextInEdge` (per ToNode) and `PrevOutEdge`/`NextOutEdge` (per FromNode). Walk a list by following `Next*Edge` until it is `-1` (`Node.GetIncomingEdges()`/`GetOutgoingEdges()` do this).
- A node's **inputs** = incoming edges; **outputs** = outgoing edges. An `EvalConnection` (on a node) is a port-level input: `Id` = source node id, `Name` = the source port to read.

### Adding a new evaluation node type (8 touch points)
1. `DxfFileToken.cs` — `ObjectXxx = "XXX"` (the DXF object name).
2. `DxfSubclassMarker.cs` — `Xxx = "AcDbXxx"` (the subclass marker).
3. `Objects/Evaluations/Xxx.cs` — the class. For a parameter, inherit `BlockParameter`; add `[DxfName]`/`[DxfSubClass]` attrs, a `Value` with a `[DxfCodeValue]`, an `Evaluate` that writes the `"Value"` port (`context.SetValue(this.Id, "Value", EvaluationValue.FromX(...))` + set `base.CurrentValue`), a typed `CurrentValue` override, a `GetDefaultValue()` override (**required** — the method is abstract: a stateful node returns its initial value, e.g. a grip's zero `Displacement`; a stateless node returns `EvaluationValue.None`), and `GetDxfClass()` (ItemClassId=499, MaintenanceVersion=55, ProxyFlags=EraseAllowed|CloningAllowed|DisablesProxyWarningDialog).
4. `IO/Templates/CadXxxTemplate.cs` — the template (`: CadBlockParameterTemplate`, typed property + parameterless and object-taking ctors).
5. `IO/DXF/DxfStreamReader/DxfObjectsSectionReader.cs` — `readXxx` (specific codes; `default` falls through to `readBlockParameter`) + a dispatch case.
6. `IO/DXF/DxfStreamWriter/DxfObjectsSectionWriter.cs` — `writeXxx` (`writeBlockParameter` + `Write(100, marker)` + per-code `Write`) + a dispatch case.
7. `IO/DWG/DwgStreamReaders/DwgObjectReader.Objects.cs` — `readXxx` + a dispatch case in `DwgObjectReader.cs`.
8. `IO/DWG/DwgStreamWriters/DwgObjectWriter.Objects.cs` — `writeXxx` + a dispatch case.

Then: build all 6 TFMs, add tests (evaluate → assert `CurrentValue.Type` + payload; a DXF round-trip), run the suite, commit.

### Gotchas
- No `#nullable` context — never use `?` on reference types (CS8632).
- `WriteBitLong`/`ReadBitLong` are the DWG 64-bit primitives, but `WriteBitLong` takes an `int` — cast a `long` value.
- The DXF writer only writes objects reachable from the root dictionary; a standalone test object must be referenced (e.g., added to `document.RootDictionary`) to be written.
- `CadDictionary.Add` triggers `OnAdd` → `CadDocument.AddCadObject`; do not call `AddCadObject` first (double-add throws).
- The test project has `InternalsVisibleTo` access, so `internal` members (e.g., `AddCadObject`, `internal set` properties) are testable.
- Pre-existing failures (not caused by new work): `ArcTests.CreateFromBulgeTest`, `ArcTests.GetCenter`. Pre-existing benign warnings: CS1658/CS1001 in `MultiLeaderPropertyOverrideFlags.cs`.
