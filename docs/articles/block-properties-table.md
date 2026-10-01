# Block Properties Table (BPT) — format research

> **Status:** the on-disk layout is **decoded and implemented** for the block-element
> prefix (table + grip) and the grip's full field set. The **table's column/row/cell
> body** is a standard R2010+ object whose **inline string region** the library's own
> `SetPositionByFlag` + `ReadVariableText` reads (see the
> [breakthrough section](#breakthrough-the-body-is-a-standard-r2010-object--the-opaque-blob-was-a-misaligned-read)).
>
> **Now confirmed from both sides (this pass):** the complete R2010+ region layout
> (main → string region → 16-bit little-endian size → flag → handle stream) is verified
> against **both** the writer (`WriteSpearShift`) and the reader (`SetPositionByFlag` +
> `applyFlagToPosition`), and a standalone byte-level decoder reads the string region with
> **exact** consumption (898/898, 1104/1104, 2352/2352 bits). See the
> [complete-region-layout section](#the-complete-r2010-region-layout-writer--reader-both-confirmed-and-exact-string-region-consumption).
>
> **Correction (this pass — supersedes the "96-bit record" section below):** the body is
> a **continuous self-describing typed-field stream** (resbuf-style), **not** a fixed
> 96-bit record array. The earlier "96-bit period" (0.848 autocorrelation) and the
> "constant `02 94 00 00 00 00 00` 7-byte record prefix" were **non-byte-aligned windowing
> artifacts**: a corrected 56-bit scan finds **no** `0x0002940000000000` in any table, and
> the 96-bit windows at `191 + 96k` show *shifting* content (pairwise match only 0.812).
> Every field is `2-bit tag + value` (tag `00`/`01`/`10`/`11`); the *width* of a `00`/`01`
> field depends on the field's **type** (BL = 32/8 bit, BS = 16/8 bit, BD = 64/0 bit,
> variant = 16/8/0/64 bit), which only the semantic model knows. The **grip** body is
> **0 bits** (empty) in both `L3-02` and `Block Properties Table.dwg`. See the
> [body-structure section](#the-table-body-a-continuous-typed-field-stream).
>
> **Still open:** pinning the exact field *order* of the table body (the
> `Columns` / `Rows` / `Cells` layout) and wiring the string region into the
> reader/writer. The decoded fields are verified against `L3-02-Dynamic Blocks.dwg`
> (all six objects) and covered by
> [`BlockPropertiesTableTests`](../../src/ACadSharp.Tests/IO/DWG/BlockPropertiesTableTests.cs).
> See [`evaluation-graph.md`](evaluation-graph.md) for the shared object envelope and
> expression.

## What a Block Properties Table is

A BPT is the lookup table behind a dynamic block's property set: rows × columns of values,
where the **first column is the key** (e.g. a visibility-state name) and the remaining
columns hold the values the other parameters must take. Example from the 2013 Autodesk
forum thread (the original motivation for the class):

| user1 | d1  | d2  |
|-------|-----|-----|
| a1    | 100 | 200 |
| a2    | 100 | 400 |
| a3    | 100 | 600 |

Selecting `user1 = a2` must automatically set `d1 = 100, d2 = 400`.

## Where the data lives (verified)

* The BPT is a **node of the evaluation graph** (an `EvalGraph` object) stored in the
  dynamic block record's extension dictionary under the key **`ACAD_ENHANCEDBLOCK`**
  (Autodesk blog, Augusto Goncalves, 25 Feb 2013 — code sample below).
* It is **not in the public ObjectARX API**: `AcDbBlockPropertiesTable` exists in
  `acdbXX.dll` but has no header (confirmed by Augusto Goncalves in the blog comments);
  it was wish-listed. Access is through the .NET internal API
  `Autodesk.AutoCAD.Internal.DatabaseServices.BlockPropertiesTable`.
* A 2020 .NET forum thread confirms the same access path still works.

Blog sample (reduced):

```csharp
DBDictionary extDic = trans.GetObject(blockDef.ExtensionDictionary, ...) as DBDictionary;
EvalGraph graph = trans.GetObject(extDic.GetAt("ACAD_ENHANCEDBLOCK"), ...) as EvalGraph;
int[] nodeIds = graph.GetAllNodes();
foreach (uint nodeId in nodeIds) {
    DBObject node = graph.GetNode(nodeId, OpenMode.ForRead, trans);
    if (!(node is BlockPropertiesTable)) continue;
    BlockPropertiesTable table = node as BlockPropertiesTable;
    int columns = table.Columns.Count;
    foreach (BlockPropertiesTableRow row in table.Rows)
        for (int c = 0; c < columns; c++)
            TypedValue[] cell = row[c].AsArray();   // a variant, like AcDbEvalVariant
    ...
}
```

**Key model facts** (from the sample + the BricsCAD .NET reference):

* `BlockPropertiesTable`: `Columns` (`BlockPropertiesTableColumnCollection`), `Rows`
  (`BlockPropertiesTableRowCollection`), `DefaultActiveRowIndex`,
  `ContainsRuntimeParametersOnly`, `MustMatch`.
* `BlockPropertiesTableColumn`: `Constant`, `CustomProperties` (object id),
  `DefaultValue`, `Editable`, `Format` (string), `Parameter` (the bound dynamic-block
  parameter — column name = `Column.Parameter.Name`), `Removable`, `UnmatchedValue`.
* `BlockPropertiesTableRow`: `Count` (cell count), indexer by column or index →
  **cell content is a variant** (`AsArray()` → `TypedValue[]`) — the same
  `AcDbEvalVariant` type as a dynamic-block property value.

## Semantic reference (BricsCAD .NET, V24 — compatible API)

| Member | Notes |
|---|---|
| `BlockPropertiesTable.Columns` | `BlockPropertiesTableColumnCollection` (AddAt/Move/Remove/ContainsColumn/GetIndex) |
| `BlockPropertiesTable.Rows` | `BlockPropertiesTableRowCollection` (AddAt/Move/Remove/Sort — rows can be **sorted by column**) |
| `BlockPropertiesTable.DefaultActiveRowIndex` | the row matching the default parameter values |
| `BlockPropertiesTable.ContainsRuntimeParametersOnly` | all bound parameters are runtime |
| `BlockPropertiesTable.MustMatch` | the parameter values must match some row |
| `BlockPropertiesTableColumn.Parameter` | the bound parameter (source of the column name) |
| `BlockPropertiesTableColumn.Format` | display format of the column |
| `BlockPropertiesTableColumn.Default/UnmatchedValue` | value used when the row/column does not match |

### The full model (decompiled from the AutoCAD 2021 .NET API, `acdbmgd.dll`)

The table above is the BricsCAD-compatible surface. The **authoritative** model is the
AutoCAD 2021 .NET API, decompiled from `acdbmgd.dll` (`.tmp-decode/autocad/
BPT_column_row.cs`). Each .NET property is a thin P/Invoke wrapper over the native
`AcDbBlockPropertiesTable`; the native method names reveal the **field types** (but not
the on-disk order — the P/Invoke names are *function* names, not field order):

**`BlockPropertiesTable`** (native `AcDbBlockPropertiesTable`):

| .NET property | type | native method |
|---|---|---|
| `IsDisabledInDrawingEditor` | `bool` | `disabledInDrawingEditor` |
| `ContainsRuntimeParametersOnly` | `bool` | `runtimeParametersOnly` |
| `MustMatch` | `bool` | `mustMatch` |
| `DefaultActiveRowIndex` | `int` | `getDefaultActiveRow` / `setDefaultActiveRow` |
| `Columns` | collection | `numberOfColumns`, `parameterInterface`, … |
| `Rows` | collection | `numberOfRows`, `getCellValue`, … |

**`BlockPropertiesTableColumn`** (per column index):

| .NET property | type | native method |
|---|---|---|
| `Parameter` | `IParameter` (handle) | `parameterInterface` |
| `CustomProperties` | `ObjectId` (handle) | `customProperties` |
| `Format` | `string` | `format` |
| `Removable` | `bool` | `removable` |
| `Editable` | `bool` | `editable` |
| `Constant` | `bool` | `constant` |
| `UnmatchedValue` | variant | `unmatchedValue` |
| `DefaultValue` | variant | `defaultValue` |

**`BlockPropertiesTableRow`** (per row index): a collection of `colCount` cell variants
(`getCellValue(row, col)`).

**On-disk interpretation.** The `Parameter` / `CustomProperties` handles are 0 bits in
the main stream (they live in the handle stream). So a column's *main-stream* footprint
is `[Format string][Removable B][Editable B][Constant B][UnmatchedValue variant]
[DefaultValue variant]`, and a row's is `colCount` variants. The **table**'s
main-stream footprint is `[3 B][DefaultActiveRowIndex int][colCount int][colCount ×
column][rowCount int][rowCount × row]`. The exact *order* of these fields — and the int
encoding (BL / BS / raw-32) — is what the brute-force is pinning down: the native method
names give the types, not the order.

## Sources

* Autodesk Developer Blog (Augusto Goncalves, 2013-02-25): "Reading the Block Table of a
  Dynamic Block" — the `ACAD_ENHANCEDBLOCK` dictionary, the .NET sample above, and the
  "not exposed on the ARX SDK headers" confirmation:
  <https://blog.autodesk.io/reading-the-block-table-of-a-dynamic-block/>
* Autodesk ObjectARX forum (2013-03-04, "Solved: how to use a Block Properties Table of a
  Dynamic Block in objectarx2010?") — the example table, the "no native ObjectARX API"
  answer, the `DynamicBlockReferenceProperty` workaround:
  <https://forums.autodesk.com/t5/objectarx-forum/how-to-use-a-block-properties-table-of-a-a-dynamic-block-in/td-p/3795143>
* Autodesk .NET forum (2013, "…using .NET API?") — `ACAD_ENHANCEDBLOCK` storage + the
  "first column is the key" model:
  <https://forums.autodesk.com/t5/net-forum/how-to-use-a-block-properties-table-of-a-a-dynamic-block-using/td-p/3798548>
* Autodesk .NET forum (2020-07-18, "how to modify the Block Properties Table of dynamic
  block?") — access path still current:
  <https://forums.autodesk.com/t5/net/how-to-modify-the-block-properties-table-of-dynamic-block/td-p/9642107>
* GitHub CEXT-Dan/PyRx discussion #461 — "AcDbBlockPropertiesTable is not a class in
  ObjectARX API": <https://github.com/CEXT-Dan/PyRx/discussions/461>
* StackOverflow (2017, "Accessing AutoCad Dynamic Block 'Block Properties Table' through
  C#/COM/Int"): <https://stackoverflow.com/questions/42521575>
* BricsCAD .NET API (V24, compatible reference):
  `BlockPropertiesTable` <https://developer.bricsys.com/bricscad/help/en_US/V24/DevRef/source/html/425113f7-4614-87b0-45f7-00d2b2f001eb.htm> ·
  `Columns` <https://developer.bricsys.com/bricscad/help/en_US/V24/DevRef/source/html/2b8d33dc-691c-93a4-b1ec-85f180c89328.htm> ·
  `Rows` <https://developer.bricsys.com/bricscad/help/en_US/V24/DevRef/source/html/272607e1-6c1d-d5bf-b72d-0247c1b989f5.htm> ·
  `BlockPropertiesTableColumnCollection` <https://developer.bricsys.com/bricscad/help/en_US/V24/DevRef/source/html/eca0f678-7a05-d3db-3a8b-41e2cc41c5ea.htm> ·
  `BlockPropertiesTableRowCollection` <https://developer.bricsys.com/bricscad/help/en_US/V24/DevRef/source/html/2b947895-122c-fa30-7200-9c475a7a527e.htm> ·
  `BlockPropertiesTableColumn` <https://developer.bricsys.com/bricscad/help/en_US/V24/DevRef/source/html/1b4ffa27-d583-9029-9ecf-c3b0506afdca.htm> ·
  `BlockPropertiesTableRow` <https://developer.bricsys.com/bricscad/help/en_US/V24/DevRef/source/html/d36272b1-1a7b-dc12-ad42-c46a07a710e2.htm>

## On-disk layout (decoded from a real R2010+ file)

> Decoded against `L3-02-Dynamic Blocks.dwg` (R2010+) with a standalone bit-level
> decoder (`.tmp-decode/Decode2.cs`). All six evaluation-graph objects in the file
> (3 `BLOCKPROPERTIESTABLE` + 3 `BLOCKPROPERTIESTABLEGRIP`) were decoded consistently,
> which validates the layout.

### Object envelope (R2010+, verified against the library reader/writer)

```
[size: ReadModularShort] [handleSize: ReadModularChar]
[class: 2-bit tag pair + 8-bit class id]      // e.g. 0x48 0x5A → class 0x5A = 90
[object data]                                  // the fields below
[handle stream]                                // handleSize bits
```

`handleStart = headerEnd + size*8 − handleSize` (all in **bits**). The object data =
common header + the class fields; the handle stream holds the owner / reactors /
`XDataDictionary` handles.

### Field widths (writer-verified, `DwgStreamWriterBase` L177–238)

| type | tag 00 | tag 01 | tag 10 | tag 11 |
|---|---|---|---|---|
| **BL** (bit-long) | 34 (2+32) | 10 (2+8) | 2 | — |
| **BS** (bit-short) | 18 (2+16) | 10 (2+8) | 2 | 2 (=256) |
| **BD** (bit-double) | 66 (2+64) | 2 (=1.0) | 2 (=0.0) | — |
| **B** (bit) | 1 | | | |

A wrong assumption in an earlier pass (32/8/1-bit widths) produced spurious "exact-fit"
parses — the widths above are the ground truth, taken from the library's own writer.

### The expression (82 bits — fully verified, all 6 objects)

`Dwg_EvalExpr` (LibreDWG `include/dwg.h` L6997–7014) is the first object field and is
present in both the table and the grip:

```
parentid  BL    = -1      (tag 00, 34 bits)
major     BL    = 33      (tag 01, 10 bits)
minor     BL    = 175     (tag 01, 10 bits)
value_code BS    = 55537   (tag 00, 18 bits; 0xD801 — a "no value" sentinel)
nodeid    BL    = <handle id> (tag 01, 10 bits)
```

Total `34 + 10 + 10 + 18 + 10 = 82` bits. `value_code = 0xD801` means the inline value
union is **not written** (a sentinel); `nodeid` is the handle id of the node this
expression points at. This matches the semantic model: a BPT node is itself part of
the `EvalGraph`, and its expression links into the graph.

### The grip (`BLOCKPROPERTIESTABLEGRIP`) — mostly decoded

After the expression, the grip is `BLOCKELEMENT` fields + grip-specific fields
(LibreDWG `dwg.h` L8119–8151):

```
name          T       (string → text region; 0 main-stream bits for the data,
                        but a ReadBitShort length *is* in the main stream)
be_major      BL      = 33
be_minor      BL      = 175
eed1071       BL      = 0
bg_bl91       BL      = 11
bg_bl92       BL      = 12
bg_location   3BD     = (x, y, z)
bg_insert_cycling   B
bg_insert_cycling_weight BLd
```

All of the above decode to sensible, consistent values across the three grips.
**Open:** a constant **91-bit gap** remains after `bg_insert_cycling_weight` on all
three grips (tails 302/302/174 bits). The 3D location *varies* (134 vs 6 bits) while
the gap stays 91, so the gap is a separate fixed-size region. A fresh pass shows it is
the object's **final region**: a constant field (identical across the three grips)
followed by the 17-bit **text-region metadata** (a flag = 1 + a 16-bit pointer into the
separate string stream) — see the
[dedicated section below](#the-91-bit-grip-gap--now-understood-as-the-text-region-metadata--a-constant-field).

### The table (`BLOCKPROPERTIESTABLE`) — not yet decoded

The table's tail (2602/6539/3532 bits) begins with the same `be_major=33`,
`be_minor=175`, `eed1071=0` triple as the grip, then is **not yet decoded**.
Like the grip, the table's tail *ends* with the same 17-bit **text-region metadata**
(flag + 16-bit string-stream pointer) — see the [dedicated section below](#the-91-bit-grip-gap--now-understood-as-the-text-region-metadata--a-constant-field) — so a table's
`name` is also in the separate string stream, not in the object bytes.

> *Correction (this session):* an earlier pass reported "real doubles ≈ 3.0 / 6.0 /
> 12.0 spaced 64 bytes apart" in the table tail. **Retracted** — those are
> misalignment artifacts. The tail start is *not* byte-aligned (it follows the
> 82-bit expression, which ends mid-byte), so byte-aligned "double" sightings in the
> raw are denormals (≈ 1e-314) and carry no meaning.

LibreDWG's `Dwg_Object_BLOCKPROPERTIESTABLE` is **empty** (`"??"`), so the layout is
data-driven and must be read off the bitstream: a column-count / row-count header,
then per-column (parameter handle + format string + default/unmatched variants) and
per-row (cells of variants), then `DefaultActiveRowIndex` (BS), `MustMatch` (B),
`ContainsRuntimeParametersOnly` (B).

### The 91-bit grip gap — now understood as the text-region metadata + a constant field

A fresh data-driven pass (diffing the three grips' tails bit-by-bit) resolved most of
this. The gap is the **last region of the object data, immediately before the handle
stream**, and it has two parts:

1. **A constant field** (the first 74 bits of the gap; ≈ 90 bits if measured from the
   library reader's slightly earlier end) — **identical across all three grips** (the
   three tails differ only in the final ~12 bits, which fall inside the pointer below).
   Its exact bits (grip #1) are
   `0100000100010001110000000001110010000000000110100100000000011100000000000001001010000000001`
   truncated to 74 bits; a greedy `BL`/`BS` decode of it yields arbitrary values, so its
   field type is still not pinned down.
2. **The text-region metadata** (the final 17 bits): a 1-bit **flag** (bit
   `handleStart − 1`, = 1 for all three grips) and a 16-bit **value** at
   `handleStart − 17`.

> **Correction (previous pass misread this value):** the 16-bit value is **not** a
> pointer into a separate string stream. Read the way the library reads it (a
> little-endian `UShort` from the bit stream, i.e. the **byte-swapped** form of the
> MSB-first bits) it is a small **size** — **74 bits for all three grips** (the MSB-first
> bits `0x4A00` byte-swap to `0x004A` = 74). `SetPositionByFlag` uses it as
> `stringStart = handleStart − 17 − size`, which lands **inside the object's own data
> region**, so the string data (the grip's `name`, etc.) is **inline**, immediately
> before the 17-bit metadata. The "tight `0xE0x` cluster" the previous pass saw in a
> naive MSB-first read was the byte-swapped form of these small size values. The same
> 17-bit tail (flag = 1 + a 16-bit size) appears on the *tables* too — see the
> [breakthrough section](#breakthrough-the-body-is-a-standard-r2010-object--the-opaque-blob-was-a-misaligned-read)
> for the decoded values (898 / 1104 for the tables) and the strings the library's own
> `ReadVariableText` extracts (`"Block Table"` / `"Block Table1"` for the tables,
> `"Grip"` + more for the grips).

> *Remaining:* the exact field type(s) of the ~74-bit part of the gap that precedes the
> 17-bit metadata are still not pinned down, and the semantic role of the extracted
> strings (table name vs. column `Format`/`Parameter` vs. row/cell data) is open.

### The 17-bit tail, in the library's own R2010+ terms

The "text-region metadata" above is the object's **R2010+ text/secondary-data region**,
exactly as the library's reader handles it (`DwgObjectReader` L259–263 →
`DwgStreamReaderBase.SetPositionByFlag` L374–402 + `applyFlagToPosition` L1139–1170).
For an R2010+ object the reader builds a **merged reader** (object + text + handles)
and positions the *text* reader with:

```
position = handleStart − 1            // the last bit of the object data
flag     = bit at position           // "string stream present"
if flag:
    size     = UShort at (position − 16)      // 16-bit, at handleStart − 17
    if size & 0x8000:
        hiSize   = UShort at (position − 32)  // 16-bit, at handleStart − 33
        size     = (size & 0x7FFF) | (hiSize << 15)   // 30-bit
    stringData = at (position − 16 − size)    // `size` BITS, right before the size field
```

(Note the unit is **bits**, not bytes — e.g. a `size` of 898 is 112.25 bytes, so the
string region is bit-aligned with the rest of the object data, not byte-aligned.)

So the object data layout is
`[real fields] [string data] [hiSize (if 0x8000)] [size: 16b] [flag: 1b]`, and the
`flag` is the *last* bit of the object data.

> **Correction (previous pass misread the flags and the value):** the `flag` is
> **1 for all six objects** — every BPT object has a string region (the earlier
> "T2=0, T3=0, G1=0" was a misaligned read). And the 16-bit `size` is **not** a
> pointer into a separate string stream: read as the library's little-endian `UShort`
> it is a small value (898 / 74 / 1104 / 74 / 74, none with the `0x8000` extended bit
> set), and `stringStart = handleStart − 17 − size` lands **inside the object's own
> data region**, immediately before the 17-bit metadata. The string data is therefore
> **inline**, and the "tight `0xE0x` cluster" seen in a naive MSB-first read was the
> **byte-swapped** form of these small size values — not a pointer. See the
> [breakthrough section](#breakthrough-the-body-is-a-standard-r2010-object--the-opaque-blob-was-a-misaligned-read)
> for the decoded values and the strings the library extracts.

### Breakthrough: the body is a standard R2010+ object — the "opaque blob" was a misaligned read

> **Correction (supersedes the "opaque blob" claim in the previous pass):** the table
> body is **not** a compression / delta / secondary-data scheme. It is a **standard
> R2010+ object data region**, and the library's **own** text-region machinery reads it.
> The `0xE0` / `0xCC` "opaque" bytes seen in an earlier raw dump were a **misaligned
> read** of the inline UTF-16 **string data** (see below), not an unknown encoding.

**Ground truth, taken from the library itself** (`.tmp-decode/Decode2.cs` `strings`
mode, which builds the library's own `DwgStreamReaderBase` via `GetStreamHandler`,
calls `SetPositionByFlag(handleStart − 1)` exactly as `DwgObjectReader` L261 does, and
pumps the library's own `ReadVariableText`):

| object | `SetPositionByFlag` → | strings extracted by `ReadVariableText` |
|---|---|---|
| TABLE #1 | 5873241 | `"Block Table"` (11), `"Block Table1"` (12) |
| TABLE #3 | 6033772 | `"Block Table"` (11), `"Block Table1"` (12) |
| TABLE #6 | 6539967 | `"Block Table"` (11), `"Block Table1"` (12) |
| GRIP #2 | 5874613 | `"Grip"` (4), + a 40-char and a 256-char string |
| GRIP #4 | 6035381 | `"Grip"` (4), + 6 more strings (one 3090-char, embedding `"End Grip"`, `"UpdatedBaseX"`, `"XScale"`) |
| GRIP #5 | 6071429 | `"Grip"` (4), + a 40-char and a 256-char string |

So the body is a standard R2010+ object whose **inline string region** the library
already knows how to read. The three tables carry the same two short strings
(`"Block Table"` / `"Block Table1"`); the grips carry the grip name (`"Grip"`) plus
several larger strings that belong to the surrounding dynamic-block parameter data.

**The 16-bit value, decoded.** The `flag` (the last bit of the object data) is **1 for
all six objects** — every BPT object has a string region. The 16-bit value at
`handleStart − 17` is read by the library as a **little-endian `UShort` from the bit
stream**, i.e. the **byte-swapped** form of the natural MSB-first 16-bit read. The
decoded values are:

| object | MSB-first 16 bits | library `ReadUShort` (= the real value) |
|---|---|---|
| TABLE #1 | `0x8203` | **898** |
| GRIP #2  | `0x4A00` | **74** |
| TABLE #3 | `0x5004` | **1104** |
| GRIP #4  | `0x4A00` | **74** |
| GRIP #5  | `0x4A00` | **74** |

None have the `0x8000` bit set, so the **extended (30-bit) `hiSize` path is never taken**
for these objects; the value is a plain 16-bit count. `SetPositionByFlag` uses it as
`stringStart = handleStart − 17 − value`, which is why the "pointer to a separate
string stream" interpretation from the previous pass was wrong: the string data is
**inline**, immediately before the 17-bit metadata, and the value is what locates it.

**Open:** the exact *semantic* mapping of the extracted strings (are
`"Block Table"` / `"Block Table1"` the table name, a column `Format`, or a column
`Parameter` name? do the grips' larger strings hold the table's row/cell data, or are
they unrelated dynamic-block strings?). The string region is now readable end-to-end;
mapping it to the `Columns` / `Rows` / `Cells` model is the remaining step.

### The complete R2010+ region layout (writer + reader, both-confirmed) and exact string-region consumption

The previous section confirmed the string region via the library's **own** readers
(`SetPositionByFlag` + `ReadVariableText`). This pass goes further: it confirms the
**complete object-data region layout from the writer side** (`DwgMergedStreamWriter.
WriteSpearShift` L240–289) and decodes the string region with a **standalone
byte-level decoder** (`.tmp-decode/guided/Guided.cs`) that reads the object directly
from the raw object section — **no library readers involved** — and verifies the
string region is *exactly* consumed.

**The region layout (R2010+, both-confirmed).** An R2010+ object's data is three
sub-regions plus the handle stream, assembled in this order by `WriteSpearShift`:

    [main stream]                            // every B/BL/BS/BD field; handles & strings are 0-bit placeholders
    [string region: S bits]                 // only if S > 0
    [size field: 16 / 32 / 48 bits]         // only if S > 0
    [flag: 1 bit]                           // = 1 iff a string region is present
    [handle stream: handleSize bits]

`WriteSpearShift` writes exactly this: the main stream first; if the text writer has
any content (`textSizeBits > 0`), the string region is spliced in, then the
16-bit / 32 / 48-bit size field via `SetPositionByFlag`, then `WriteBit(true)`;
otherwise just `WriteBit(false)`; then the handle stream. The reader
(`DwgObjectReader` L259–263 → `SetPositionByFlag` L374–402 + `applyFlagToPosition`
L1139–1170) reads it back:

    handleStart = headerEnd + size*8 − handleSize        // all in bits
    flag        = bit at (handleStart − 1)              // last bit of the object data
    if flag:
        S = UShort at (handleStart − 17)               // 16-bit, LITTLE-ENDIAN
        if S & 0x8000:
            hi = UShort at (handleStart − 33)         // 16-bit high part
            S = (S & 0x7FFF) + (hi << 15)
        string region = S bits at (handleStart − 17 − S)   // ends right before the size field
    else:
        (no string region; the main stream ends at handleStart − 1)

Two details this pass nailed (the previous pass had the *reader* side but not the
*writer* side, and was off-by-one on the string-region end):

* **The 16-bit size field is little-endian** (raw bytes, low byte first), **not** the
  natural MSB-first bit read. `SetPositionByFlag` writes it via `WriteBytes(
  LittleEndianConverter …)` (L623–641); the reader's `ReadUShort` reads it back as a
  LE `UShort`. Reading it MSB-first gave impossible values (33283 / 20484 / 1241580035);
  reading it LE gives the small, sensible sizes **898 / 1104 / 2352**.
* **The string region ends at `handleStart − 17`** (immediately before the 16-bit
  size field), so its start is `handleStart − 17 − S`. The flag is 1 bit at
  `handleStart − 1`; the size field is 16 bits at `handleStart − 17`. (An earlier
  draft used `handleStart − 16 − S` — off by the flag bit, which made the decoded
  strings drift.)

**Exact string-region consumption (the strongest validation).** The standalone decoder
reads each object straight from the raw object section (modular `size` / `handleSize`
header → 2-bit-pair object type → main-stream prefix: common data + expression +
`be_*`/`eed1071`), computes `handleStart` / `S` / the string region from the layout
above, and decodes the string region as a run of `WriteVariableText` fields
(`BitShort` char count + char-count × 2 UTF-16 bits). The string region is
**exactly** consumed in all three `L3-02` tables:

| object   | S (bits) | strings extracted                                                                                          | consumption |
|----------|----------|------------------------------------------------------------------------------------------------------------|-------------|
| TABLE #1 | 898      | `"Block Table"`, `"Block Table1"`, `""`, `""`, `"UpdatedDistance"`, `""`, `""`, `"UpdatedDistance"`, `""`   | **898/898** |
| TABLE #3 | 1104     | `"Block Table"`, `"Block Table1"`, `""`, `""`, `"UserVariable"`, `""`, `""`, `"UpdatedDistance"`, `""`, `""`, `"UpdatedDistance"`, `""` | **1104/1104** |
| TABLE #6 | 2352     | `"Block Table"`, `"Block Table1"`, `"UserVariable"`, `"Custom"`, `"UpdatedDistanceX"`, `"1 Space"` … `"10 Spaces"` (20 strings) | **2352/2352** |

A byte-level decoder that lands **exactly** on the string-region boundary (no over-
or under-read) is the strongest evidence the region layout is correct: the
class-specific body is the bits *before* the string region, and the 17-bit tail
(`[16-bit size][flag]`) sits between the string region and the handle stream.

**The class-specific body, sized.** Subtracting the string region, the 17-bit tail,
and the common-header handles from each table's `RawTail` (2596 / 6533 / 3536 bits)
gives the class-specific body:

| object   | RawTail | − string region | − 17-bit tail | − 16-bit common handles | = class-specific body |
|----------|---------|-----------------|---------------|--------------------------|------------------------|
| TABLE #1 | 2596    | 898             | 17            | 16                       | **1665** |
| TABLE #3 | 6533    | 1104            | 17            | 16                       | **5396** |
| TABLE #6 | 3536    | 2352            | 17            | 16                       | **1151** |

The common-header handles (the object's own handle + owner + N reactors + the
`XDataDictionary` handle, read by the handles reader) are a constant **16 bits** for
all three tables — the same count the writer's `handleSize` field reports. The
class-specific body (1665 / 5396 / 1151 bits) is the region between the
`be_*`/`eed1071` prefix and the string region, and holds the `Columns` / `Rows` /
`Cells` — the **remaining work** (see the section above and
[Status & next steps](#status--next-steps)).

 ### The table body: a continuous typed-field stream

**This pass re-examined the body bit-by-bit and corrected the "96-bit record"
interpretation below.** The body is a **continuous self-describing typed-field stream**
(the same resbuf-style `2-bit tag + value` encoding used by the expression and the
block-element prefix), **not** a fixed 96-bit record array.

**Evidence (`.tmp-decode` `vstream` mode, which decodes the body as a self-describing
variant stream — `tag 00` = 16-bit, `01` = 8-bit, `10` = 0, `11` = 64-bit — and the
`recs` mode's 96-bit windows):**

* **The `02 94` "prefix" is gone.** A corrected 56-bit scan for `0x0002940000000000`
  finds **no** match in any of the five tables (3 from `L3-02`, 2 from `Block
  Properties Table.dwg`). The earlier "constant 7-byte record prefix" was a
  **non-byte-aligned windowing artifact** (the 96-bit windows do not land on record
  boundaries).
* **The 96-bit "period" was an autocorrelation artifact.** The `recs` mode's 96-bit
  windows at `191 + 96k` show *shifting* content (pairwise match only 0.812, against a
  0.917 "same-record" expectation) — the content moves as the window slides, which is
  the signature of a **stream of variable-width fields**, not fixed 96-bit records.
* **The fields are self-describing.** Decoding the body as a variant stream yields a
  clean, consistent sequence of values (integers, a `-9999` / `0xF601` "not found"
  sentinel, `0.0` doubles) that runs all the way to the string region with no
  misalignment — e.g. `L3-02` TABLE #1: `33, 175, 0, 0, 0, …, 10, 2, 2, 0, -1, 5,
  -9999, -9999, 145, 0.0, …`.

**The full object layout (verified, all six objects):**

```
[size] [handleSize] [class]
[common header]            ownHandle + ext-data + reactors + missing + xdic + hasDs
[expression]               parentid(BL) + major(BL) + minor(BL) + code(BS) + nodeid(BL)  = 82 bits
[block-element prefix]     be_major(BL) + be_minor(BL) + eed1071(BL)   (3 × ReadBitLong)
[body]                     the column/row/cell data — a continuous typed-field stream
[string region]            [16-bit size][1-bit flag=1]  (inline UTF-16, byte-swapped LE)
[handle stream]
```

The **body** (between the block-element prefix and the string region) is where the
`Columns` / `Rows` / `Cells` live. It is a **run of `2-bit tag + value` fields**; the
*width* of a `tag 00` / `tag 01` field depends on the field's **type** (which only the
semantic model / writer knows):

| type   | tag 00 | tag 01 | tag 10 | tag 11 |
|--------|--------|--------|--------|--------|
| **BL** | 34 (2+32) | 10 (2+8) | 2 | — |
| **BS** | 18 (2+16) | 10 (2+8) | 2 | 2 (=256) |
| **BD** | 66 (2+64) | 2 (=1.0) | 2 (=0.0) | — |
| variant| 18 (2+16) | 10 (2+8) | 2 (=0) | 66 (2+64) |

> **The tag ambiguity (why blind decoding is not enough):** the 2-bit tag alone does
> **not** determine the width. A `tag 00` could be 16/32/64 bits (BS/BL/BD), and a
> `tag 01` could be 8 bits or 0 bits (BD = 1.0). The width is set by the field's
> **type**, which only the semantic model / writer knows. So the body must be decoded
> **guided by the field types** (the `Columns` / `Rows` / `Cells` layout), not blind.
> The variant-stream decode above is a *hypothesis* (it assumes every field is a
> variant); it is a useful probe but not the final layout.

**Cross-file body sizes** (`.tmp-decode` `body` mode; the grip body is **0 bits** in
both files — a grip is a single point, not a table):

| file | table | body (bits) |
|---|---|---|
| `L3-02-Dynamic Blocks.dwg` | TABLE #1 | 1665 |
| `L3-02-Dynamic Blocks.dwg` | TABLE #3 | 5396 |
| `L3-02-Dynamic Blocks.dwg` | TABLE #6 | 1151 |
| `Block Properties Table.dwg` | TABLE #1 | 773 |
| `Block Properties Table.dwg` | TABLE #3 | 773 |

**The `Block Properties Table.dwg` pair (two 773-bit tables, identical shape)** is the
key to pinning the layout. A **bit-by-bit diff** of the two bodies (`.tmp-decode/bodies.txt`)
gives a decisive result: the two tables differ in **only 28 bits, all within the first
~69 bits** (positions 0–68); the **remaining 704 bits are byte-identical**. So the body
is **~96% constant** — a small leading region carries the *varying* data (the numeric
cell values / `DefaultActiveRowIndex`), and the bulk of the body is *constant*
(structure + the string references that point into the separate string region). The
differing bit positions are
`2,5,7,8,14,20,23,24,26,27,29–33,37,40,42,43,44,46,47,54,56,57,58,63,68`; the
`"10"` (tag-10) 2-bit units and the interleaved constant runs show the leading region
is a fine-grained mix of a few short constant fields and the varying data, while the
704-bit constant tail is the uniform per-string / per-variant reference block.

**Next step (not yet done):** decode the body **guided by the semantic-model field
types** (`MustMatch` B, `ContainsRuntimeParametersOnly` B, `DefaultActiveRowIndex` BL,
`ColumnCount` BL, `ColumnCount × [Parameter handle + name + type + Constant/Editable/
Removable B + DefaultValue/UnmatchedValue variants]`, `RowCount` BL,
`RowCount × ColumnCount × variant`) and confirm which width choices produce a
consistent parse across the five tables.

**What the constant-vs-varying diff already tells us:** the two tables in
`Block Properties Table.dwg` are **two separate `BlockPropertiesTable` objects** (not
two copies of one body) that share one column schema (47 strings) but carry permuted
cell data. Their 704-bit constant tail has two parts: (a) a complex leading region
(≈320 bits) and (b) a **regular repeating pattern** (`1010 0000 0010 1000 0000 1010
0000 0010 …`, ≈384 bits) — the per-cell / per-variant reference block. Crucially, that
pattern does **not** parse as standard BL/BS/BD long-form fields (`tag 00` would be
18/34/66 bits, but the run is far tighter), which means the **class-specific body uses
a different field-width scheme than the common object header** — the width is set by
the field's *type* (per the [tag-ambiguity note](#the-field-widths)), so the body must
be decoded guided by the semantic-model field types, not by blind tag-length
assumptions. The 28 varying bits (the header / `DefaultActiveRowIndex` / a few numeric
cells) are the *only* per-instance data in the body; the cell *values* themselves live
in the separate string region.

**Refinement — cross-schema verification: the "constant" is schema-specific, not a
universal AutoCAD constant.** To check whether the ~96%-constant body is a universal
encoding feature or a property of one schema, I ran the decoder over the *other*
dynamic-block samples in the repo. The 10 `samples/dynamic-blocks/*.dwg` files (the
classic single-parameter blocks) contain **no `BlockPropertiesTable` objects** at all
(they are simple parameter blocks with no lookup table). The `L3-02-Dynamic Blocks.dwg`
file, however, holds **three** `BlockPropertiesTable` objects with **three different
schemas** (body sizes 1665 / 5396 / 1151 bits). A five-body comparison (the two 773-bit
tables + the three L3-02 tables) gives a decisive refinement:

| group | schema | body (bits) | relationship |
|---|---|---|---|
| `Block Properties Table.dwg` #1, #3 | *one* schema (47 strings) | 773, 773 | **identical** except 28 bits in the first 69 (positions 2–68) |
| `L3-02-Dynamic Blocks.dwg` #1, #3, #6 | *three* different schemas | 1665, 5396, 1151 | **all different** |

Three concrete results:

1. **The 773 pair's repeating tail is schema-specific.** The regular `1010 0000 0010
   1000 0000 1010 …` pattern in the 773-bit tail appears **0 times** in any of the
   three L3-02 bodies. Even a 24-bit slice from the 773 body's mid region
   (`0001 0010 0011 1111 1111 1111 1111 1111`) appears 0 times in the L3-02 bodies.
   The "constant" pattern is not a universal encoding constant — it is the
   **variant-code stream for that specific schema**.
2. **The *shape* is universal; the *bits* are not.** All five bodies share the same
   macro shape — a dense leading region (≈ the first 200–400 bits; 46–62 ones per
   100-bit chunk) followed by a sparser tail (≈ 7–22 ones per 100-bit chunk) — but
   the exact bits differ between schemas. So the body's *structure* (dense header +
   sparse per-cell reference block) is universal, while its *content* is
   schema-specific.
3. **Within one schema the body is ~96% constant; across schemas it is not.** The two
   773-bit tables (same schema, permuted cell data) differ in only 28 bits, all in
   the leading region. The three L3-02 tables (different schemas) differ substantially
   (last-300-bit agreement: 266 and 235 of 300).

**Implication for the decode.** The "constant" is a property of the *schema* (the
table's column / row / variant structure), not a universal AutoCAD constant. This
means:

- I **cannot** treat the 773-bit tail as a template for other tables' bodies — each
  schema has its own variant-code stream.
- The body must be decoded **guided by the semantic-model field types** (per schema):
  `MustMatch` B, `ContainsRuntimeParametersOnly` B, `DefaultActiveRowIndex` int,
  `ColumnCount` int, `ColumnCount × [Parameter handle (0 b) + Format string (0 b) +
  CustomProperties handle (0 b) + Removable B + Editable B + Constant B +
  UnmatchedValue variant + DefaultValue variant]`, `RowCount` int,
  `RowCount × Row (colCount cell variants)`. The body size is set by `ColumnCount`,
  `RowCount`, and the variant widths.
- The 773-bit pair remains a valid **same-schema** cross-check (two objects, 28-bit
  diff), but it is *not* a universal template.

The 28 varying bits (positions 2–68) are the *only* per-instance data in a same-schema
body; the cell *values* themselves live in the separate string region / the evaluation
graph.

**Schema of the 773-bit pair (from its 47-string region).** The 47 strings in the pair's
string region break down into three groups that pin the column / row counts:

| group | count | content |
|---|---|---|
| leading named refs | 2 | `"Block Table"`, `"Block Table1"` |
| empty (column-level) | 13 | 13 × `""` (positions 3–15) |
| data (cell-level) | 32 | 8 rows × 4 columns (positions 16–47) |

The 32 data strings form a clean **8 × 4 grid**:

| row | col 0 (number) | col 1 (name) | col 2 (plan) | col 3 (unit) |
|---|---|---|---|---|
| 0 | `6000` | Sarah | PER PLAN | UNIT A |
| 1 | `6001` | Zac | REVERSE | UNIT A |
| 2 | `6002` | Volker | PER PLAN | UNIT B |
| 3 | `6003` | Victoria | REVERSE | UNIT B |
| 4 | `6004` | Nauman | PER PLAN | UNIT C |
| 5 | `6005` | Mike | REVERSE | UNIT C |
| 6 | `6006` | Ashley | PER PLAN | UNIT D |
| 7 | `6007` | Dave | REVERSE | UNIT D |

So the schema is **`colCount = 4`, `rowCount = 8`**. The two tables in the pair have
the *same* schema but **permuted** cell values (e.g. TABLE 1 has `PER PLAN`, `UNIT A`
at positions 18–19; TABLE 2 has `UNIT A`, `PER PLAN`), which is why their bodies are
~96% identical (same variant-code stream) while their string regions differ (permuted
values). The 13 empty strings are the column-level string values (4 columns × 3
string-capable fields — `Format`, `UnmatchedValue`, `DefaultValue` — = 12, plus 1 extra),
and the 2 leading named refs are a puzzle (they precede the column data; the semantic
model has no "block name" field, so they may come from a parent class).

**String-reference mechanism (key insight).** The string region is a *sequence* of
strings (`[BS charCount][charCount × 2 bytes UTF-16]` × 47), consumed in order. A
string field in the body is **0 bits** in the main stream (the writer's
`WriteVariableText` writes only to the `TextWriter`, not the `Main` writer); the
*value* is pulled from the string region in sequence order. So the order of the strings
in the region **equals** the order of the string-capable fields in the body. The
main stream carries the *type tags* (which mark a variant as a string vs a number) and
the *non-string values*; the string region carries the *string values* in order.

**The `BlockPropertiesTable` class hierarchy (decompiled from AutoCAD 2021).** The
Table class **inherits from `DBObject`** (not from a block or a property group), and
its own fields are exactly: `IsDisabledInDrawingEditor` (B),
`ContainsRuntimeParametersOnly` (B), `MustMatch` (B), `DefaultActiveRowIndex` (int),
`Columns` (a `BlockPropertiesTableColumnCollection`), and `Rows` (a
`BlockPropertiesTableRowCollection`). It also has an `AuditError` / `tableAudit`
facility (a collection of per-cell `AuditError` records, each with `RowIndices`,
`ColumnIndex`, `RowIndex`, `Type`), but that is a query API, not on-disk state. So the
**2 leading named strings** (`"Block Table"`, `"Block Table1"`) are *not* Table-class
fields — they precede the column data in the string region but the Table class has no
"name" field. The likely explanation: the 2 named strings come from the `DBObject`
base (the object's name / handle text) or a parent-block reference, and are *not* part
of the class-specific body. This means the body's own string-capable fields are the
**13 empty + 32 data = 45 strings** (not 47), which tightens the column / cell
constraint (4 columns × 3 string-capable fields = 12, + 1 extra column-level + 32
cells = 45).

**Cross-version note (R2000 / R2004 / R2025 files have no BPT).** Four user-supplied
files were tested: `car.dwg` (R2000), `window_4.dwg` (R2000), `DYN--TEE--3000--1.dwg`
(R2004), and `1kVKeetKOPIE.dwg` (R2025 / AC1032). The two R2000 files contain the
*old* dynamic-block mechanism (`AcDbBlockLinearParameter`, `AcDbBlockXYGrip`,
`AcDbBlockScaleAction`, `AcDbBlockFlipAction`, `AcDbDynamicBlockGUID`,
`AcDbDs::IndexedPropertySchema`, …) but **no `BlockPropertiesTable` class at all**; the
R2004 and R2025 files are minimal (2 class entries each, no dynamic blocks). So none
of the four can cross-check the BPT body finding — the BPT object only exists in
R2010+ files that have a dynamic block *with a lookup / visible property table* (the
`L3-02` and `Block Properties Table.dwg` samples).

**3rd independent schema confirmed on R2025 (AC1032) — the region layout holds.**
A user-supplied R2025 file (`1kVKeetKOPIE.dwg`, AC1032) contains a
`BlockPropertiesTable` object with a **3800-bit body** and a string region of
**S = 27386 bits** (a distinct schema from the 773-bit pair and the three L3-02
schemas). The decoder (which reads the R2010+ object layout) confirms the region layout on
this third schema: `prefixBits = 134` ✓, `flag = 1` ✓ (a string region is present),
and `sizeField = 16b` ✓. The body's density profile is `[19,46,50,62,54, 18,18,15,14,13,16,21,16,12,14,14,17,11,12,13,16,18,12,10,15,17,18,11,11,15,18,17,13,11,17,17,14,14]`
(ones per 100-bit chunk) — a **dense leading region** (chunks 0–4) followed by a
**sparser tail** (chunks 5–37), the same macro shape as the 773-bit pair
`[32,62,58,46,20,21,22,17]`. A cross-schema pattern match confirms the "constant" is
schema-specific: 16-bit slices of the 773-bit tail appear in the 3800-bit body 304/385
times (a similar repeating structure), but 48-bit slices appear **0/353** times, and the
3800-bit leading 48 bits appear in the 773-bit pair **0 times** — so the 3800-bit body
is a genuinely independent schema, not a re-use of the 773-bit pattern. This is the
**third independent confirmation** of the R2010+ object region layout
(134-bit prefix + 16-bit LE size field + 1-bit flag + body + string region).

**The string region is consumed *exactly* — it holds 125 strings, not 50 (anomaly resolved).**
An earlier pass read the R2025 string region with a 50-string cap and concluded it
"only" consumed 7452 / 27386 bits (a ~19934-bit gap), which looked like a size-field
mis-read. Re-reading the region with **no cap** (`.tmp-decode/guided/StringsAll.cs`,
assembly name `decode` for `InternalsVisibleTo`) shows the region is consumed
**exactly**: **125 strings, 27386 / 27386 bits = 100.0 %, 0 bits remaining**. So
`S = 27386` is correct (the 16-bit LE size field is not a mis-read) and the earlier
"anomaly" was simply the 50-string stop condition. The R2025 schema therefore holds
**125 strings** — far more than the 47 of the 773-bit pair.

The 125 strings break down as (index = position in the string region):
- `[0..1]` — 2 leading named refs: `"Block Table"`, `"Block Table1"`.
- `[2..3]` — 2 empty strings.
- `[4..15]` — 3 × (`"UserVariable"`, `"Custom"`, `""`, `""`) = 12 strings
  (3 groups of 4; the `UserVariable` / `Custom` names look like column /
  property descriptors).
- `[16]` — `"UpdatedDistance"`.
- `[17..18]` — 2 empty strings.
- `[19]` — `"VisibilityState"`.
- `[20]` — 1 empty string.
- `[21..124]` — **104 data strings** (the cell values).

The 104 data strings are the cell values of the table (e.g. `"1kV Keet"`,
`"Klassiek"`, `"Dubbelzijdig"`, `"Rechts"`, `"Links"`, `"1kV Keet - VPR"`,
`"1kV Keet - VPL"`, `"Uitbreiding - 1kV keet"`, `"Aanduiding omvormers"`,
`"Smeltveiligheden"`, `"BIC/LIC/SEC - Gemotoriseerd"`, `"IL Shelter - VPR"`,
`"IL Shelter - Double Wide - VPL"`, …). `104 = 8 × 13 = 4 × 26`, so the R2025 table
is a larger grid than the 773-bit pair's 8×4 (32 cells) — consistent with the
3800-bit body being ~5× the 773-bit body. The exact row / column split is still
open (see the body-width search below), but the **string count (125) and the exact
region consumption (27386/27386) are now hard constraints** on the body decode:
the 3800-bit body must reference exactly these 125 strings.

**Body-width hypothesis H1 (773 bits consumed, values implausible — preliminary).**
The hypothesis `3 B (3) + DefaultActiveRowIndex (22) + ColumnCount (16) +
4 × [Format (16) + Removable/Editable/Constant (3) + UnmatchedValue (16) +
DefaultValue (16) = 51] + RowCount (16) + 8 × 4 × cell (16)` sums to exactly
**773 bits**, a strong signal the field-width scheme is close. But the decoded values
are implausible (`DefaultActiveRowIndex = 526009`, `ColumnCount = 2725`,
`RowCount = 65310` instead of 4 / 8), so the 16-bit fields are *not* the count ints
and the leading region's field order / widths still need refinement. The 3 B flags
decode to `(1, 0, 0)` (plausible), with only the 3rd flag in the varying set — so the
per-instance data is not a clean leading block. Next step: pin the exact leading-region
field order (the P/Invoke names are function names, not on-disk order) and the int /
variant encodings, then validate a full parse against all five bodies.

<details><summary>Superseded: the "96-bit record" interpretation (previous pass)</summary>

<p><em>The following section was derived from a T6-only 191-aligned view and is
**disproven** by this pass — see the correction above. It is retained for the record
only.</em></p>

### The table body: isolated, sized, and structured

 With the string region out of the way, the **remaining undecoded data** is the region
 between the `be_*`/`eed1071` prefix and the string region — the actual
 **column / row / cell** data. `.tmp-decode/Decode2.cs` `body` mode isolates it: it
 walks the verified prefix (common header + 5-field expression + the `be_*`/`eed1071`
 block-element fields) to find the body start, uses the library's `SetPositionByFlag`
 to find the string-region start, and dumps the region in between.

 | object | body (bits) | body (bytes) |
 |---|---|---|
 | TABLE #1 | 1665 | 208.1 |
 | TABLE #3 | 5396 | 674.5 |
 | TABLE #6 | 1151 | 143.9 |
 | GRIP #2 / #4 / #5 | **0** | 0 |

 Two immediate findings:

 * **The grips carry no body at all.** A grip's tail is *entirely* the string region —
   after its `be_*`/`bg_*` fields the next thing is the 17-bit metadata + strings.
   That is consistent with the semantic model: a grip is a single point (a location),
   not a table, so it has no columns/rows/cells.
 * **The table body is a *header + repeating records* structure.** An autocorrelation
   scan (`.tmp-decode` `body` mode, match-fraction at fixed lags) shows a strong
   **96-bit period** in TABLE #6 (0.848 at lag 96, 0.842 at lag 192 = 2×96, against a
   0.69 baseline). TABLE #1 / #3 are mostly zero-padded (0.76–0.82 baseline) so their
   period is less distinct, but they peak at lag 160. In raw bits the record boundary
   is visible as the repeating `0000010100101000…` motif at 96-bit intervals. So each
   table body = a short **header** (≈ the first ~191 bits, holding the table-level
   counts/flags) + **N records of 96 bits** (the per-row or per-column records).

 **Record structure confirmed** (`.tmp-decode` `recs` mode, dumping 96-bit windows at the
 record offsets and computing the pairwise match): TABLE #6's records match at **0.917**
 (a 56-bit constant prefix + 14 scattered data bits — the per-bit agreement line shows
 exactly which bits vary), TABLE #1 at 0.800, TABLE #3 at 0.650 (its cells hold more
 distinct data, so the records agree less). This confirms the body is a fixed-layout
 record array: a constant "shape" per record with a small per-record payload.

   **Header vs. records separated** (`.tmp-decode` `recs0` mode, dumping the full 96-bit
  window sequence from offset 0). The body splits cleanly into a **variable-size header**
  (the first window(s)) and **N records of 96 bits**:

  | object | body | 96-bit windows | rem (bits) | header ≡ (mod 96) |
  |---|---|---|---|---|
  | TABLE #1 | 1665 | 17 | 33 | 33 |
  | TABLE #3 | 5396 | 56 | 20 | 20 |
  | TABLE #6 | 1151 | 11 | 95 | 95 |

  The **header size varies** table-to-table (≡ 33 / 20 / 95 mod 96) because it holds the
  **column definitions** — variable-size fields (a `Parameter` handle + a `Format` string
  + `DefaultValue` / `UnmatchedValue` variants) — while the **records are a fixed 96 bits**.
  The records (windows 2+) are near-identical across the three tables (a 56-bit constant
  prefix + a small variable suffix); the header (windows 0–1) differs table-to-table
  (TABLE #1 and #6 share a 16-bit prefix; TABLE #3 is distinct). This is consistent with
  the semantic model: the **header holds the columns**, and the **records are the rows**
  (each row = 96 bits of cell data).

  **Varying-bit map (TABLE #6, all 9 records, `.tmp-decode` `recs` mode).** The per-bit
  agreement across the 9 records shows **15 varying bits** at positions 56–58 (3b), 64–71
  (8b), and 88–91 (4b):

  | rec | 56–58 | 64–71 | 88–91 |
  |---|---|---|---|
  | 192 | `000` | `01011011` | `0001` (1) |
  | 288 | `000` | `01101011` | `0010` (2) |
  | 384 | `010` | `01110100` | `0011` (3) |
  | 480 | `000` | `01111011` | `0100` (4) |
  | 576 | `111` | `00000000` | `0101` (5) |
  | 672 | `010` | `10001000` | `1100` (12) |
  | 768 | `101` | `00001110` | `1111` (15) |
  | 864 | `000` | `01011100` | `0000` (0) |
  | 960 | `011` | `00011101` | `0001` (1) |

  The **4-bit group (88–91) is sequential (1, 2, 3, 4, 5) for the first five records** — a
  strong hint that one field is a small per-record index / count (the record's position in
  the table, or a cell count). The other two groups (56–58, 64–71) vary less regularly and
  likely hold the cell value(s) + type code(s).

  **The record as bytes (`.tmp-decode` `recs` mode, 96-bit window at the 191-bit
  alignment, regrouped into 12 bytes).** The 96-bit record is 12 bytes: a **7-byte
  constant prefix** (`02 94 00 00 00 00 00` — the 56-bit constant prefix) + a **5-byte
  variable suffix**:

  ```
  rec@191: 02 94 00 00 00 00 00 | 00 0B 68 08 0A 00
  rec@287: 02 94 00 00 00 00 00 | 00 0D 68 08 12 00
  rec@383: 02 94 00 00 00 00 00 | 08 0E 88 08 1A 00
  rec@479: 02 94 00 00 00 00 00 | 00 0F 68 08 22 00
  rec@575: 02 94 00 00 00 00 00 | 1C 10 08 08 2A 00
  rec@671: 02 94 00 00 00 00 00 | 08 10 88 08 32 00
  rec@767: 02 94 00 00 00 00 00 | 14 10 E8 08 3A 00
  rec@863: 02 94 00 00 00 00 00 | 00 11 68 08 42 00
  rec@959: 02 94 00 00 00 00 00 | 0C 11 C8 08 4A 00
  ```

  The variable suffix (bytes 7–11) shows two strong signals: **byte 11 increases by
  exactly 8 per record** (10, 18, 26, 34, 42, 50, 58, 66, 74) — a per-record counter or
  an offset into a shared data array — and **byte 8 increases** (11, 13, 14, 15, 16, 16,
  16, 17, 17). Byte 10 is constant (`08`). The 7-byte constant prefix (`02 94 …`) is the
  shared "shape" of the record; its exact field type is still not pinned down.

  **Open:** the exact per-record field layout (which 96 bits are the cell values, the type
  codes, the variant payload) and the header fields (the column count, the per-column
  `Constant` / `Editable` / `Removable` / `DefaultValue` / `UnmatchedValue` /
  `CustomProperties` / `Parameter` / `Format`, then `DefaultActiveRowIndex`, `MustMatch`,
  `ContainsRuntimeParametersOnly`). The record *period* and *shape* are established; the
  *fields within a record* and the *header fields* are the next thing to pin down.

</details>

## Status & next steps

**Done (this work):**
1. **Implemented the decoded layout** in the `BlockPropertiesTable` /
   `BlockPropertiesTableGrip` model (replacing the old `RawTail`-only placeholders):
   the 5-field expression + the grip's `be_*`/`eed1071`/`bg_bl91`/`bg_bl92`/
   `bg_location` (3BD) / `bg_insert_cycling` / `bg_insert_cycling_weight`, with the
   undecoded tail (the 91-bit grip gap; the whole table tail after the `be_*`/
   `eed1071` triple) preserved verbatim in `RawTail` to keep round-trips lossless.
2. **DWG reader + writer** read and write the decoded fields in on-disk order.
3. **DXF writer** emits the fields (codes `90`–`96` + `10`/`20`/`30`). *Limitation:*
   there is no authoritative DXF code reference for these classes (LibreDWG's struct
   is empty, and no AutoCAD DXF sample with a BPT record was found), so the codes are
   **assigned by analogy** and may not match AutoCAD's own.
4. **Tests** — `BlockPropertiesTableTests` (sample-gated, self-skipping) verify the
   decoded fields against `L3-02-Dynamic Blocks.dwg`.

**Breakthrough (this pass):** the table body is **not** an opaque/compressed blob — it is
a standard R2010+ object whose **inline string region** the library's own
`SetPositionByFlag` + `ReadVariableText` already reads. The 17-bit tail is
`[16-bit size][1-bit flag]` (flag = 1 for all six objects; the size is a small bit count
— 898 / 74 / 1104 / 74 / 74 — read as a byte-swapped little-endian `UShort`, none with
the `0x8000` extended bit set). The library extracts the real strings (tables:
`"Block Table"` / `"Block Table1"`; grips: `"Grip"` + related dynamic-block strings).
See the [breakthrough section](#breakthrough-the-body-is-a-standard-r2010-object--the-opaque-blob-was-a-misaligned-read).

**Now confirmed from both sides (this pass):** the complete region layout is verified
against **both** the writer (`WriteSpearShift` L240–289) and the reader
(`SetPositionByFlag` L374–402 + `applyFlagToPosition` L1139–1170), and a standalone
byte-level decoder (`.tmp-decode/guided/Guided.cs`, no library readers) reads the string
region with **exact** consumption — 898/898, 1104/1104, 2352/2352 bits across the three
`L3-02` tables. Two non-obvious details were nailed: the 16-bit size field is
**little-endian** (raw bytes, not the MSB-first bit read), and the string region ends at
`handleStart − 17` (start `handleStart − 17 − S`). This also sizes the class-specific
body (1665 / 5396 / 1151 bits) and the constant **16-bit** common-header-handle span.
See the [complete-region-layout section](#the-complete-r2010-region-layout-writer--reader-both-confirmed-and-exact-string-region-consumption).

**Still open:**
1. **Wire the string region into the reader/writer.** `readBlockPropertiesTable` /
   `readBlockPropertiesTableGrip` currently read only the main-data fields + `RawTail`
   and never touch the `_textReader` / merged reader, so the decoded objects do not yet
   carry the extracted strings. Add the `ReadVariableText` calls (matching the on-disk
   order of the object's `T` fields) so the objects carry the real string fields.
2. **Map the extracted strings to the semantic model** — are `"Block Table"` /
   `"Block Table1"` the table name, a column `Format`, or a column `Parameter` name? Do
   the grips' larger strings hold the table's row/cell data, or are they unrelated
   dynamic-block strings? (LibreDWG's `Dwg_Object_BLOCKPROPERTIESTABLE` is empty, so the
   string→field mapping must be derived from the data + the semantic model.)
3. **Pin down the ~74-bit part of the grip gap that precedes the 17-bit metadata**
   (its field type is still not confirmed).
 4. **Decode the table body's field layout.** The body is a **continuous self-describing
    typed-field stream** (not a 96-bit record array — that was a windowing artifact;
    see the [body-structure section](#the-table-body-a-continuous-typed-field-stream)).
    The next step is to decode it **guided by the semantic-model field types**
    (`MustMatch` B, `ContainsRuntimeParametersOnly` B, `DefaultActiveRowIndex` BL,
    `ColumnCount` BL, `ColumnCount × [Parameter handle + name + type +
    `Constant`/`Editable`/`Removable` B + `DefaultValue`/`UnmatchedValue` variants]`,
    `RowCount` BL, `RowCount × ColumnCount × variant`), using the two identical 773-bit
    `Block Properties Table.dwg` tables to separate constant (structural) from varying
    (data) bits.

    **Status of the guided decode:** the semantic model is now **fully decompiled** from
    the AutoCAD 2021 .NET API (`acdbmgd.dll` → `.tmp-decode/autocad/BPT_column_row.cs`),
    not just the Table but the `BlockPropertiesTableColumn` and
    `BlockPropertiesTableRow` wrappers too (see the
    [full-model section](#the-full-model-decompiled-from-the-autocad-2021-net-api-acdbmgdDll)).
    It gives the field *types*: a column's main-stream footprint is
    `[Format string][Removable B][Editable B][Constant B][UnmatchedValue variant]
    [DefaultValue variant]` (the `Parameter` + `CustomProperties` handles live in the
    handle stream), and a row's is `colCount` cell variants. But the P/Invoke method
    names are *function* names, **not** the on-disk field order, so a brute-force over
    `colCount` (1–8) × `rowCount` (1–15) × the int encoding (BL / BS / raw-32) is
    running (`.tmp-decode/guided/Guided.cs`), validating that the body is fully consumed
    *and* that the string-typed-field count matches the decoded string region
    (9 / 12 / 20). As of this writing it is **not yet landing**: the assumed field
    order keeps misaligning the bit stream (the parser trips over tag-`11` fields that
    are undefined for BL/BD). The open questions it must settle: the exact on-disk
    field *order* (the 3-B order, the 2-variant order, and where `RowCount` sits —
    before or between the columns and rows); the int encoding (BL vs BS vs raw-32);
    and the int64 variant width (assumed 3 × BL). The two identical 773-bit
    `Block Properties Table.dwg` tables are the planned constant-vs-varying cross-check.

     **New hard constraint (R2025, this pass):** the 3rd schema's string region is
     consumed **exactly** by **125 strings** (27386/27386 bits, 0 remaining — the earlier
     "50-string / 7452-bit gap" anomaly was a 50-string stop condition, see the
     [3rd-schema section](#3rd-independent-schema-confirmed-on-r2025-ac1032--the-region-layout-holds)).
     So the 3800-bit body must reference **exactly 125 strings**: the string-typed-field
     count in the body = 125 = 21 (table- + column-level) + 104 (cell values). The 104
     cell values = `rowCount × colCount` (e.g. 8×13 or 4×26), which — together with the
     773-bit pair (4×8 = 32 cells) — is the key constraint set for the body-width search
     (`.tmp-decode/guided/Crack773.cs` / a two-body solver). The body's dense leading
     region (chunks 0–4) is the table- + column-level fields; the sparser tail
     (chunks 5–37) is the row / cell data.

**Two-schema string-count constraint (derived this pass).** The body's string-typed-field
count = the string region's string count. Let `T` = table-level string fields (shared
across schemas — the `Table` class is identical) and `C` = string fields per column
(shared — the `Column` class is identical). Then:

- 773-bit schema (47 strings, 32 cells = 4×8): `T + 4C = 15`.
- 3800-bit schema (125 strings, 104 string-cells): `T + colCount₂·C = 21`.

Subtracting gives `C·(colCount₂ − 4) = 6`, whose integer solutions are:

| `C` (string fields/col) | `colCount₂` (R2025) | `T` (table-level) |
| --- | --- | --- |
| 3 (Format+Default+Unmatched) | 6 | 3 |
| 2 | 7 | 7 |
| 1 | 10 | 11 |

**Leading hypothesis: `C=3`, `colCount₂=6`, `T=3`** — i.e. the R2025 table has **6
columns** (vs 4 for the 773-bit pair) and the same **3 table-level string fields**
(the 2 leading named `"Block Table"` / `"Block Table1"` + 1 empty). The 773-bit pair
then has 4 columns, `T=3`, `C=3` (15 = 3 + 4·3 ✓) and 32 cells (4×8 ✓); the R2025
schema has 6 columns, `T=3`, `C=3` (21 = 3 + 6·3 ✓) and 104 string-cells. Note the
R2025 cell count is **not** a clean `6 × rowCount` (104/6 = 17.3), so not every cell
is a string — some are numeric (in the main stream) — consistent with the 3800-bit
body being ~5× the 773-bit body. This is the constraint set for the body-width search.

**The uniform-C model is disproven — the 125 strings are in field order (preliminary, this pass).**
The 3rd schema's 125 strings decode in field order (100% region consumption) and show a
non-uniform per-column string pattern:

| field order | string content | interpretation |
| --- | --- | --- |
| [0] | "Block Table" | table-level name 1 |
| [1] | "Block Table1" | table-level name 2 (parent block?) |
| [2] [3] | "" "" | table-level empty strings (T=4?) |
| [4-7] | "UserVariable" "Custom" "" "" | column 0 (4 strings) |
| [8-11] | "UserVariable" "Custom" "" "" | column 1 (4 strings) |
| [12-15] | "UserVariable" "Custom" "" "" | column 2 (4 strings) |
| [16-18] | "UpdatedDistance" "" "" | column 3 (3 strings) |
| [19-20] | "VisibilityState" "" | column 4 (2 strings) |
| [21-124] | 104 value strings ("1kV Keet", "Klassiek", "Dubbelzijdig", "Rechts"/"Links"...) | row cell values |

So the table has **5 columns** with string counts **[4,4,4,3,2]**, **T=4** table-level
strings, and 104 string cells. `Column` evidently carries a **name** string on disk
(plus Format, plus the UnmatchedValue/DefaultValue variants, which contribute a string
only when string-typed):

- `UserVariable` columns: name + format + 2 string variants = 4.
- `UpdatedDistance`: name + 1 string variant + 1 numeric variant = 3.
- `VisibilityState`: name + 2 numeric variants = 2.

The 104 string cells / 5 columns = 20.8, so the rows are not a clean 5×N: the numeric
cells (the `UpdatedDistance`/`VisibilityState` double/bool values) are the difference
(e.g. 21 rows × 5 = 105 cells − 1 numeric cell, or 20 rows + 4 extra string cells).
This supersedes the uniform-C hypothesis (C=3, col₂=6, T=3) — the `C` is not uniform
across columns, so the two-schema constraint `C·(colCount₂−4)=6` has no solution for
the real layout. **Open:** the exact per-variant on-disk encoding (tag width, payload
widths) and the exact row count; the 3800-bit body's leading region starts `10` + 46
zero bits + a sparse 1-run (bits 53,55,60,61,74,79,81,83,85..94...), which must be
segmented into the header + column fields.

## L3-02 DWG: three more same-schema bodies (preliminary)

The user provided `l302.dwg` (571035 bytes, **AC1032 / R2025** — same version as
`1kVKeetKOPIE.dwg`, hence the **same BPT encoding schema**). It contains **three**
`BLOCKPROPERTIESTABLE` objects, all decoded with the verified R2010+ region layout:

| object | body bits | string region | strings | consumed |
| --- | --- | --- | --- | --- |
| TABLE1 | 1665 | 898 | 9 | 898/898 = 100% |
| TABLE2 | 5396 | 1104 | 12 | 1104/1104 = 100% |
| TABLE3 | 1151 | 2352 | 20 | 2352/2352 = 100% |

Size arithmetic cross-check (verified): `size*8 − handleSize = 10 (class type) + 134
(prefix) + body + S + 17 (tail)` holds for all three (e.g. TABLE1: 351b →
2808 − 84 = 2724 = 10 + 134 + 1665 + 898 + 17). The "body" region extraction is
therefore **correct** and is the real bit-packed BPT data.

### Per-table string structure (field order confirmed by 100% consumption)

| table | col string counts | cell strings |
| --- | --- | --- |
| TABLE1 | [3, 2] | 0 |
| TABLE2 | [3, 3, 2] | 0 |
| TABLE3 | [4, 3] | 10 ("1 Space" … "10 Spaces") |

Column name/format content: TABLE1 = "UpdatedDistance" (3) + "UpdatedDistance" (2);
TABLE2 = "UserVariable" (3) + "UpdatedDistance" (3) + "UpdatedDistance" (2);
TABLE3 = "UserVariable"/"Custom" (4) + "UpdatedDistanceX" (3). All 4 tables (incl. 1kV)
share the 4 table-level strings "Block Table", "Block Table1", "", "".

### Cell/row inference (preliminary)

- **1kV: 26 rows** — the 104 cell strings form 26 repeating 4-tuples
  (panel name, subtype, orientation, extended name); 1 of the 5 columns is
  numeric (a double), the other 4 are string-valued.
- **TABLE3: 10 rows** — 10 string cells = 10 rows × 1 string column
  ("UpdatedDistanceX" is string-valued: "1 Space" … "10 Spaces"); the
  "UserVariable" column is all-numeric.
- **TABLE1 / TABLE2: 0 string cells** — all-numeric (row count not yet derivable
  from strings alone).

### Negative results this round (quantitative, C# tools)

- **JointBPT** (size-equation system over all 4 same-schema objects, per-column
  variant-type assignment, tag 1–4 bits, int payload 8–40, double payload 64–80,
  defRow 1–72 bits, table flags 3–8): **no exact solution**.
- **SemanticParser** (8 variant encodings × flag widths × defRow encodings × row
  counts 18–24 on the 3800-bit body): no exact 3800-bit consumption; near-misses
  stall mid-row.
- **StreamDecode** on the 3800-bit body: leading region = bit0 = 1, bits 1–51 = 51
  zeros, then sparse 1s at {53,55,60,61,74,79,81,83,85..94,…}; no clean B/BL/BS/BD
  segmentation found among the leading programs.
- Parity/uniform-width two-body model: no solution (earlier).

### New hypothesis (preliminary, unverified)

The `hasDsBinaryData` object-header flag (R2013+, `AcDb:AcDsPrototype_1b` data-store
section) makes the body look possibly **binary/compressed** rather than plain
B/BL/BS/BD fields (TABLE2's body shows repeating `0011 1100` byte patterns in its
leading ~80 bits). If the BPT payload is a data-store blob, the uniform-width
field model is invalid and explains all no-solution results. **Next step:** dump the
AC1032 file's section table (real R2025 header, not the R2004 layout) and check for
an AcDs section whose size correlates with the body sizes; also finish the
independent-type JointBPT search.

## ODA spec + data-store verification (preliminary)

The user provided the **Open Design Alliance "Specification for .dwg files"** PDF.
Extracted (15,017 lines). Findings:

- The ODA spec covers the general R2010+ object format (confirmed: the
  `Has DS binary data` B flag at the common-object-header tail, R2013+) and
  documents the **`AcDb:AcDsPrototype_1b` data-store section in full** (§24):
  a **byte stream** (not a bit stream) of file segments (segidx / datidx /
  `_data_` / schidx / schdat / search / blob01), each with a 64-byte header
  (0xd5ac signature), indexed by **handle**. The file header (first ~52 bytes)
  carries the segment-index offset/entry-count and the schema/data/search
  segment indexes. A data record = `[dataSize u32][bytes]`; large values go
  through a `0xbb106bb1` blob-reference (paged `blob01` segments).
- **The ODA spec contains NO `BlockPropertiesTable` / BPT / EvalVariant**
  content — it predates the feature (last content ~2013). So it cannot give
  the BPT field layout, but it is the authoritative reference for the
  data-store and confirms the R2010+ primitives.

### Data-store check on L3-02 (negative for BPT)

Ran the library's own `DwgPrototype1bReader` over L3-02's `AcDb:AcDsPrototype_1b`
section (29,696 bytes). Result:

- File header: sig 0x6472616a, version 2, rev 1152, 19 segment entries.
- **5 schemas, all system**: `AcDb_Thumbnail_Schema`, `AcDbDs::TreatedAsObjectDataSchema`,
  `AcDbDs::LegacySchema`, `AcDbDs::IndexedPropertySchema`, `AcDbDs::HandleAttributeSchema`.
- **1 data record** (handle 0x22, 1,236 bytes) = a **PNG thumbnail**
  (`89 50 4E 47 0D 0A 1A 0A` + IHDR). **No BPT data-store entry.**

**Conclusion:** the BPT object's data is **inline** in the object section (the
"body" region we extract), NOT in the data store. The data-store hypothesis is
ruled out for these files. The body is a valid bit-packed stream (it segments
cleanly into B/BL/BS/BD with plausible values) — the missing piece is the
**field layout** (order + type of the table/column/cell fields).

### 1kV row structure — confirmed

The 104 cell strings form **26 rows × 4 string columns** (row-major), plus one
numeric (double) column. Each row = 4-tuple `(a,b,c,d)`:
- a = full oriented name ("1kV Keet", "1kV Keet - VPR", "1kV Keet - VPL", "Uitbreiding - …"),
- b = base/subtype ("Klassiek", "Aanduiding omvormers", "Smeltveiligheden", "BIC/LIC/SEC", "IL Shelter", "Double Wide"),
- c = orientation ("Dubbelzijdig" / "Rechts" / "Links"),
- d = extended name.
So **1kV = 26 rows × [4 string cols + 1 double col]**. Column string counts
[4,4,4,3,2] → string variants [2,2,2,1,0], numeric variants [0,0,0,1,2].

### Diophantine size solver (preliminary, unverified)

Built a 2-stage solver over all 4 same-schema bodies. Model: a field = a type
tag of `w` bits + a payload; string/empty payload = 0, int = I, double = D;
column = 3 flag bits + 2 variants; body = T + defRow + flags + (nVariant·w) +
(cI·I + cD·D). Stage 1 uses 1kV−t3 = 2649 → `di·I + dd·D = 2649 − 116w`;
stage 2 verifies all 4 (solving R1, R2).

Result: a family of solutions, **w=7–8, I≈36–57, D≈93–95**, R1≈9–15,
R2≈17–20. The widths are **non-standard** (a double ≈ 93–95 bits, not 64) and
the system is **under-constrained** (T+defRow is degenerate — only their sum
is pinned). The clean `I∈{32,34,36}, D∈{64,66,68}` assumption is **not
consistent** (1kV−t3=2649 has a parity/magnitude mismatch for those). This
means either the double cell is a ~93-bit structure (e.g. 64-bit double + a
~29–31-bit extra field per cell) or the model is missing a per-field component.
**Open:** the exact double/cell on-disk width and whether there is an extra
per-cell (or per-row) field.

## R2010+ primitive widths — the missing piece (preliminary)

The ODA spec §2.2–2.5 (authoritative, covers R2010+) defines the object-section
bit primitives. **The tag is 2 bits** (not 1), and the width is value-dependent:

- **BS** (bitshort): `00`→2-byte LE short, `01`→1-byte, `10`→0, `11`→256.
  ⇒ a string field = **18 bits** (non-empty, 16-bit offset) or **2 bits** (empty).
- **BL** (bitlong): `00`→4-byte LE long, `01`→1-byte, `10`→0.
  ⇒ an int field = **34** (large) / **10** (small) / **2** (zero) bits.
- **BD** (bitdouble): `00`→8-byte IEEE, `01`→1.0, `10`→0.0.
  ⇒ a double field = **66** (non-zero) / **2** (0.0 or 1.0) bits.

### 1kV fits the model (preliminary, single body)

With these widths, 1kV (3800 bits, 4 string cols + 1 double col, 26 rows):

| region | count | bits each | subtotal |
|---|---|---|---|
| 4 string columns (name18+fmt10+3flags+unm2+def2) | 4 | 35 | 140 |
| 1 double column (name18+fmt2+3flags+unm2+def2) | 1 | 27 | 27 |
| 104 string cells | 104 | 18 | 1872 |
| 26 double cells (non-zero) | 26 | 66 | 1716 |
| header (T flags + defRow) | — | — | ≈45 |
| **total** | | | **3800** ✓ |

140 + 27 + 1872 + 1716 + 45 = **3800**. The earlier solver's "double ≈ 93–95 bits"
was an artifact of a fixed-width model that ignored the R2010+ 2-bit-tag +
value-dependent-payload structure. **Open:** the other 3 bodies (t1/t2/t3) do not
fit this assignment cleanly (their implied header is ~200 bits) — the cell
string/numeric assignment per body needs empirical decoding, not assumption.

## LibreDWG spec — the `Dwg_EvalVariant` / `Dwg_EvalExpr` structures (breakthrough)

The user provided the **LibreDWG spec** (2010–2025, 14,068 lines). LibreDWG's
`BLOCKPROPERTIESTABLE` object struct is **empty** (`parent` only — they have not
cracked the table body), but it **does define the two key sub-structures** that
the BPT body is built from (Chapter 4, p.237):

### `Dwg_EvalExpr` (the expression the BPT inherits)
```
Dwg_EvalExpr
    parentid     BLd
    major        BL
    minor        BL
    value_code   BSd
    value.num40  BD
    value.pt2d   2RD
    value.pt3d   3BD
    value.text1  TV
    value.long90 BL
    value.handle91 H
    value.short70 BS
    nodeid       BL
```
The `value` is a **union selected by `value_code`** (a resbuf code):
`40`→BD, `2`→2RD, `3`→3BD, `1`→TV, `90`→BL, `91`→H, `70`→BS.

### `Dwg_EvalVariant` (a single variant = a resbuf entry)
```
Dwg_EvalVariant
    code   BS
    u.bd   BD
    u.bl   BL
    u.bs   BS
    u.rc   RC
    u.text TV
    u.handle H
```
A `Dwg_EvalVariant` = `[code: BS][value: <union by code>]`. The `code` is a
**BS** (2-bit tag: `00`→16-bit, `01`→8-bit, `10`→0, `11`→256) holding the
resbuf code; the value follows in the code-specific primitive. So a double cell
= `code(40)` + `BD` = 10 + (2 or 66) bits; a text cell = `code(1)` + `TV`;
a long cell = `code(90)` + `BL`.

### Full BPT body layout (R2010+/AC1032)
```
[common object header]
[Dwg_EvalExpr]          parentid, major, minor, value_code, value(union), nodeid
be_major     BL  (DXF 98)
be_minor     BL  (DXF 99)
eed1071      BL  (DXF 1071)
<table data>  = column definitions + rows; each unmatched/default/cell value
               is a Dwg_EvalVariant = [code BS][value union]
```
The existing ACadSharp reader already reads `[EvalExpr][BeMajor][BeMinor]
[Eed1071]` and preserves the rest as `RawTail`. **The `RawTail` is the table
data, and its cell/default values are `Dwg_EvalVariant`s** — this is the model
to decode. The `d` suffix (BLd/BSd) is a LibreDWG annotation for fields with a
default value; the on-disk primitive is the base (BL/BS). **Open:** the exact
table-data layout (numColumns, column fields, numRows, rows) and confirmation
of the `value_code`/`code` → union mapping on real bytes.

## Breakthrough (this session): the RawTail = the table data + the full R2010+ tail — exact 1kV numbers

Using the **verified existing reader** (not hand-math), the `RawTail` the reader
captures is now fully understood. It is the **entire remainder of the object
after the BPT prefix** — i.e. `RawTail = [table data][string region (S)][size
field][1-bit flag]`, extending all the way to the handle-region start.

**Why:** `DwgObjectReader.cs:245,257` sets
`handleSectionOffset = dataStart + sizeInBits − handleSize` and positions
`_handlesReader` there. `readRawTail` (`Objects.cs:385`) computes
`remaining = _handlesReader.PositionInBits() − _objectReader.PositionInBits()`,
so the RawTail runs from the end of the prefix (after `eed1071`) to the handle
region start — which includes the string region, size field, and 1-bit flag.

### The 1kV object (AC1032), verified
| field | value | source |
|---|---|---|
| `size` (modular short, bytes) | 3941 → `sizeInBits` = 31528 | scan |
| `handleSize` | 181 bits | scan |
| `RawTailBitCount` | **31219** | verified reader |
| `S` (string region) | **27386** bits | L302Dump, 100% consumed |
| string count | **125** | L302Dump |
| size field | 16-bit LE | L302Dump |
| 1-bit flag | **1** | L302Dump |
| `be_major` / `be_minor` / `eed1071` | **33 / 73 / 0** | verified reader |
| **table data** | `31219 − 27386 − 16 − 1` = **3806 bits** | derived |

So the **table data is the first 3806 bits of the RawTail** (the string region is
the next 27386 bits, then the 16-bit size field, then the 1-bit flag).

### The "body" == the table-data region (the earlier 1kV fit is corrected)
The L302Dump "body" (3800 bits, `[after the 134-bit prefix][before the string
region]`) **exactly equals the RawTail's first 3800 bits** (verified by the
`Align` tool, 3800/3800 bit match). Combined with the 3806-bit table-data size,
this confirms the table-data region sits at the **head of the RawTail** and the
"body" and "table data" are the same region (the 6-bit delta is a 2-bit
`handleStart` offset in the L302Dump extraction). **This corrects the earlier
"1kV fits 3800" size table**, which had (incorrectly) counted 1872 bits of
string cells *inside* the 3800-bit body — in R2010+ a string value consumes
**zero main-stream bits** (the text lives in the string region; only the
`code` BS is in the main stream).

### The 1kV table content (125 strings, in reference order)
- **Names:** `"Block Table"`, `"Block Table1"`.
- **Column names:** `UserVariable` (×3), `UpdatedDistance`, `VisibilityState`.
- **Rows:** ~24 rows of Dutch text (panel/distribution-board names, e.g.
  `"1kV Keet"`, `"Klassiek"`, `"Rechts"`/`"Links"`, `"BIC/LIC/SEC"`,
  `"Lastscheider/Contactor"`, `"IL Shelter"`, `"Double Wide"`, …) — each row
  = a set of property values for one configuration.

**Next:** decode the 3806-bit table-data stream as a sequence of
`Dwg_EvalVariant` (`[code BS][value]`) cells, pulling string cells (code 1) from
the string region in order. The `TDecode` tool does this; the open question is
the header layout (name, numColumns, numRows, flags) before the cell matrix.

### Status & open questions (end of this session)
**Cracked / verified (solid):**
- The R2010+ object envelope and the `handleSectionOffset = objectStart + sizeInBits −
  handleSize` relation (the RawTail runs from the end of the prefix to the handle-region
  start, and therefore includes the string region, size field, and 1-bit flag).
- The 1kV object's exact numbers (RawTailBitCount 31219, string region 27386 bits = 125
  strings, 100% consumed; `be_major`/`be_minor`/`eed1071` = 33/73/0).
- The table-data region (the first ≈3800–3816 bits of the RawTail) and its **content**
  (a "Block Table" with columns `UserVariable`×3, `UpdatedDistance`, `VisibilityState` and
  ≈24 rows of Dutch panel/distribution names).

**Open (the hard remaining piece):**
- The table-data **cell encoding** — the 3800–3816-bit region does **not** decode as a
  flat `BL`/`BS`/variant stream from bit 0 (the codes come out as garbage, e.g. 12288,
  2.05E+279), so it has a header + a structured column/row layout that is not yet cracked.
  The `Dwg_EvalVariant` (`[code BS][value union]`) model from LibreDWG is the best
  hypothesis for individual cells, but the wrapping table header (name, `numColumns`,
  `numRows`, flags) and the exact column/row field order are unresolved.
- The exact `flag` / size-field position is sensitive to a 2-bit `handleStart` offset
  (the verified reader's RawTail last bit is 0, while the L302Dump extraction reports
  flag 1) — the 125-string content is the robust anchor, not the raw flag byte.

**Implementation status:** the `BlockPropertiesTable` reader reads `[EvalExpr][be_major]
[be_minor][eed1071]` and preserves the rest as `RawTail` (bit-exact round-trip, verified
by `BlockPropertiesTableTests`); the writer re-emits it. The DXF reader/writer is a
stub (the class has no public ObjectARX property API — only the current value is
addressable), which is consistent with the table data still being undecoded.

### CORRECTION (verified): the record is 126 bits; the 504-bit period was 4×126
The earlier "63-byte (504-bit) repeating record" hypothesis is **retired**: 504 = 8×63
but is also **4×126**, and the authoritative 8-bit-chunk diff tool (`TDecide`, built to
eliminate the `long` overflow / shift-wrap corruption that corrupted the earlier
`TPattern`/`TCheck2` dumps) shows the true period is **126 bits**: the `02 94 00 00 00 00
00 00` marker repeats at exact 504-bit intervals (= 4 record periods), which was the
source of the 504 illusion.

**The 126-bit record layout (verified: 25 full records + the 26th, all constant-except
7 bits; bit-level diff via 3-chunk 41/41/44-bit extraction, no overflow):**

```
bits  0..7:    00000010  (0x02)   constant
bits  8..15:   10010100  (0x94)   constant
bits 16..63:   00000000 × 6 bytes constant
bits 64..71:   0000010X            X = bit 70  (VARIES: 1 in 3 of 25 records)
bits 72..79:   10001000  (0x88)   constant
bits 80..87:   00001000  (0x08)   constant
bits 88..95:   00001010  (0x0a)   constant
bits 96..103:  00 FF 00 00        6-bit field at bits 97..102 (97 = MSB)  (VARIES)
bits 104..111: 10000000  (0x80)   constant
bits 112..119: 10100000  (0xa0)   constant
bits 120..125: 001010            constant
```

(Constant 1-bits: 69, 72, 75, 84, 91, 93, 104, 112, 114, 122, 124 — verified by the
rawest direct byte-level read `TBits2`, which supersedes the earlier TSplit/TFinal
bit-alignment: e.g. the `0x04` byte 8 has its 1 at bit 69, and the 0x28 byte 15
contributes `001010`, not `000010`.)

Only **7 bits vary** across the 25 full records: bit 70 and bits 97..102 — together they
form a **7-bit index** (`bit70 << 6 | 6b[97..102]`) into the 126-string pool. There are
no per-record flags in the full records (earlier "flags" at bits 107/115/117 were
contamination: the 26th window at bit 3694 extends 22 bits into the string region, and
its string-region bits were misread as record bits).

**1kV record table (26 records, verified; 7-bit index → string content from the 126-string
pool):** k0=28 "Rechts", k1=29 "1kV Keet - VPR", k2=53 "1kV Keet aanduiding omvormers -
VPR", k3=118 "IL Shelter", k4=119 "Double Wide", k5=94 "IL Shelter", k6..k24 = the
consecutive run 31..49 (Klassiek, Links, 1kV-VPL, 1kV Keet, Klassiek, Dubbelzijdig,
Uitbreiding-keet, 1kV Keet, Klassiek, Rechts, Uitbreiding-VPR, 1kV Keet, Klassiek,
Links, Uitbreiding-VPL, 1kV Keet, Aanduiding omvormers, Rechts,
"1kV Keet aanduiding omvormers"), k25=120 "Rechts".

**The 26th record is truncated:** the table data is `544-bit header + 25×126 + 104 bits`,
and the 104-bit trailer is the first 104 bits of a 26th 126-bit record
(`02 94 00 00 00 00 00 00 04 88 08 0a 70`, 7-bit index = 120) — its last 22 bits are
absent. A 522-bit-header / 26-full-records alternative (`522 + 26×126 = 3798` exactly)
is **disconfirmed** by the `02 94` marker at bit 544 (with a 522 start, bits 544..551
would be record byte 3 = `00 00`, but they are `02 94`).

**Open:** the 544-bit header layout; whether the truncated 26th record is a writer
truncation or a variable record length; the record's semantic role (a row reference? a
column option list?).

### CORRECTION: the string region starts at bit 3798 (largest-P rule, re-verified); the tail is 33 bits
The earlier "3816-bit table data" (from `RawTail − S − 16 − 1`) and the subsequent
"bit 3800" figure are **retired**. The authoritative rule (`TExtract`, re-run and
re-verified on the 1kV body): decode a `[BS len][len×2 bits]` sequence from candidate
start `P`, require **100% consumption** of `rawBits − 33 − P` bits plus all-printable
content, and take the **LARGEST** valid `P`. For the 1kV body this yields **P = 3798**
(`3800` is also a valid parse; `3820` — the 26-record boundary — is NOT valid):

```
[ table data: 3798 bits ][ string region: 27388 bits ][ 33-bit tail ]
    (0..3798)            (3798..31186)               (31186..31219)
```

Table data = `544-bit header + 25×126-bit records + 104-bit truncated 26th record`
(see the record section above) = 3798 bits.

- The **33-bit tail** = `32-bit value + 1 flag bit`. The 32-bit value (LE) at bit 31186
  = `1627870815` (0x6129978F) — NOT the string-region size 27388, so the "size field = S"
  assumption is **disconfirmed** (likely a hash/checksum). The last bit (31218) = `0`.
- The L302Dump "body end" figure differs from 3798 by a 2-bit `handleStart` offset
  (a known sensitivity of that extraction path) — the largest-P consumption rule is the
  anchor, not the L302Dump figure.

**String region (verified):** 126 strings (including a leading empty string), each
`[BS len][len×2 bits]`, MSB-first, 16-bit chars big-endian, with the **characters in
reversed order** in the bit stream (the first non-empty string decodes to `"elbaT
kcolB"` = `"Block Table"` reversed). 100% region consumption at P = 3798.

### CORRECTION: 126-bit record — rawest verification (TBits2), the truncated 26th record, cross-BPT scan
The record layout above is now verified by the **rawest possible read** (`TBits2`: direct
`(raw[b/8] >> (7 - b%7)) & 1` byte-level bit extraction, no reader class, no multi-byte
shifts — superseding every earlier tool). One correction: bits 120..125 = **`001010`**
(1s at 122 and 124), not `000010` (the TFinal-era figure was a shift-wrap artifact).

- The 7 variable bits are confirmed at **70 and 97..102**; constant 1-bits at
  69, 72, 75, 84, 91, 93, 104, 112, 114, 122, 124. k0 = `02 94 00 00 00 00 00 00 04 88 08 0a
  38 80 a0 28` + `001010` (raw bytes 68..83 = `02 94 00×6 04 88 08 0a 38 80 a0 28`, raw[84]
  = `0a` already belongs to record 1).
- Per-record 7-bit indices (bit70 = 1 only for k3..k5): **28, 29, 53, 118, 119, 94,
  31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49** — i.e. a
  run of 19 consecutive indices (31..49) plus four out-of-run values (28, 29, 53, 94, 118, 119).
  All fall inside the string region's 26 four-string groups (bits 22..125): the 126
  strings = 22 leading (table name `"Block Table"`, `"Block Table1"`, 4× the column-type
  names `UserVariable`/`Custom`/`UpdatedDistance`/`VisibilityState`, empties) + 26 × 4
  per-group strings (full name, short name, category, direction).
- **The 26th record is truncated to 104 bits** (bits 3694..3797 = `02 94 00×6 04 88 08 0a 70`):
  its first 104 bits give index **56** (`000001` + `111000` = 56 = the `"Dubbelzijdig"`
  string of group 14), and its last 22 bits are simply not stored. The body is 22 bits
  short of 26 complete records (31219 + 22 = 31241 = 544 + 26×126 + S + 33): the writer
  cut the stream after the 13th byte of the 26th record. The body is NOT byte-padded at
  the end (31219 = 3902×8 + 3: a 33-bit tail, value 0x6129978F + flag 0, precedes 5 pad
  bits in the file).
- **The 0-start period hypothesis is disconfirmed** (`THead0`): 126-bit windows at
  0, 126, 252, 378, 504, 630, 756, 882 share no template (w0 alone varies 110 of 126
  bits). The 544-bit header region is a genuine separate bit-packed struct; the record
  period begins exactly at 544.

**Cross-BPT scan (TScan / TApply, all candidate bodies):** the `02 94 00×6` 126-bit record
model is **not shared** by the other BPT bodies — the record model appears to be
per-BPT (at least per-AutoCAD-version) in its constant 119-bit template:

| body | rawBits | leading 68 bytes (first) | 126-bit `02 94 00×6` markers |
| --- | --- | --- | --- |
| 1kV BPT | 31219 | `80 00 00 00 00 00 05 0c 00 21 57 be …` | **26 (truncated)** at 544, 670, …, 3694 |
| l302 BPT#1 | 2596 | `aa a4 29 02 40 a3 ff fd 05 3c 76 0f 1d 86 47 ff fa …` | none |
| l302 BPT#2 | 6533 | `8e 0c cc cc cc cc c0 c4 00×7 44 f6 a5 ed 02 40 e3 ff fd …` | 16 loose `XX 94 00×6` hits (byte 0 = `0x80`), at 464, 864, 1016, … with **400/400/152-bit spacing** (no 126 period — 58 varying bits in a 126-window) |
| l302 BPT#3 | 3536 | `aa a4 69 02 40 a3 ff fd 1d 3c 76 10 16 47 ff fa …` | none |
| TEE3000 / DYN--TEE--3000--1 | 12972 | `c6 f6 36 b2 05 46 16 26 c6 50 04 65 68 a8 a8 aa aa aa aa aa da bf a9 3e 40 90 d4 26 c6 f6 36 b2 05 46 16 26 c6 53 10 09 07 8f ff f9 0d` then **inline ASCII** `UserVariable\0`, `Cust…` | none — a different schema with **inline (non-trailing) strings**; a 9-byte `c6 f6 36 b2 05 46 16 26 c6` prefix repeats at 28-byte spacing |
| GRIP objects | 107 | `41 11 c0 1c 80 1a 40 1c 00 12 80 38 22 20 …` | none — a small fixed layout, identical across all 4 GRIP objects found |

Shared motifs across the 1kV and l302 BPT#2 bodies (`ff fd`, `3c 76`, `8f ff`, `f1 d8`,
the `02 40 …` region) indicate a common bit-packed struct family with per-instance
fields; the l302 BPT#2 `80 94` region is the closest analog of the 1kV record but with
a 400/152-bit record cadence and a different constant template (byte 8 = `00`/`08`
instead of `04`, byte 9 = `08` instead of `88`).

**Open:** the 544-bit header (bit 0 = 1, bits 1..36 = 0, then a sparse→dense
bitstream; no `[BS]` sequence parses from bit 0 or bit 1 — the walk yields
12288/21999/negative values). Candidate models: a large bit-packed metadata struct
(row/column counts, per-column type + name index — 1kV has 26 rows × 6 column-type
names in its string pool), or a second record family with a 544-bit period.

## Breakthrough (preliminary): the 544-bit header = 32×17 = 11×48+16 — a stream of file offsets

Two exact factorizations of 544 both close, and the file-offset interpretation is now
supported by direct evidence.

### The 48-bit (6-byte) decomposition — exact

Grouping the 68 header bytes into 6-byte (48-bit) fields gives **11 × 48 + 16 = 544
exactly** (the earlier `F8 = 0xD840…?` uncertainty is resolved: byte 47 = `0xEB`, so
`F8 = 0xD840591FFFEB`). Splitting each 48-bit field into three 16-bit chunks:

| field | 48-bit | [16·16·16] (decimal) |
| --- | --- | --- |
| F1  | `0x800000000000` | 32768 · 0 · 0 |
| F2  | `0x050C002157BE` | 1292 · 33 · 22462 |
| F3  | `0x2A69899102A5` | 10857 · 35217 · 677 |
| F4  | `0x61024163FFFD` | 24834 · 16739 · 65533 |
| F5  | `0x8E3C76101647` | 36412 · 30224 · 5703 |
| F6  | `0xFFFB1E78EC20` | 65531 · 7800 · 60448 |
| F7  | `0x2C8FFFF640F1` | 11407 · 65510 · 16625 |
| F8  | `0xD840591FFFEB` | 55360 · 22815 · 65515 |
| F9  | `0xF1E3B078EC32` | 61923 · 45176 · 60466 |
| F10 | `0x3FFFD143C760` | 16383 · 53571 · 50944 |
| F11 | `0xF1D862348DA0` | 61912 · 25140 · 36256 |
| tail| `0x280A` (16-bit)      | 10250 |

7 of the 11 leading 16-bit chunks have bit 15 set (32768, 35217, 36412, 65531, 55360,
61923, 61912) → the header is **not** a run of signed 16-bit `[BS]` fields on a 48-bit
grid. (Matches the prior ODA-walk result: 6+ of the 16-bit values carry bit 15, valid
only as *unsigned* 16-bit or 33-bit.)

### The `[BS]` walk from bit 0 — 23 fields + 9-bit remainder = 544 exactly

Parsing the 544-bit header as an ODA `[BS]` stream (tag 0 → 16-bit, tag 1 → 33-bit)
from bit 0 gives **23 complete fields** (14 × 17-bit + 9 × 33-bit) that end at bit
534 (= 535 bits), followed by a **9-bit remainder** (value `0x0A` = 10):
**535 + 9 = 544 exactly.** A 24th `[BS]` field starts at bit 535 and straddles into
record 0 (value 2562) — so the header is *not* a run of 24 complete `[BS]` fields
(walking it naïvely gives 24 fields summing to 552 bits, 8 bits over); the true framing
is 23 fields + 9 trailing bits. The 32×17 factorization is a near-miss, not the true
structure.

**The 2-bit-tag (AutoCAD `ReadBitShort`) hypothesis is disconfirmed:** parsing the header
with AutoCAD's 2-bit-tag encoding (`00`→2-byte LE, `01`→1 byte, `10`→0, `11`→256)
from bit 0 yields the `12288`/`21999` values the prior session observed, then a bogus
run of **seven `256`** (tag `11`) values at bits 238..252 and 364..368 — not a coherent
structure. The 1-bit `[BS]` walk is the better parse.

The 23 fields (bit position, width, value; the 9 32-bit payloads shown as
`[hi16 · lo16]`):

The 24 fields (bit position, width, value; the 9 32-bit payloads shown as
`[hi16 · lo16]`):

| # | bit | width | value |
| --- | --- | --- | --- |
| 1 | 0 | 33 | 0 |
| 2 | 33 | 16 | 0 |
| 3 | 50 | 16 | 10336 |
| 4 | 67 | 16 | 533 |
| 5 | 84 | 16 | 63429 |
| 6 | 101 | 16 | 39522 |
| 7 | 118 | 16 | 51329 |
| 8 | 135 | 16 | 42337 |
| 9 | 152 | 16 | 1154 |
| 10 | 169 | 33 | `0x8FFFF638` = [36863 · 63032] |
| 11 | 202 | 33 | `0xE3B080B2` = [58288 · 32946] |
| 12 | 235 | 16 | 32767 |
| 13 | 252 | 33 | `0x63CF1D84` = [25551 · 7556] |
| 14 | 285 | 16 | 2851 |
| 15 | 302 | 33 | `0xFFF62078` = [65510 · 8312] |
| 16 | 335 | 33 | `0xD840591F` = [55360 · 22815] |
| 17 | 368 | 33 | `0xFFD7E3C7` = [65511 · 58311] |
| 18 | 401 | 16 | 49635 |
| 19 | 418 | 33 | `0x6191FFFE` = [24977 · 65534] |
| 20 | 451 | 33 | `0x143C760F` = [5180 · 30223] |
| 21 | 484 | 16 | 15116 |
| 22 | 501 | 16 | 36131 |
| 23 | 518 | 16 | 53268 |
| — | 535 | 9 | `0x0A` = 10 (9-bit remainder; a 24th 16-bit field would read 2562 and straddle into record 0) |

The 32-bit `[BS]` payloads **each split into two 16-bit quantities that are both
in-range file offsets** (≤ 65534 < 74687) — e.g. field 16 = `0xD840591F` is the exact
leading 4 bytes of 48-bit field F8 above.

### The 16-bit values are file offsets (the key new evidence)

The 1kV DWG is **74687 bytes** long; a 16-bit offset spans 0..65535, so every 16-bit
value above is a *valid in-range file offset*. Probing the 23 field targets:

- **offset 36131** (field 22) → `00 6b 00 52 00 6f 00 75 00 6e 00 64 00 54 …` = UTF-16BE
  `"kRoundTripPu…"` — a real readable symbol in the file.
- **offset 2562** (field 24) → `74 00 20 00 73 00 61 00 76 00 65 00 64 00 20 00 62 00 79 …`
  = UTF-16BE `"…t saved by a…"` — a version/“last saved by” banner fragment.
- The other targets point to binary / non-printable data.

So the 16-bit values behave as **file offsets** (at least the two that hit readable
UTF-16BE strings), but they are *not* a uniform string table — most targets are binary.
The leading `[BS] 0` (field 1, 33-bit) + `[BS] 0` (field 2, 16-bit) read as two leading
null/version fields, after which the stream is a mix of 16-bit and 32-bit (two packed
16-bit) file offsets.

### Status

- **Confirmed (exact):** 544 = 32×17 = 11×48+16; the 6-byte/48-bit decomposition and the
  16-bit chunk table above; the 1-bit `[BS]` walk = 23 complete fields (ending at bit
  534 = 535 bits) + 9-bit remainder = 544 exactly.
- **Disconfirmed (this session):** the 2-bit-tag (AutoCAD `ReadBitShort`) walk — yields
  `12288`/`21999` then a bogus run of seven `256` values.
- **Supported (preliminary):** the 16-bit header values are in-range file offsets into
  the 74687-byte DWG; two hit readable UTF-16BE strings (`"kRoundTripPu…"`, `"…t saved
  by a…"`); the nine 32-bit `[BS]` payloads each = two packed 16-bit in-range offsets.
- **Open:** (a) the semantic meaning of the 23 `[BS]` fields + 9-bit remainder (the 9-bit
  value 10; the leading `0`/`0` pair); (b) what the 32-bit dual-offset fields reference
  (two related strings? a name + its category?); (c) whether the offset list is the BPT's
  own row/column metadata (26 rows × 4-name groups) or a global file string-index table.

### Offset resolution (preliminary): the 16-bit values point at global file structures, not the BPT's strings

Resolving all 30 distinct 16-bit values as file offsets into the 74687-byte DWG and
dumping the target bytes:

| target type | offsets |
| --- | --- |
| file header / version marker (`AC1032`) | 0 |
| global C++ symbol (`kRoundTripPurgePrevent…`, UTF-16BE) | 36131 |
| CJK characters | 8312, 15116, 32767 |
| Thai characters | 42337 |
| small-value tables (repeating 2-byte period) | 533, 1154, 15116, 2851, 39522, 42337, 51329 |
| opaque binary | the remainder (22815, 24977, 25551, 30223, 32946, 36863, 49635, 53268, 55360, 58288, 58311, 63032, 63429, 65510, 65511, 65534, 10336, 7556) |

**Key negative (preliminary):** *no* header offset points at the BPT's own 126 strings
(`UserVariable`, `Custom`, `UpdatedDistance`, `VisibilityState`, `1kV Keet`, `Klassiek`,
`Dubbelzijdig`, …). Those strings are **not present in the file as 8-bit, UTF-16LE, or
UTF-16BE** — they are decoded by the extraction tool from the object's **binary**
on-disk region (the "string region" bytes, e.g. `90 b4 20 06 c0 06 f0 06 …`, are
bit-packed, not text). So:

- The 544-bit header is **not** a pointer table into the BPT's string pool.
- The 16-bit values are valid file offsets, but they reference **global file structures**
  (the file header, a `kRoundTripPurgePrevent` symbol) and **character/localization
  tables**, not the BPT's own data.
- The BPT's actual strings live in a **separately-encoded** (binary/bit-packed) region of
  the object that a specialized decoder resolves — the exact string encoding is still open.

## BREAKTHROUGH (confirmed): the full 1kV BPT body layout — the string region is `[ReadBitShort length][UTF-16LE]`

The string-region encoding is now **cracked and confirmed**. Decoding the region starting
at bit **3798** with the AutoCAD `[ReadBitShort]` length (2-bit tag: `00`→16-bit,
`01`→8-bit, `10`→0, `11`→256) followed by `length·2` **UTF-16LE** bytes yields **126
clean printable-ASCII strings consuming exactly 27388 of 27388 bits** (`exact = true`).

The encoding of each string:
- **`[ReadBitShort length]`** — a 2-bit-tag value (`00`→16-bit, `01`→8-bit, `10`→0,
  `11`→256). All 126 1kV lengths are small, so they use tag `01` (8-bit) or `10` (0).
- **`length·2` bytes as UTF-16LE** — 2 bytes/char; ASCII strings have high byte `0x00`.

### Confirmation (decoding from candidate string-start P; region = bits P..31185)

| P | region bits | result |
| --- | --- | --- |
| **3798** | 27388 | **126 strings, all printable, exact bit consumption** ✓ |
| 3800 | 27386 | 125 strings (skips the leading empty string — artifact) |
| 3820 | 27366 | **fails (EOF)** — the prior "whole 26th record" correction is wrong |
| 3790 / 3810 / 3830 / 3850 | — | fail (non-printable) |

### The complete 1kV BPT body (31219 bits, AC1032)

| region | bits | content |
| --- | --- | --- |
| header | 544 | table of 16-bit/32-bit **global file offsets** (characterized above) |
| records | 3254 | **25 whole 126-bit records + a 104-bit (22-short) 26th record** |
| string region | 27388 | **126 strings**, each `[ReadBitShort length][length·2 UTF-16LE]` (confirmed) |
| tail | 33 | 32-bit LE `0x6129978F` + flag `0` |
| **total** | **31219** | 544 + 3254 + 27388 + 33 |

**Corrections to the prior model:**
- The 26th record is **truncated to 104 bits** (bits 3694..3797; 7-bit index **56** from
  its first 104 bits). The body is exactly **22 bits short** of 26 complete records
  (31219 + 22 = 31241 = 544 + 26×126 + 27388 + 33). The prior "correction" that the
  26th record is whole (P = 3820) is **disconfirmed** — decoding from 3820 fails (EOF);
  the 126-bit window at 3694..3819 was a misalignment across the record/string boundary.
- The string region is **27388 bits** (P = 3798), not 27366 (P = 3820).
- The string encoding is **`[ReadBitShort length][UTF-16LE]`** — not 2-bit chars, not a
  1-bit `[BS]` stream, not a length-prefixed raw-byte stream. (This resolves the long
  "string-region encoding unknown" open question.)

### Full validation: the 26 records index the string pool (the model is self-consistent)

Decoding all 26 records (25 whole + the 104-bit 26th) with the 126-bit template and
resolving each 7-bit index (`bit70 << 6 | 6-bit[97..102]`) into the 126-string pool:

| k | bits | const match | index | → string |
| --- | --- | --- | --- | --- |
| 0 | 126 | 70/70 | 28 | `Rechts` |
| 1 | 126 | 70/70 | 29 | `1kV Keet - VPR` |
| 2 | 126 | 70/70 | 53 | `1kV Keet aanduiding omvormers - VPR` |
| 3 | 126 | 69/70 | 118 | `IL Shelter` |
| 4 | 126 | 69/70 | 119 | `Double Wide` |
| 5 | 126 | 69/70 | 94 | `IL Shelter` |
| 6..24 | 126 | 70/70 | 31..49 | `Klassiek`, `Links`, `1kV Keet - VPL`, `1kV Keet`, `Dubbelzijdig`, `Uitbreiding - 1kV keet`, … |
| 25 | 104 | 48/48 | 56 | `Dubbelzijdig` |

- **Every index is valid** (`< 126`) and resolves to a meaningful Dutch electrical term.
- **Bit 70 refinement:** the 64..71 field is `0x04` **except bit 70**, which is the
  index's high bit. Records with index ≥ 64 (k3/k4/k5 = 118/119/94) have bit 70 = 1 and
  show 69/70 (the one mismatch is bit 70); records with index < 64 show 70/70. So bit 70
  is **variable** (the index's top bit), not constant.
- **The 26th record (104 bits, index 56)** matches its first 104 template bits (48/48);
  it is the last record and is 22 bits short.

**The model is self-consistent and fully validated** for the 1kV file: a header of global
file offsets, 26 records each holding a 7-bit index into a 126-entry UTF-16LE string
pool (a string-interning table), and a 33-bit tail.

**Open:** why the writer emitted the 26th record 22 bits short (a writer bug, a
variable-length record, or a count/length field elsewhere); and whether the 544-bit
header's 16-bit global offsets are shared across BPTs in the file.

### Cross-validation (preliminary): the string pool is general; the record/header schema is 1kV-specific

Applying the cracked 1kV model to the other BPTs (l302 obj1/obj3/obj5, TEE3000 obj1 —
full `RawTail` dumps, `[ReadBitShort][UTF-16LE]` string-start scan):

| file | body bits | string pool | record region | 1kV model |
| --- | --- | --- | --- | --- |
| 1kV | 31219 | **126 strings** (P=3798) | 3254 = 25×126 + 104 | ✓ (544 header + 126-bit records) |
| l302 obj1 | 2596 | **10 strings** (P=1663) | 1663 (≡25 mod 126) | ✗ |
| l302 obj3 | 6533 | **16 strings** (P=5388) | 5388 (≡96 mod 126) | ✗ |
| l302 obj5 | 3536 | **21 strings** (P=1149) | 1149 (≡15 mod 126) | ✗ |
| TEE3000 | 12972 | **none** (inline) | — | ✗ |

**Findings (preliminary):**
- The **`[ReadBitShort length][UTF-16LE]` string-pool encoding is general** across BPTs:
  it decodes with exact bit consumption for 1kV (126) and all three l302 BPTs (10/16/21).
  Every l302 pool begins with the same BPT "scaffold" strings 1kV has — `Block Table`,
  `Block Table1`, `UserVariable`, `Custom`, `UpdatedDistance(X)` — plus block-specific
  names (l302 obj5: `1 Space`, `2 Spaces`, `3 Spaces`).
- The **1kV record/header schema is specific to 1kV**: no l302 record region is a
  multiple of 126 (remainders 25/96/15), and none is `544 + N×126`. So the 544-bit
  header + 126-bit-record + 7-bit-index structure is a 1kV (or one block family's)
  variant, not the universal BPT record format.
- **TEE3000 has a different schema entirely** — no trailing string pool (the
  `[ReadBitShort][UTF-16LE]` scan finds at most 1 empty string; its strings are inline,
  at 28-byte spacing, with a 9-byte `c6 f6 36 b2 05 46 16 26 c6` prefix).

**Implication:** the BPT string pool (interned UTF-16LE strings with `[ReadBitShort]`
lengths) is a shared mechanism; the record/header that references it varies by block
type. A general BPT reader must treat the record region as schema-specific, while the
string-pool decode is uniform.

### CORRECTION (preliminary, **superseded**): the 2-bit string-region model is disconfirmed; the 26th record is whole

> **Superseded** by the [confirmed breakthrough](#breakthrough-confirmed-the-full-1kV-bpt-body-layout--the-string-region-is-readbitshort-lengthutf-16le)
> below this point: the 26th record is **truncated to 104 bits** (P = 3798), not whole
> (P = 3820); decoding from 3820 fails (EOF).

Re-deriving the string-region arithmetic from the verified 126-string content (1639 chars
total) kills the prior "100% consumption at 2-bit chars" claim:

- 126 strings = 1639 chars → **3278 char bits + 252 tag bits = 3530 bits**, but the
  string region is **27366 bits** (P = 3820) / 27388 bits (P = 3798) — an **87% gap**.
  No integer (T, C) solves `27388 = 126·T + 1639·C` for C ∈ {2, 8, 16}: the strings are
  *not* 2-bit/8-bit/16-bit-chars-from-3798.
- A `[BS]`-len + 16-bit-char walk from bit 3820 (and from 3798) **parses 0 strings**
  (fails at the first field: the 33-bit value 0x8801B001 is in the signed-[BS] dead
  zone; unsigned it = 4162201057, not a plausible length). The region is **not** a
  `[BS]` string stream at either candidate boundary.
- **The 26th record is a whole 126-bit record** (bits 3694..3819 = `02 94 00×6 04 88 08 0a
  70 9C 24 2D 08 9C…`): 6-bit field 97..102 = `111000` = 56, bit 70 = 0 → index **56**
  (the `Dubbelzijdig` string of group 14, matching the earlier truncated-view index).
  The prior "truncated to 104 bits" was an artifact of the P = 3798 boundary; **P = 3820
  (544 + 26×126) is the more likely table/string split** — the body is *not* 22 bits
  short of 26 complete records.
- Record 26's trailing 22 bits (104..125 = `10011100 00100100 001011` = 0x1C242B =
  1860299 as 22-bit) **deviate from the 25-record template** (`10000000 10100000 001010`
  = 0x00A02A = 40914) at bits 107, 108, 109, 112, 117, 125 (6 of 22) — so bits 104..125
  are *not* constant across the 26 records; they carry per-record data the 25-record
  sample could not expose (candidate: a 15-bit offset + 6-bit string index — the 22-bit
  values 40914/1860299 are both valid 22-bit quantities; semantics unverified).
- The `02 94` record markers appear only **8 times** in the full 31219-bit body (bits
  544, 1048, 1552, 2056, 2520, 2560, 3064, 3568) = record starts k ∈ {0, 4, 8, 12, 16,
  20, 24} (only the byte-aligned ones, since 126k ≡ 0 mod 8 ⇔ k ≡ 0 mod 4) + one
  anomaly at 2520 (40 bits before the k=16 start). Consistent with the 26-record model.
- `02 9X` (90..9F) appears 13 times; other BPT bodies (l302) share none of the 1kV
  record-template bits (3-way per-bit diff: 1106 runs, 553 constant — no shared
  template), so cross-BPT diffing is a dead end for the header.

## Now implemented: the decoder in the library + the `tools/bpt-decode` tool

The cracked layout above is now **implemented** (no longer just forensics):

- **`ACadSharp.Objects.Evaluations.BptBodyDecoder`** — a static, self-contained decoder
  (nullable-disabled, so it lifts cleanly into the library). `Decode(data, bitCount)`
  splits a BPT body into the 544-bit header, the records (each a 7-bit string-pool
  index `bit 70 << 6 | 6-bit[97..102]`), the string pool, and the 33-bit tail. The pool
  start is found by **search** (scan candidate starts; keep the one whose
  `[ReadBitShort][UTF-16LE]` stream consumes the pool exactly with every string
  printable, preferring the most strings — this distinguishes the true start P=3798
  from the off-by-a-bit artifact P=3800). `RecordSchemaMatched` reports whether the 1kV
  record schema matched; when it did **not**, `RecordIndices` is left empty (the pool
  still decodes).
- **`BlockPropertiesTable`** exposes the decoded form lazily: `Strings` (the pool),
  `RecordIndices`, `StringPoolStart`, and `RecordSchemaMatched`. The raw payload is
  still preserved verbatim (`RawTail`/`RawTailBitCount`) for lossless round-trips.
- **`tools/bpt-decode`** — a reusable, self-documenting tool (see its `README.md`):
  loads a DWG, decodes every BPT's `RawTail` with the library's decoder, and prints the
  body layout + the record → string resolutions.
- **`BlockPropertiesTableTests`** locks in the decode against real samples: the 1kV body
  (126 strings, 26 record indices, `StringPoolStart` 3798, schema matched) and the L3-02
  bodies (10/16/21 strings, schema **not** matched → empty indices). The sample DWGs
  live in the gitignored `samples/bpt-forensics/` (kept out of `samples/dynamic-blocks/`
  so the `DynamicBlockTests` corpus is not polluted).

**Verified numbers** (all reproduced by the C# decoder, not just the Python forensics):
1kV = 31219 bits → header 544 + 3254 records + 27388 pool + 33 tail = 126 strings,
26 indices `28, 29, 53, 118, 119, 94, 31…49, 56`, P=3798, schema matched. L3-02 =
2596/6533/3536 bits → 10/16/21 strings, schema not matched. TEE3000 = 12972 bits → no
trailing pool (inline strings), `Decode` returns null.

## The L3-02 record schema (preliminary): a different, per-family layout

Cracking the L3-02 record schema (the pre-pool region, which the 1kV model does not
explain) shows it is a **genuinely different layout** from the 1kV's 544-bit-header +
126-bit-record model. The L3-02 file has three BPTs (2596 / 6533 / 3536 bits; 10 / 16 /
21 strings) whose pre-pool (record) regions are 1663 / 5388 / 1149 bits — none a
multiple of 126, none `544 + N×126`, and 1663 is prime (so the region is not
`N × R` for a single record size `R`).

Findings (preliminary):

- **The header has a shared field layout across families.** Both L3-02 bodies (A: 10
  strings, C: 21 strings) share an identical 12-byte prefix shape
  (`aa a4 XX 02 | 40 a3 ff fd | XX 3c 76 XX`), with only a few differing bytes (the
  `02`/`3c`/`76` bytes are constant, a few bytes vary per BPT). The 1kV header carries
  the same `ff fd` + `3c 76` pair, shifted to bytes 14–19. So the `…ff fd YY 3c 76 ZZ`
  substructure is a recurring field (candidate: a tagged pointer / sentinel entry) that
  appears in every family's header, at a family-specific position.
- **One L3-02 body (C, 21 strings) has a clean 96-bit entry structure.** After a
  ~256-bit header, the region is **9 × 96-bit entries**. Aligning the 9 entries
  reveals a template: **81 constant bits, 15 variable bits** in three fields:
  - **bits 24–27 (4-bit) = the entry's own counter, 1…9** (the high 4 bits of byte3,
    which is `0x10·k + 4`, k = 1…9) — so this is the row index, **not** a string index.
  - **bits 90–92 (3-bit) = a small value** 0, 2, 0, 6, 0, 4, 2, 3, 7.
  - **bits 2–9 (8-bit) = a code** 91, 107, 117, 123, 128, 140, 135, 225, 254 (larger than
    the 21-string pool's 0…20 range).
  - The middle (`0x05280000` and the surrounding constant bits) is a constant
    type/sentinel block shared by all 9 entries.
  - **None of the three variable fields is a direct index into the 21-string pool**, so
    the record→string link is **indirect / multi-level** (not a single index field).

**The indirect record→string mechanism (VERIFIED in C#, `verify_l302`):** each 96-bit
entry references **two** pool strings — a *label* and a *value*:

- **`byte3 = (k << 4) | 4`** (k = 1…10, the entry's own index). The **high 4 bits are a
  direct pool index → the label** `pool[k]`: `Block Table`, `Block Table1`,
  `UserVariable`, `Custom`, `UpdatedDistanceX`, or empty (the empty-pool slots).
- **`byte11` is a count N** (0…3 observed), and **the pool index is `10 + N`** → the
  **value**, an *offset-based* index into the "N Spaces" group at `pool[10..20]`
  (empty, "1 Space", "2 Spaces", … "10 Spaces"). The record stores the **count**, not
  the index — the group base (`10`) is implicit in the block's "value" type.

So the (label, value) pairs decode coherently (C body): `(Block Table, —)`,
`(Block Table1, 1 Space)`, `(UserVariable, 1 Space)`, `(Custom, 2 Spaces)`,
`(UpdatedDistanceX, 3 Spaces)`, plus value-only entries. **The "value" link is genuinely
indirect (offset/count-based), while the "label" link is a direct pool index** — a mix of
the two. The **10th entry is truncated (29 bits)** — `byte3 = 0xa4` (index 10), `byte0 =
0x24` (the counter continues 35 → 36) — the same "last entry truncated" pattern as the
1kV's 26th record. The group base (`10`) is specific to this body's "value" strings; a
different block with a different "value" group would use a different base.
- **The other L3-02 bodies (A: 10 strings, B: 16 strings) use a sparser layout**
  (many-zero 32-bit values, e.g. `0038404a 00000000 00000284 …`), so even within L3-02
  the record layout varies per BPT (likely by the block's property-table shape).

**Conclusion:** the BPT record layout is **per-block-family** (and even **per-BPT within
a family** — the L3-02 file's three bodies use three different record layouts); only the
trailing `[ReadBitShort][UTF-16LE]` string pool is uniform. There is **no single L3-02
schema**: one body uses 96-bit entries (a 4-bit row counter, a 3-bit field, an 8-bit code,
with an *indirect* record→string link), the others use sparser, different layouts. So a
general reader must (1) **always decode the pool** (uniform) and (2) **treat the record
region as schema-specific** (1kV = 126-bit records, the only fully-cracked schema). The
1kV `RecordSchemaMatched` guard in `BptBodyDecoder` is the correct general behavior: it
exposes `RecordIndices` only when the 1kV schema matched, and leaves them empty for other
families (the pool still decodes for all).

**The BPT's role (research — how a block references/uses it):** the
`BlockPropertiesTable` is referenced **only by the IO layer** (the DWG/DXF reader/writer +
templates) — **not by the `BlockLookupAction`, the `EvalGraph`, or any block** (verified:
a grep for `BlockPropertiesTable` outside its own file + tests finds only the IO layer +
the companion `BlockPropertiesTableGrip`). The **lookup mechanism** is the
`BlockLookupAction` (a `BlockAction`), which is **self-contained**: its table data lives
inline in the action object (`Columns` → `ColumnData.Rows`, loaded by
`CadBlockLookupActionTemplate` from the action's own `RowValues`), and its `Evaluate` does
a classic table lookup (match the input columns' values → read the matched output cells).
It does **not** read from a `BlockPropertiesTable`. The `BlockPropertiesTable` +
`BlockPropertiesTableGrip` are a **pair** described as "a data object that supports the
dynamic block properties table (**a UI feature for displaying block properties**)," both
data-only `EvaluationExpression`s that evaluate to `None`. So the BPT is a **display/UI
table** (the "properties table" + its grip), **not a computation table** — the engine
does not consume it. The decoded BPT body (the `Strings` + `RecordIndices`) is therefore a
**dead end for the evaluation engine**: it is read + written (lossless round-trip) but not
used for evaluation. Its practical value is **diagnostic** (a human-readable "properties
table") and **lossless round-trip** (the `RawTail` is preserved verbatim).
