# ui-check

Verification tooling for the **viewer's graph display**: given a DWG/DXF (or a headless
screenshot PNG), it audits how the dynamic-block evaluation graph will be *drawn*
(see `src/ACadSharp.Viewer/Services/GraphModel.cs` — the model `NodeGraphView` renders).
Complements `eval-probe` (which checks what the graph *computes*).

Preserved debug tooling — **do not delete** (moved from `.tmp-shots/layoutcheck/`).

## Contents

| File | What it is |
| --- | --- |
| `Program.cs` / `ui-check.csproj` | The dispatcher (no arg → usage; one of the modes below). |
| `CheckAll.cs` | **Default mode** — layout audit: for every dynamic block, for every property, builds the `GraphModel` ancestor subgraph and reports edges whose provider sits **to the right of** the consumer (against the left-to-right flow), flagged as `lookup` (expected, dashed) or `!! NON-LOOKUP !!` (a layout bug). |
| `AnalyzePng.cs` | `--analyze` — pixel scan of a screenshot (via `Avalonia.Headless` + `ILockedFramebuffer`): counts and bounding-boxes the node colors (Parameter/Grip/Action/Component/FeedbackOrange, #3D7EBF/#3D9E5F/#C77B3D/#7A7A7A/#E8A33D). |
| `DiagPorts.cs` | `--diag` — per block: every edge (index, from/to node + type + Id, flags) plus the target node's input connections (`EvalConnection`/property connections, lookup columns). |
| `DumpGraph.cs` | `--graph` — per block: every node (type, `ElementName`, in/out edge indices) and every edge (index, from → to, flags, `TrackedCount`). |

## How to run

```bash
cd tools/ui-check
NUGET_PACKAGES=$PWD/.tmp-nuget DOTNET_ROLL_FORWARD=Major dotnet run --project ui-check.csproj -- [mode] <file>...
#   (no mode)  file.dwg file.dxf ...      → layout audit (CheckAll)
#   --analyze  screenshot.png ...         → pixel color scan
#   --diag     file.dwg ...              → edge + input-port dump
#   --graph    file.dwg ...             → full node/edge dump
```

## Capturing the screenshots to analyze

Produce the PNGs with the Viewer's built-in headless mode (no display needed — the pipeline and its gotchas, e.g. the `UseHeadlessDrawing=false` + real Skia renderer requirement, are in [`docs/agents/ui-verification.md`](../../docs/agents/ui-verification.md)):

```bash
NUGET_PACKAGES=$PWD/.tmp-nuget DOTNET_ROLL_FORWARD=Major dotnet run --project src/ACadSharp.Viewer/ACadSharp.Viewer.csproj -- --screenshot out.png samples/dynamic-blocks/BLOCKLOOKUPPARAMETER.dwg dialog
```

## What the output shows

- **Default**: `BLOCKLOOKUPPARAMETER.dwg: 0 of 22 edges go right-to-left` — `0` is the
  pass state; any `!! NON-LOOKUP !!` line is a real layout regression.
- `--analyze`: per color, the pixel count and the bounding box (`x=[..] y=[..] center=(..)`)
  — e.g. a node box drawn at the wrong position or the wrong color shows up here.
- `--diag` / `--graph`: the raw parsed graph, for cross-checking against the DXF/DWG source.

## What it is useful for

- Regression-checking **viewer layout changes** (edge direction, wire fan-out) after
  touching `GraphModel` / `NodeGraphView` — no display needed, runs on any file.
- Verifying a headless screenshot (produce one with the viewer's built-in
  `--screenshot` mode, see `src/ACadSharp.Viewer/Screenshot.cs`, then `--analyze` it)
  without eyeballing the PNG.
- Debugging "why is this port/wire missing" by comparing `--diag` (parsed connections)
  against `--graph` (the raw stored edges).

## Environment notes (this machine)

- .NET SDK 11; only the 10/11 runtimes are installed → `DOTNET_ROLL_FORWARD=Major` is required.
- `~/.nuget` is read-only → `NUGET_PACKAGES=$PWD/.tmp-nuget` (the cache lives in the repo root).
- `Avalonia`/`Avalonia.Headless` 12.1.3 restore fine from the normal feed into that cache;
  the hand-assembled `headless.nupkg` that used to sit in `.tmp-headless/` was a
  throwaway workaround and is superseded.
