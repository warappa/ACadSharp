# eval-regression

Research/debug tooling for a **forward + reverse evaluation regression check** over every
dynamic block in a DWG file. For each block it activates all grips, runs `Evaluate()`
and `EvaluateReverse()`, counts failures, and prints a summary. For the named block of
interest (currently `Callout Bubble - Imperial` — the L3-02 block with the lookup
action) it dumps the topological order, the invertible (flag-4) edges, and the lookup
actions' `CurrentValue` after each pass.

Preserved debug tooling — **do not delete** (moved from `.tmp-verify-reverse/`).

## Contents

| File | What it is |
| --- | --- |
| `Verify.cs` / `verify.csproj` | The runnable tool: `DwgReader` → per-block `Activate(grips)` + `Evaluate()` + `EvaluateReverse()` → per-block OK/FAILED + summary. |

## How to run

```bash
cd tools/eval-regression
NUGET_PACKAGES=$PWD/.tmp-nuget DOTNET_ROLL_FORWARD=Major dotnet run --project verify.csproj -- <path-to-dwg>
```

No argument → `samples/dynamic-blocks/BLOCKLOOKUPPARAMETER.dwg` (the sample that contains
a lookup action, i.e. the flag-4 / reverse edges).

## What the output shows

- `block '<name>' (nodes N, edges M)` + `forward: OK/FAILED   reverse: OK/FAILED` per block.
- For the block of interest: the forward and reverse topological order, every
  `Invertible` edge with its classification (`REVERSE (action→param)` vs
  `FORWARD (param→action)`), and each `BlockLookupAction`'s `CurrentValue` after the
  forward pass and after the reverse pass.
- `=== summary: N dynamic block(s), forward failed X, reverse failed Y ===`

## What it is useful for

- A fast **regression sweep** of the evaluation engine on a whole DWG (e.g. after
  changing `EvaluationGraph` or a node's `Evaluate`).
- Checking the **reverse (lookup) evaluation** path: a lookup action's `CurrentValue`
  is only produced by the reverse pass (forward evaluation skips flag-4 edges), so
  "after forward pass" should show the pre-evaluation sentinel and "after reverse pass"
  the matched-row value.
- Inspecting the invertible-edge pairs (`reverseEdge` cross-references) for a
  block with lookup chains.

## Environment notes (this machine)

- .NET SDK 11; only the 10/11 runtimes are installed → `DOTNET_ROLL_FORWARD=Major` is required.
- `~/.nuget` is read-only → `NUGET_PACKAGES=$PWD/.tmp-nuget` (the cache lives in the repo root).
