# Block Properties Table (BPT) — format research

> **Status:** the on-disk layout is **decoded and implemented** for the block-element
> prefix (table + grip) and the grip's full field set; the **table's column/row/cell
> body** and the **grip's constant 91-bit tail** are **still open** (preserved verbatim in
> `RawTail` for lossless round-trip). The decoded fields are verified against
> `L3-02-Dynamic Blocks.dwg` (all six objects) and covered by
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
   `handleStart − 17` (grip #1 = `0x4A00 = 18944`). That value is **not an inline
   size** — interpreted as a size it would place the string data *before* the object's
   own start (`strStart < dataStart`), which is impossible for inline data — so it is a
   **pointer into a separate string stream**. The string **data** (the grip's `name`) is
   therefore *not* in the object bytes; it lives in the separate string stream the
   pointer references.

The L3-02 string stream holds `"Block Table"`, `"Block Table1"`, `""` (the three
tables) and `"Grip"` (+ more) for the grips — the names this metadata points at.

> *Remaining:* the exact field type(s) of the ~74–90-bit constant part are still not
> pinned down (a greedy `BL, BL, …` decode yields arbitrary values), and the 16-bit
> pointer's encoding (a stream offset? a string-table index?) is unconfirmed. The
> pointer differs per grip (each `name` points at a different string), which is why the
> three tails differ only in the last ~12 bits.

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

**Still open:**
1. **Pin down the 91-bit grip gap's constant part** — now understood as a constant
   field (≈ 74–90 bits, identical across all three grips) followed by the 17-bit
   text-region metadata (a flag = 1 + a 16-bit pointer, e.g. `0x4A00 = 18944`, into the
   separate string stream). The exact field type(s) of the constant part and the
   pointer's encoding (stream offset? string-table index?) are still unconfirmed.
2. **Crack the table's column/row/cell body** (LibreDWG's struct is empty); confirm
   against the string stream's strings (`"Block Table"`, `"Block Table1"`, `"Grip"`, …).
   A fresh pass shows the three tables' bodies are **not** a single repeated unit:
   table #1 and #3 are structurally similar (differ in only a few bytes) while #2
   differs substantially, and none of the bodies is byte-aligned (they follow the
   82-bit expression, which ends mid-byte) — so the column/row/cell records are
   bit-packed and the per-record size must be derived from the size deltas.
