# evalgraph-rawdump

Research/debug tool for bit-level decoding of the **evaluation-graph object classes** in a
DWG file (R2010+ / AC1032 object section). Preserved debug tooling — **do not delete**.

## Why it exists

`dwg-rawdump` (the sibling tool) is hard-coded to a single 189-byte linear-parameter object
and is a narrow calibration aid. This tool is the **general** decoder: it walks *every*
object in the objects section and decodes the six evaluation-graph classes, class by class:

| Class | On-disk layout (object data, after the common header + expression) |
| --- | --- |
| `BLOCKUSERPARAMETER` | element name (text) + v98/v99/v1071 · show/chain bits · **value (double) · value set** |
| `BLOCKHORIZONTALCONSTRAINTPARAMETER` | 2-pt param fields · **label (text) · description (text) · labelOffset (double) · value set** |
| `BLOCKVERTICALCONSTRAINTPARAMETER` | same as horizontal |
| `ACDB_DYNAMICBLOCKPROXYNODE` | essentially just the expression (no extra tail in the samples) |
| `BLOCKPROPERTIESTABLE` | RowCount (long) + an **opaque, bit-packed tail** (rows) |
| `BLOCKPROPERTIESTABLEGRIP` | GripId (long) + an **opaque, bit-packed tail** |

## How to run

```bash
cd tools/evalgraph-rawdump
NUGET_PACKAGES=$PWD/.tmp-nuget DOTNET_ROLL_FORWARD=Major dotnet run --project evalgraph-rawdump.csproj -- <path-to-dwg>
```

The tool is **read-only** — it never modifies the input file.

## What it does

1. Reads the file header, the handle map, and the class table with the library's
   readers (`DwgReader` / `DwgHandleReader` / `DwgClassesReader`).
2. Walks every object offset in the handle map.
3. Re-positions a self-contained, absolute-bit `BitReader` (MSB-first, calibrated against
   `dwg-rawdump`) to each object and decodes it if it is one of the six classes.

## Framing facts (R2010+ object layout)

- Each object: **size (ReadModularShort) → handleSize (ReadModularChar) → class type (object type) → object data → handle stream** (last `handleSize` bits).
- `dataStart` = the bit position right after the class type; `handleStart` = `headerEnd + size*8 − handleSize`.
- **Reader routing** (the key subtlety): `ownHandle`, the EED list, and `hasDsBinaryData`
  come from the **object** reader at `dataStart`; `ownerHandle`, the reactors, and the
  `xdic` come from the **handle** reader. `updateHandleReader` is AC1015–AC1023 only.
- The **string stream is separate**: element names, display names, labels, and descriptions
  are read **in sequence** from the text reader (a `0` in the main data is a pointer into
  the text region, not "empty").
- **Expression** (`AcDbEvalExpr`): `Unknown`/`Value98`/`Value99`/`Id` are `ReadBitLong`;
  the `code` is `ReadBitShort`. A `code` of `−9999` (unsigned `55537`) is ≤ 0, so **no
  typed value** follows — that is not an error.

## The opaque table/grip tail

The `BLOCKPROPERTIESTABLE` (rows) and `BLOCKPROPERTIESTABLEGRIP` tails are **bit-packed and
not byte-aligned** (the field after `RowCount`/`GripId` does not land on a byte boundary),
and do not decode as a run of handle references, bit-longs, or bit-doubles. As of this
writing the row/cell structure is **not fully reverse-engineered** — these two are
data-only objects (their Id is not part of the evaluation graph), so the library reads
their `RowCount`/`GripId` and stops. This tool's `ProbeTail`/`DumpRemaining` emit the raw
bytes so a future pass can pin the structure.

## Notes

- The tool is **strong-name signed with the library key** (`../../src/ACadSharp.snk`) so it
  can reach `ACadSharp` internals (`DwgFileHeader`, `DwgLocalSectionMap`, …) — the same
  mechanism the test project uses (see `src/ACadSharp/AssemblyInfo.cs`).
- `ReadBitLong`/`ReadBitShort`/`ReadBitDouble` mirror `DwgStreamReaderBase` exactly
  (the modular encodings are documented in `dwg-rawdump/README.md`).
