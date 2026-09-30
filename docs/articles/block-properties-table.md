# Block Properties Table (BPT) — format research

> **Status:** the on-disk layout is **decoded and implemented** for the block-element
> prefix (table + grip) and the grip's full field set. The **table's column/row/cell
> body** was **cracked this pass**: it is a standard R2010+ object whose **inline string
> region** the library's own `SetPositionByFlag` + `ReadVariableText` reads (the earlier
> "opaque blob / 0xCC encoding" claim was a misaligned read — see the
> [breakthrough section](#breakthrough-the-body-is-a-standard-r2010-object--the-opaque-blob-was-a-misaligned-read)).
> **Still open:** wiring the string region into the reader/writer and mapping the
> extracted strings to the `Columns` / `Rows` / `Cells` model. The decoded fields are
> verified against `L3-02-Dynamic Blocks.dwg` (all six objects) and covered by
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

  **Open:** the per-record field layout (which 96 bits are the cell values, the type
  codes, the variant payload) and the header fields (the column count, the per-column
  `Constant` / `Editable` / `Removable` / `DefaultValue` / `UnmatchedValue` /
  `CustomProperties` / `Parameter` / `Format`, then `DefaultActiveRowIndex`, `MustMatch`,
  `ContainsRuntimeParametersOnly`). The record *period* and *shape* are established; the
  *fields within a record* and the *header fields* are the next thing to pin down.

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
 4. **Decode the table body's records + header.** The body is isolated, its **96-bit
    record period** is established, and the body is now split into a **variable-size
    header** (the column definitions — ≡ 33 / 20 / 95 mod 96) + **N fixed 96-bit records**
    (the rows) (see the [body section](#the-table-body-isolated-sized-and-structured)).
    The next step is the **per-record field layout** (cell values, type codes, variant
    payload) and the **header fields** (column count, the per-column
    `Constant` / `Editable` / `Removable` / `DefaultValue` / `UnmatchedValue` /
    `CustomProperties` / `Parameter` / `Format`, then `DefaultActiveRowIndex`, `MustMatch`,
    `ContainsRuntimeParametersOnly`).
