# graph-dump

Research/debug tooling for inspecting the **display model** of a dynamic block's
evaluation graph — i.e. what the viewer's `NodeGraphView` will *draw*: for every
parameter (or the ones matching a name substring), it builds the `GraphModel` ancestor
subgraph (`src/ACadSharp.Viewer/Services/GraphModel.cs`) and dumps each edge line
(→/from node, **wire index**, `lookup` vs `value` kind, label) and each node's ports
(input/output port names + wire index). Complements `eval-probe` (which checks what the
graph *computes*); `ui-check` audits the same model for layout anomalies.

Preserved debug tooling — **do not delete** (moved from `.tmp-verify2/`).

Part of the headless UI-verification toolkit (capture with the Viewer's `--screenshot` mode, analyze with `ui-check`, inspect with this tool) — the pipeline and its gotchas are in the [`ui-verification` skill](../../.agents/skills/ui-verification/SKILL.md).

## Contents

| File | What it is |
| --- | --- |
| `Dump.cs` / `graph-dump.csproj` | The runnable tool: `DwgReader` → per matching block → per parameter → `GraphModel.BuildAncestors` → edge + port dump. |

## How to run

```bash
cd tools/graph-dump
NUGET_PACKAGES=$PWD/.tmp-nuget DOTNET_ROLL_FORWARD=Major dotnet run --project graph-dump.csproj -- <path-to-dwg|dxf> [name-substring]
```

- No substring: dump the display model of **every parameter** of every dynamic block.
- With one: only blocks/parameters whose name or `ElementName` contains the substring.

## What the output shows

Per matching parameter (e.g. `--- 'Linear' (node 0): 2 node(s), 2 edge line(s) ---`):

- `edge <from> -> <to>  wire=<n>  [lookup|value]  label='<port name>'` — one line per
  **wire** (parallel connections to the same port get distinct wire indices, so a
  4-wire fan-out shows as 4 lines). `lookup` = dashed edge (a `BlockLookupAction`
  connection).
- `node <idx> (<Type> '<ElementName>')` + `in: [port[wire], ...]` + `out: [...]` — the
  port view the node box will render (port names with their wire index).

## What it is useful for

- Debugging "why does the viewer draw this wire in the wrong place / missing / doubled":
  the wire index and label come straight from `GraphModel`, so any discrepancy with a
  screenshot is a `GraphModel` bug, not a rendering bug.
- Verifying `BlockLookupAction` fan-out (how many lookup wires feed a parameter).
- Cross-checking parsed port names against the DXF source (label codes `304`/`305`/`306`).

## Environment notes (this machine)

- .NET SDK 11; only the 10/11 runtimes are installed → `DOTNET_ROLL_FORWARD=Major` is required.
- `~/.nuget` is read-only → `NUGET_PACKAGES=$PWD/.tmp-nuget` (the cache lives in the repo root).
