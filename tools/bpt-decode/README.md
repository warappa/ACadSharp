# bpt-decode

Research/debug tool for decoding the on-disk body of an AutoCAD
`BlockPropertiesTable` (BPT) object — the "Block Properties Table" behind a
dynamic block's property set. This directory is preserved debug tooling —
**do not delete**.

A BPT's `RawTail` (the payload after the `BeMajor`/`BeMinor`/`Eed1071` header)
is a bit-packed, **string-interning** lookup table. This tool decodes it into
its structure (header, records, string pool, tail) and resolves each record's
string index to its interned string, so the table's content can be read
without a public ObjectARX API. The full bit-exact layout and the reverse-
engineering notes live in [`docs/articles/block-properties-table.md`](../../docs/articles/block-properties-table.md).

## Contents

| File | What it is |
| --- | --- |
| `Program.cs` | The runnable tool. Loads a DWG with `DwgReader`, reaches every `BlockPropertiesTable` (via the document's `_cadObjects` table), decodes its `RawTail` with the library's `BptBodyDecoder`, and prints the body layout + the record → string resolutions. |
| `bpt-decode.csproj` | The tool project. `net10.0`, **strong-name signed** with the library key (`../../src/ACadSharp.snk`) so it can reach ACadSharp internals (the `_cadObjects` field, `BlockPropertiesTable.RawTail`), exactly like the other tools. References `ACadSharp` (for `DwgReader`) and reuses the library's decoder (below). |

The decoder itself is **not** duplicated here — it lives in the library as
[`BptBodyDecoder`](../../src/ACadSharp/Objects/Evaluations/BptBodyDecoder.cs)
(`ACadSharp.Objects.Evaluations`), so the tool and the library share a single
source of truth. `BlockPropertiesTable` (the object) exposes the decoded form
lazily: `Strings`, `RecordIndices`, `StringPoolStart`, `RecordSchemaMatched`.

## How to run

```bash
cd tools/bpt-decode
NUGET_PACKAGES=$PWD/../../.tmp-nuget DOTNET_ROLL_FORWARD=Major dotnet run --project bpt-decode.csproj -c Release -- <path-to-dwg>
```

`DOTNET_ROLL_FORWARD=Major` is required on this machine (only the .NET 10/11
runtimes are installed).

Example (the "1kV Keet" block family — one table that matches the full 1kV
schema):

```
obj1: BPT 31219 bits, decode 235 ms
  header=544 records=3254 pool=27388 tail=33 (schema matched=True)
  126 strings; 26 record indices = [28, 29, 53, 118, 119, 94, 31, …, 49, 56]
    k0 -> [28] Rechts
    k1 -> [29] 1kV Keet - VPR
    …
```

## The decoded structure (verified)

`BptBodyDecoder.Decode` splits the BPT body into, in order:

1. **Header** (544 bits, 1kV family) — a table of 16/32-bit *global* file offsets
   (to other structures such as the `AC1032` marker), **not** a pointer table to
   the BPT's own strings.
2. **Record region** — a set of records; each 1kV record is 126 bits and carries a
   **7-bit string-pool index** (`bit 70 << 6 | 6-bit[97..102]`). The record
   layout is block-family-specific (see "Schema is family-specific" below).
3. **String pool** — the interned strings. Each entry is
   `[ReadBitShort length][length·2 UTF-16LE bytes]`. `ReadBitShort` is the DWG
   2-bit-tag short (`00`→16-bit signed LE, `01`→8-bit, `10`→0, `11`→256).
4. **Tail** (33 bits, 1kV) — a 32-bit value + a flag.

The pool's **start offset is found by search**: the decoder scans candidate start
bits and keeps the one whose `[ReadBitShort][UTF-16LE]` stream consumes the pool
*exactly* with every string printable, preferring the candidate that yields the
most strings (this distinguishes the true start from an off-by-a-bit artifact).

### Schema is family-specific, the pool is not

- The **string-pool encoding** (`[ReadBitShort][UTF-16LE]`) is a **uniform**
  mechanism: it decodes cleanly for the 1kV family (126 strings) *and* the
  L3-02 family (10 / 16 / 21 strings per table).
- The **record schema** (the 126-bit records + 7-bit index) is **1kV-specific**.
  Other families (L3-02, TEE3000) use a different record layout — for TEE3000 the
  strings are stored *inline* (no trailing pool at all). The decoder reports this
  via `RecordSchemaMatched`: when it is `false`, `RecordIndices` is left empty
  (the pool still decodes; the record → index resolution does not).

## What was learned

The reverse-engineering journey (bit-level, cross-validated across the 1kV,
L3-02, and TEE3000 block families) is documented in
[`docs/articles/block-properties-table.md`](../../docs/articles/block-properties-table.md).
The headline findings:

- The BPT body is a **string-interning table**: a set of records, each holding a
  7-bit index into a pool of interned UTF-16LE strings. The 1kV table's strings
  are the block's variant/option names (Dutch electrical, e.g. `Rechts`,
  `Klassiek`, `Dubbelzijdig`).
- The **26th 1kV record is emitted 22 bits short** (104 of 126 bits); the pool
  begins 22 bits later than a naive "26 × 126" boundary would predict.
- The string-pool encoding is **general**; the record schema is **not**.
