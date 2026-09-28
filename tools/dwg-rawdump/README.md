# dwg-rawdump

Research/debug tooling for inspecting the raw object layout of AutoCAD DWG files
(R2004+ object section). This directory is preserved debug tooling — **do not delete**.

## Contents

| File | What it is |
| --- | --- |
| `Decode.cs` / `decode.csproj` | **Standalone** bit-level decoder (no library dependency). Reads a DWG object section's raw bytes and prints the bit-framing of each object. This is the runnable tool. |
| `RawObjectDumpTests.cs` | The dumper, originally a test in `ACadSharp.Tests`. Reference copy only — it references `DwgObjectReader`'s internal members, so it does **not** compile standalone. Kept for documentation. |
| `dump-fresh.log`, `dump-fresh2.log`, `dump-full.log` | Captured decoder output (L3-02 dynamic-blocks DWG). `dump-fresh2.log` is the current, post-Id-fix calibration. |

## How to run the standalone decoder

```bash
cd tools/dwg-rawdump
NUGET_PACKAGES=$PWD/.tmp-nuget DOTNET_ROLL_FORWARD=Major dotnet run --project decode.csproj -- <path-to-dwg>
```

## What was learned (framing + text-region findings)

### Object framing (R2004+)
- Each object in the objects section is: **object type (short) → object data → end handle**.
- `DwgObjectReader.getEntityType(long offset)` (L220-261) re-positions `_objectReader` /
  `_textReader` to the object's offset and reads its class type. It is called once per
  object, **before** the object's data is read.
- **No CRC validation in the read path.** The stored object CRC is not checked when an
  object is read, so a reader may read a *prefix* of the object and stop; the unconsumed
  tail is safely discarded because the next object's `getEntityType` re-positions the
  readers.
- `DwgStreamReaderBase.SetPositionInBits` (L1097) **resets the bit-buffer**. So a read
  method may stop mid-object (after a prefix) and the next read starts cleanly. This is
  what makes the *prefix-read-and-stop* strategy viable without any reader change.

### Bit encodings (`DwgStreamReaderBase.cs`)
- `ReadBitShort` (L459): `00`→2-byte little-endian short (can be negative; −9999 = `0xF601`),
  `01`→1 byte, `10`→0, `11`→256.
- `ReadBitLong` (L499): `00`→4-byte LE, `01`→1 byte, `10`→0, `11`→**throws**.
- `ReadBitDouble` (L546): `00`→8-byte LE, `01`→1.0, `10`→0.0.
- `Read3BitDouble` (L574): an XYZ = three `ReadBitDouble` in a row.

### The text region is separate from the main data region
- Object **strings** (labels, descriptions, proxy names) live in the **separate text
  region**, NOT in the object's main data.
- In the main data, a string field appears as a **tag10 (=0) marker** (empty). The actual
  text is fetched from the text region.
- Consequence: a string field that reads as `0` in the main data is **not** "empty" —
  it is a pointer into the text region. Do not conclude a label is missing from a `0`
  in the main data.

### The 6 dynamic-block classes (item #499, non-graphical)
All six are **item #499 non-graphical** objects in L3-02 (R2018+ / AC21-AC24). Their
main-data tails are **undecodable** with the generic primitives, so the reader uses
**prefix-read-and-stop** (read the known prefix, stop, discard the tail):

| Class | Read prefix | Then |
| --- | --- | --- |
| `BLOCKHORIZONTALCONSTRAINTPARAMETER` | `readBlock2PtParameter` (element + parameter + 2 points + 4 eval-props + 4 grip-ids + base-location) | **stop** (tail undecodable) |
| `BLOCKVERTICALCONSTRAINTPARAMETER` | same as horizontal | **stop** |
| `BLOCKUSERPARAMETER` | `readBlockParameter` (element + parameter + show-props + chain-actions) + first `ReadBitDouble` (the value) | **stop** (63-bit tail undecodable) |
| `BLOCKPROPERTIESTABLE` | `readCommonNonEntityData` (shared header) | **stop** |
| `BLOCKPROPERTIESTABLEGRIP` | `readCommonNonEntityData` | **stop** |
| `ACDB_DYNAMICBLOCKPROXYNODE` | `readCommonNonEntityData` | **stop** (≈83-bit object) |

### Writer design (matches the reader's read-and-stop)
- Constraint params: write the **2-point prefix only** (tail not written — the reader
  reads the prefix and stops).
- User param: write **element + parameter + value** (`WriteBitDouble`).
- The 3 stubs: write **common header + expression** (the reader reads the common header
  and stops; the expression tail is discarded on read — acceptable because these are
  data-only objects whose `Id` is not part of the evaluation graph).
- The 3 stub DXF writers intentionally skip `ProxyData` (byte[], code 309 — no reliable
  string/byte[] code-309 write support relied on); `ProxyName` (300) is written.

### Calibration (L3-02, verified)
- The dumper was misaligned until the trailing **`Id = ReadBitLong()` (code 90)** was
  added to the evaluation-expression read — its absence had shifted every later field.
- After the fix, the linear-parameter calibration matches the parsed values exactly.
- Reference values (L3-02): Door linear 30; Tag polar (1.25, π); North Arrow rotations
  1.571/1.309/1.833; Detail Layout Grid XY (1,1); Toilet alignment 0 / rotation −1.573;
  Arrows rotation 3.142; constraint params 40/80/4/36/12/48/−96.
- `code = −9999` (`0xF601`) is a **legitimate** 2-byte LE short, not an error.
- The 6 classes do **not** fix the 28 unresolved connections (24 `BlockGripLocationComponent.Conn`
  + 4 `BlockArrayAction`) — that is a separate issue.

## Environment notes (this machine)
- .NET SDK 11; only the 10/11 runtimes are installed → `DOTNET_ROLL_FORWARD=Major` is required.
- `~/.nuget` is read-only → `NUGET_PACKAGES=$PWD/.tmp-nuget`.
- `/tmp` is **not** persistent between tool calls → keep artifacts under the repo
  (`.tmp-*` or `tools/`).
