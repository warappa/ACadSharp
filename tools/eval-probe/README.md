# eval-probe

Research/debug tooling for inspecting the **evaluation graph of every dynamic block** in a
DXF file: it activates all grips, evaluates, and dumps each node's type, its
`CurrentValue` (or `<unset>`), and — for parameters — the label, description,
`ElementName`, and stored `Location`.

Preserved debug tooling — **do not delete** (moved from `.tmp-probe/`).

## Contents

| File | What it is |
| --- | --- |
| `Probe.cs` / `probe.csproj` | The runnable tool: `DxfReader` → per-block `Activate(grips)` + `Evaluate()` → per-node dump. |

## How to run

```bash
cd tools/eval-probe
NUGET_PACKAGES=$PWD/.tmp-nuget DOTNET_ROLL_FORWARD=Major dotnet run --project probe.csproj -- <path-to-dxf>
```

No argument → `samples/dynamic-blocks/BLOCKLOOKUPPARAMETER.dxf`.

## What the output shows

- `node <index>: <Type> value=<value>` for every node in the block's graph.
- `<unset>` = the node's `CurrentValue` is `EvaluationValue.None` — i.e. the node is
  **stateless** (actions, `BlockGripLocationComponent`, the table stubs, the proxy) and
  has no stored value, or it has not been evaluated. A stateful parameter/grip showing
  `<unset>` after `Evaluate()` is a bug signal.
- Parameters additionally show `Label`/`Desc` (the per-class string codes, e.g. `305`/`306`
  on a linear parameter, `304`/`306` on a point parameter), `ElementName` (code `300`),
  and — for 1-pt parameters — the stored `Location`.

## What it is useful for

- Verifying the **default-value design** (see `docs/articles/evaluation-graph.md`,
  "The three value-semantics archetypes"): after `Evaluate()`, stateful parameters/grips
  must carry a value; stateless nodes must read `<unset>`.
- Sanity-checking a new node type's `Evaluate()` output without writing a test.
- Cross-checking parsed stored values (labels, locations) against the DXF source.

## Environment notes (this machine)

- .NET SDK 11; only the 10/11 runtimes are installed → `DOTNET_ROLL_FORWARD=Major` is required.
- `~/.nuget` is read-only → `NUGET_PACKAGES=$PWD/.tmp-nuget` (the cache lives in the repo root).
