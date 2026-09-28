---
name: dwg-forensics
description: Forensically investigate a suspect AutoCAD DWG/DXF object — bit-level raw dump, cross-reference against the on-disk specification, evaluation regression sweep.
whenToUse: When a DWG/DXF does not parse as expected, a field decodes to a wrong value, or you must separate "what AutoCAD wrote" from "what our engine computed".
---

# DWG/DXF forensics

Separate the two questions: **what AutoCAD wrote** (on disk) vs **what we do with it** (engine). Reference material: [`docs/articles/evaluation-graph.md`](../../docs/articles/evaluation-graph.md) (on-disk format, node/edge records, the connection model, the lookup table, 2008 history); [`docs/articles/evaluation-engine.md`](../../docs/articles/evaluation-engine.md) (our implementation).

## 1. Bit-level raw dump (what is actually on disk)

```bash
cd tools/dwg-rawdump
NUGET_PACKAGES=$PWD/.tmp-nuget DOTNET_ROLL_FORWARD=Major dotnet run --project decode.csproj -- <path-to-dwg>
```

A standalone bit-level decoder (no library dependency) that prints the bit-framing of each object in the R2004+ object section. Calibration references live in the same dir: `dump-fresh2.log` (current, post-Id-fix) and `dump-fresh.log` / `dump-full.log`.

Framing facts (full detail in `tools/dwg-rawdump/README.md`):

- Each object: **object type (short) → object data → end handle**. `getEntityType` re-positions the readers per object; there is **no CRC validation in the read path** and `SetPositionInBits` resets the bit-buffer — so a *prefix-read-and-stop* strategy is safe.
- `ReadBitShort`: `00`→2-byte LE (can be negative; −9999 = `0xF601`), `01`→1 byte, `10`→0, `11`→256. `ReadBitLong`: `00`→4-byte LE, `01`→1 byte, `10`→0, `11`→**throws**. `ReadBitDouble`: `00`→8-byte LE, `01`→1.0, `10`→0.0. `Read3BitDouble`: an XYZ = three `ReadBitDouble` in a row.
- **The text region is separate from the main data region**: a string field that reads as `0` in the main data is a **tag10 (=0) pointer into the text region**, not "empty". Do not conclude a label is missing from a `0` in the main data.

## 2. Evaluation state (what our engine computes)

```bash
# node values after evaluation (DXF):
cd tools/eval-probe
NUGET_PACKAGES=$PWD/.tmp-nuget DOTNET_ROLL_FORWARD=Major dotnet run --project probe.csproj -- <path-to-dxf>

# forward + reverse evaluation sweep over a whole DWG:
cd tools/eval-regression
NUGET_PACKAGES=$PWD/.tmp-nuget DOTNET_ROLL_FORWARD=Major dotnet run --project verify.csproj -- <path-to-dwg>
```

`eval-regression` prints each `BlockLookupAction`'s `CurrentValue` **after the forward pass and after the reverse pass**, plus `=== summary: N dynamic block(s), forward failed X, reverse failed Y ===`. Remember: the forward pass **skips** `flag=4` (lookup/reverse) edges, so a lookup action's `CurrentValue` is produced only by the reverse pass — a pre-evaluation sentinel after the forward pass is **expected, not a bug**.

## 3. Display model (what the viewer draws)

If the question is about how the graph is *drawn* (layout, wires, ports, colors), switch to the `ui-verification` skill (`tools/graph-dump` / `tools/ui-check`).
