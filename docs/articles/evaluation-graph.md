# The Evaluation Graph (AcDbEvalGraph) — Format Analysis & Evaluation Plan

ACadSharp can read the AutoCAD **evaluation graph** (`ACAD_EVALUATION_GRAPH` / `AcDbEvalGraph`) that dynamic blocks store in the block record's XDictionary under the key `ACAD_ENHANCEDBLOCK`, but it cannot **evaluate** the graph. This document records the format analysis, the decoded connection model, the official ObjectARX evaluation model, the open questions, the research sources, and the plan to close the gap.

**Status:** Phase 1 (format research) is largely complete. The node/edge invariants are verified against all 10 bundled samples; the connection model, `TrackedCount`, edge flags, and the lookup-table structure are decoded and cross-verified. The official ObjectARX API has been fully captured (see the companion doc [`objectarx-eval-api.md`](objectarx-eval-api.md)).

---

## Where the object lives

```
BLOCK_RECORD
  └> 330 → DICTIONARY
        └> 360 → ACAD_EVALUATION_GRAPH   (AcDbEvalGraph)
              └> 360 (per node) → AcDbEvalExpr (the "expression" objects)
```

The same navigation in AutoLISP (from Lazebny, part 6):

```lisp
;; from a selected block reference in model/paper space:
(setq EVAL_GRAPH
  (entget (cdr
    (assoc 360
      (entget (cdr
        (assoc 360
          (entget (cdr
            (assoc 330
              (entget (car (entsel "Select required block: ")))))))))))))
```

Each graph node references one **expression** object (a dynamic-block element). The expressions are the `Block*` classes in `src/ACadSharp/Objects/Evaluations/`:

- **Parameters**: `BlockLinearParameter`, `BlockPointParameter`, `BlockXYParameter`, `BlockPolarParameter`, `BlockRotationParameter`, `BlockAlignmentParameter`, `BlockFlipParameter`, `BlockVisibilityParameter`, `BlockLookupParameter`
- **Grips**: `BlockLinearGrip`, `BlockPointGrip` (via `BlockPointParameter`), `BlockXYGrip`, `BlockPolarGrip`, `BlockRotationGrip`, `BlockFlipGrip`, `BlockLookupGrip`, `BlockVisibilityGrip`
- **Actions**: `BlockScaleAction`, `BlockMoveAction`, `BlockRotationAction`, `BlockStretchAction`, `BlockArrayAction`, `BlockLookupAction`, `BlockFlipAction`
- **Components**: `BlockGripLocationComponent` (the "UpdatedX"/"UpdatedY" of a grip)

> The ODA DWG specification and the AutoCAD 2012 DXF reference **do not document** this object at all. It is an undocumented AutoCAD internal.

---

## The official ObjectARX evaluation model

The complete, verbatim API surface for the six `AcDbEval*` classes (all in the SDK header `dbeval.h`) is captured in the companion document [`objectarx-eval-api.md`](objectarx-eval-api.md) (collected from the official Autodesk ObjectARX 2025 docs; verified byte-identical to 2024 and present in the .NET OARXMAC 2024 reference). The evaluation model in one paragraph:

1. **`AcDbEvalGraph` is a directed acyclic graph (DAG).** "If an `AcDbEvalExpr` E1 depends on (requires input from) an `AcDbEvalExpr` E2, an edge from **E2 to E1** is represented in the graph." So **edge direction = value flow**: the *target* of an edge is the element that *consumes* the value; the *source* is the element that *produces* it.
2. **`activate(activatedNodes[, pActiveSubgraph, pCycleNodes])`** — "Activates a collection of nodes … Active nodes are used as the starting point for the directed traversal of the graph during graph evaluation. If `activatedNodes` is empty, all of the nodes in the graph are deactivated. Returns `Acad::eGraphCyclesFound` if the node activation resulted in a cyclic graph." The activated set = the elements the user directly touched (a moved grip, a parameter edited in the Properties palette).
3. **`evaluate([pContext[, activatedNodes]])`** — "Evaluates the class by traversing the graph and invoking `AcDbEvalExpr::evaluate()` on all of the visited nodes." The visited set = the subgraph reachable from the activated nodes; it is **topologically sorted** to determine evaluation order. A non-successful `AcDbEvalExpr::evaluate()` **terminates the traversal**.
4. **`AcDbEvalExpr::evaluate(ctxt)`** — "Causes the expression represented by the node to be evaluated. Called for a graph-resident node when the node is visited during a call to `AcDbEvalGraph::evaluate()`. **The default implementation does nothing and returns `Acad::eOk`.**"
5. **`AcDbEvalExpr::value()` → `AcDbEvalVariant`** — "The value of the variant node. The value is usually updated during the `AcDbEvalExpr::evaluate()` call. The default value is uninitialized (`AcDbEvalVariant::Type::kNone`)."
6. **`AcDbEvalContext`** — a `key → void*` container (`getAt`/`insertAt`/`removeAt`/`newIterator`) passed unchanged to every visited node's `evaluate()`. It is the scratch space for the evaluation (e.g. an element-ID → value map).
7. **Lifecycle callbacks** on `AcDbEvalExpr`: `graphEvalStart/End/Abort(bool bNodeIsActive)`, `activated`, `addedToGraph`/`removedFromGraph`/`copiedIntoGraph`/`movedFromGraph`/`movedIntoGraph`, `adjacentEdgeAdded`/`adjacentEdgeRemoved`/`adjacentNodeRemoved`, `remappedNodeIds(AcDbEvalIdMap&)` (update cross-node references after `addGraph` renumbers IDs), `isActivatable()`, `nodeId()` (`kNullNodeId` when not graph-resident).

`AcDbEvalVariant` is a lightweight `resbuf` wrapper with typed constructors (`double → kDouble`, `AcGePoint3d → kPoint3d`, `ACHAR* → kString`, `Adesk::Int32 → kLong`, …).

**Implication for the ACadSharp evaluator (Phase 3):** mirror the API — an `EvaluationContext` (ID → value), `Activate(nodes)` + `Evaluate()` that topologically sorts the reachable subgraph and calls a per-class `Evaluate(context)` on each `Block*` (default: no-op), writing results into the context and the stored value fields. Lookup reverse-edge cycles are the one place the "DAG" assumption is relaxed (see the lookup section).

---

## Decoded format (verified against all 10 files in `samples/dynamic-blocks/`)

### Object header

| DXF code | Meaning |
|---|---|
| `96` | **max node ID** — the size of the graph's ID space, *not* the node count (e.g. the lookup sample has 96=39 but only 19 nodes, because IDs are never reused after deletion). The 2008 forum thread (Supermax, post #10) independently reports "96 and 97 are always equal … both point at the creation number (code 95) of the last property" — consistent with max-ID. |
| `97` | same value as 96 in every sample observed |
| (implicit) | edge count — not stored in DXF; the edge records simply follow the node records until the object ends. In DWG the node/edge counts are explicit fields. |

### Node record (a.k.a. the 2008 "main record", `93 = 32`)

| DXF code | Field | Meaning |
|---|---|---|
| `91` | `Index` | position of the node in the node list (0-based); records are always ordered by this (a whole-record swap is reverted by `entmod`; swapping only the 91 values swaps record contents — Supermax post #10) |
| `93` | `Flags` | `0x20` for every node in every sample; the current `NodeFlags` enum names (Visited/OutsideRefed/Selected/…) look copy-pasted from an unrelated ODA enum and are likely wrong |
| `95` | `Id` | unique expression ID — the element's **creation number**: assigned at creation, immutable (changing it → fatal error), always travels with the `360` pair; **the lookup action's `94` points exactly at it** (Supermax post #7) |
| `360` | `Expression` | handle of the `AcDbEvalExpr` this node represents |
| `92` ×4 | **`Data1–4`** | **`(firstInEdge, lastInEdge, firstOutEdge, lastOutEdge)`** — indices into the edge list, `-1` = empty list |

✅ *Verified (C# tool, all 10 samples, DXF + DWG): for every node, `Data1/2` equal the first/last of the node's actual in-edges and `Data3/4` the first/last of its out-edges.*

### Edge record (a.k.a. the 2008 "extended record", `93 = 0`)

| DXF code | Field | Meaning |
|---|---|---|
| `92` | `Index` | position of the edge in the edge list (0-based) |
| `93` | `Flags` | `0` for normal edges; **`4` for lookup (bidirectional) edges** — the 8 lookup-action edges in the lookup sample all have `93=4` |
| `94` | `TrackedCount` | **the number of "wires" the edge carries** — see the hypothesis below (decoded, verified on all 10 samples) |
| `91` | `FromNodeIndex` | source node index (the 2008 "parent" record) |
| `91` | `ToNodeIndex` | target node index (the 2008 "own" record) |
| `92` ×5 | **`Data1–5`** | **`(prevInEdge, nextInEdge, prevOutEdge, nextOutEdge, reverseEdge)`** — prev/next are the doubly-linked in/out edge lists of the from/to nodes; the 5th is a paired *reverse* edge (only set for bidirectional lookup connections, e.g. the `0↔6`, `10↔6`, `2↔6`, `15↔6` pairs in the lookup sample) |

✅ *Verified (C# tool, all 10 samples): for every edge, `Data1–4` equal the prev/next of the actual per-node in/out edge lists, and the 5th field pairs reverse edges.*

### Topology

- The graph is usually a **tree** (edges = nodes − 1): the linear/point/rotation/xy/polar/alignment/flip/visibility/basepoint samples all satisfy this.
- **Lookup** parameters add reverse-edge pairs (22 edges for 19 nodes in the lookup sample), creating cycles.
- **Direction = value flow** (matches the official ObjectARX model): `grip → parameter → component / action`. A grip's position determines a parameter's value; the parameter's value determines the updated grip positions (components) and drives the actions.

### Worked example — `BLOCKLINEARPARAMETER.dxf`

8 nodes: `Linear` parameter (0), `End Grip` (1), `UpdatedEndX/Y` (2,3), `Base Grip` (4), `UpdatedBaseX/Y` (5,6), `Scale` action (7).

```
End Grip(1) ─E2→ param(0) ─E0→ UpdatedEndX(2)
Base Grip(4) ─E5→ param(0) ─E1→ UpdatedEndY(3)
                        param(0) ─E3→ UpdatedBaseX(5)
                        param(0) ─E4→ UpdatedBaseY(6)
                        param(0) ─E6→ Scale(7)
```

- node 0 data `(2, 5, 0, 6)` = in-edges [E2, E5] → first 2, last 5; out-edges [E0, E1, E3, E4, E6] → first 0, last 6
- edge E2 data `(-1, 5, -1, -1, -1)` = first in-edge of node 0 (prev = -1), next in-edge = E5 (idx 5); only out-edge of node 1 (prev = next = -1)
- `TrackedCount`: E2/E5 = 2 (the parameter carries 2 port-connections to each grip), E0–E4 = 1, E6 = 5 (the scale action has 5 ports bound to the parameter)

---

## The connection model (decoded)

Every `Block*` element carries **connection** fields. A connection is a `(targetId, portName)` pair:

- `targetId` — the **ID** (`95` creation number) of the element this port is bound to. `0` = unbound.
- `portName` — the name of the **port on the target element** whose value is consumed (e.g. `"DisplacementX"`, `"UpdatedEndX"`, `"Scale"`, `"EndXDelta"`).

**Direction rule (verified on all 10 samples):** if element B holds a connection `(A.id, name)`, then the graph contains an edge **A → B** — value flows from A to B. Concretely:

| Holder | Connection field(s) | Target | Edge |
|---|---|---|---|
| parameter | `FirstPointDisplacementX/Y`, `SecondPointDisplacementX/Y` (and `DisplacementX/Y` for 1-pt) | a grip | `grip → parameter` |
| `BlockGripLocationComponent` | `Connection` | the parameter (or lookup parameter) | `parameter → component` |
| action (scale/move/rotation/stretch/polar-stretch/array/flip) | `ScaleConnection`, `XScaleConnection`, `YScaleConnection`, `UpdateBaseXConnection`, `UpdateBaseYConnection`, `XDeltaConnection`, `YDeltaConnection`, `AngleDeltaConnection`, `EndXDeltaConnection`, `EndYDeltaConnection`, `BaseConnection`, `EndConnection`, `UpdatedBaseConnection`, `UpdatedEndConnection`, `FlipConnection`, `UpdatedFlipConnection` | the parameter | `parameter → action` |

So the value pipeline is: **grip position → parameter value → (updated grip positions, action transforms)**. The parameter's own *output* ports (the names that components/actions bind to) include `UpdatedEndX/Y`, `UpdatedBaseX/Y`, `Scale`, `XScale`, `YScale`, `EndXDelta`, `EndYDelta`, `XDelta`, `YDelta`, `AngleDelta`, `UpdatedFlip`, …; a grip's *output* ports are `DisplacementX/Y`.

### The 170–174 connection layout (2-pt parameters)

Verified from the raw DXF of the linear and alignment samples:

```
170  : count of grip slots
91   × 170   : the grip IDs (0 = slot unused)
171  : count of FirstPointDisplacementX  connections
[92  : targetId
 301 : portName] × 171
172  : count of FirstPointDisplacementY  connections
[93  : targetId
 302 : portName] × 172
173  : count of SecondPointDisplacementX connections
[94  : targetId
 303 : portName] × 173
174  : count of SecondPointDisplacementY connections
[95  : targetId
 304 : portName] × 174
177  : base-location mode (0 = start point)
```

Example (linear sample): `170=4`, `91=[5,2,0,0]`, `171=1 (92=5, 301="DisplacementX")`, `172=1 (93=5, 302="DisplacementY")`, `173=1 (94=2, 303="DisplacementX")`, `174=1 (95=2, 304="DisplacementY")` — i.e. the first point is bound to grip 5 (X+Y) and the second point to grip 2 (X+Y).

Example (alignment sample): `170=4`, `91=[2,0,0,0]`, `171–174 = 0` — the parameter has **no port connections at all**; it is bound to grip 2 solely through the `GripIds` list. (1-pt parameters use `170`/`171` for a single `Location`/`GripId`.)

### `TrackedCount` (94) — decoded

Hypothesis, verified on all 10 samples (7 of 10 exactly; the 3 remaining explained by the fallback/column rules):

> **`TrackedCount` = the number of "wires" (parallel connection channels) between the two elements of the edge.**

Concretely:

1. **Port connections:** count the connection entries on the **TO** element whose `targetId` equals the **FROM** element's ID. (Linear E2/E5 = 2, E0–E4 = 1, E6 = 5; XY all grip edges = 2, action edge = 4; lookup E0 = 2, E6/E9 = 2, E10 = 2, component edges = 1 — all match.)
2. **Fallback — grip binding without port connections:** if the count in (1) is 0 but the two elements are still bound — via a `GripId`/`GripIds` slot pointing at the grip, or by **positional match** (the parameter's `1010`/`1011` point equals the grip's `1010` location) — the count is **1**. (Alignment E0 = 1 via `GripIds=[2,…]`; visibility E0 = 1 via positional match with `gripId=0`; lookup-parameter edges = 1 via `93` gripId.)
3. **Lookup-action edges (flags = 4):** the count is the number of **lookup-table columns** whose input (`94`) equals the FROM element's ID — i.e. how many table columns that element feeds. (Lookup sample: the linear parameter feeds 1 column, the point parameter feeds 2 — X and Y — and each lookup parameter feeds 1; total = 5 columns, matching `93=5`. The reverse edges carry the same count.)

The 2008 forum thread (Supermax post #7) reports `94` as "unknown, always 1" in his 2007-era samples — consistent with the hypothesis, since his simple blocks had exactly one wire per edge.

### "Not yet evaluated" sentinels

- `BlockGripLocationComponent` stores its evaluated value as a `40` (double) pair: **`1.797693134862314E+99`** (a large-but-finite sentinel; *not* DBL_MAX, which is `1.7976931348623157E+308`) = "not yet evaluated / at initial position". Base-position components store `0.0`. (Searched all retrieved forum sources: the sentinel value is never mentioned there — it is a modern-format observation.)
- Parameters store their **current value** in `140` (double) — e.g. the linear parameter's `140 = -6.8007…` is the label offset in the sample, and the adn-cis 2009 dump shows `140 = -13.0145` = the line's current length. `141/142/143` triples (counted by `96`) are associated value sets (e.g. displacement components).
- The per-class value fields (`140`, `70`, `1`, `1010–1032`, …) still need to be pinned down per class (open question 5).

### 98 / 99 / 1071 — sub-record tags

- `98` / `99` appear after **each** `100` subclass marker. In the modern samples `98 = 33` and `99 = 329` are constant; the 2009 dumps show `98` varying per sub-record (27 = `AcDbBlockElement` for visibility/lookup-action, 31 = `AcDbBlockElement` for the linear parameter, 25 = `AcDbBlockAction` for stretch, 8 = 2-pt-parameter section) with `99` = 1 or 8. So `98` is a per-sub-record type tag (format/version dependent) and `99` a companion tag.
- `1071` (32-bit int) — a per-record tag: `0` for actions/linear parameters, `8` for the lookup action, `16` for the visibility parameter. **Rejected by `entmod`** — stripped and regenerated.
- `90` on the expression object = the element's creation number (= the node's `95`).

### Group-code data types (per the DXF reference)

Per the [ezdxf DXF-tags reference](https://ezdxf.readthedocs.io/en/stable/dxfinternals/dxftags.html) (which mirrors the Autodesk group-code table):

| Code(s) | Value type | Used for |
|---|---|---|
| `90`–`99` | 32-bit integer | `90` creation no., `91` record/grip/state index, `92` edge index/marker, `93` record type (32/0), `94` TrackedCount/state-element-count/lookup input, `95` creation number/state-property-count, `96`/`97` header, `98`/`99` sub-record tags |
| `100` | string | subclass markers |
| `140`–`143` | double | current parameter value + value triples |
| `170`–`174` | 16-bit integer | connection-slot counts |
| `175`, `177` | 16-bit integer | flags |
| `280`–`282` | 16-bit integer | visibility flag, flags |
| `300`–`307` | string | names, labels, descriptions, state names, port names |
| `330`–`333` | soft pointer (hex object ID) | element lists (visibility parameter) |
| `360` | hard-owner (hex object ID) | node → expression object |
| `1010`–`1031` | double | points (grip/parameter/action locations) |
| `1071` | 32-bit integer | per-record tag |

Note: `91` is **overloaded** — "current state index" in `BLOCKVISIBILITYPARAMETER`, but graph-record/creation-number references in parameter bodies (e.g. the four `91` pairs after `170`).

---

## The lookup table (decoded)

`BLOCKLOOKUPACTION` (the lookup "activator") stores a table. Raw layout (from the lookup sample, 3 rows × 5 columns):

```
92  : 3            = number of rows
93  : 5            = number of columns
301 : ""           = (first column name, empty)
302 : 15 values    = the table, row-major (3 × 5)
--- then one definition per column:
303 : ""           = input port name (empty)
94  : 24           = input element ID (the parameter feeding this column)
95  : 40           = input value type (DXF code: 40 = double, 1 = string)
96  : 2            = (2 for double, 0 for string — meaning unclear)
282 : 0            = flag
305 : "Custom"     = value-set type
281 : 0            = flag
304 : "UpdatedDistance"  = output port name (a port on the input element)
280 : 1            = flag
```

The 5 columns of the sample: input = linear param (double, out `UpdatedDistance`), point param (double, out `UpdatedX`), point param (double, out `UpdatedY`), lookup param 1 (string, out `lookupString`), lookup param 2 (string, out `lookupString`).

**Semantics (forum-verified, Supermax posts #70/#73, 2008):** a lookup parameter is a table. **As soon as the current values of the linked parameters match a row of the table, the lookup switches to that row and sets all the parameters of that row** (chained lookups switch when *all* values of the other set match; a non-matching value keeps the old value, or a configurable default). One lookup parameter can carry any number of activators (tables); the UI only allows one. A lookup can be made to control another lookup by changing **one** dotted pair. The lookup parameter's `94` points at the lookup **action's** `95` creation number (the binding), and the action's per-column `94` points back at the feeding parameter's `95`.

The bidirectional graph edges (flags = 4, paired via the 5th edge field) reflect this: the parameter's value flows *into* the table (row selection) and the table's output flows *back* (updating the parameter's value).

---

## Visibility parameter (decoded)

`BLOCKVISIBILITYPARAMETER` (2008 dumps + the modern visibility sample):

- `301` = visibility label, `302` = description, `303` × n = state names
- `91` = current state index (0-based)
- `93` = total block-element count, followed by `331` × 93 = all block elements
- `92` = number of states; per state: `303` (state name), `94` (element count in the state), `332` × 94 (elements visible in the state), `95` (property count in the state), `333` × 95 (visible properties). (`94=0` → no 332; `95=0` → no 333.)
- `280` = visibility in the Properties palette; `281` = flag.

**Evaluation semantics (Supermax post #14, verified empirically in 2008):** on every state switch AutoCAD **first turns OFF everything in the 331 list and all properties, then turns ON the elements of the 332 list and the properties of the 95/333 lists** of the selected state. Multiple visibility parameters can coexist and manage disjoint element sets.

---

## 2008 "family tree" interpretation (historical context)

Supermax's master explanation (forum.dwg.ru thread 24597, post #7, 13.09.2008) describes the records as a **dependency lineage** ("родословная"):

- **Main record** (`93 = 32`): `91` (record number), `95` (creation number), `360` (pointer), then four `92`s: ① the element's own "main marker", ② the "adopted parent" marker (usually an ACTION; equals ① when there is no action; the main parent is always the GRIP), ③ child #1 (for a PARAMETER: its "UpdatedX"; for GRIP/ACTION: the PARAMETER), ④ child #2 (for a PARAMETER: its "UpdatedY"; for GRIP/ACTION: identical to ③; for a LOOKUP chain: the next extended record). `-1` = no affiliation.
- **Extended record** (`93 = 0`): `92` (owning marker), `94` ("unknown, always 1"), `91` ×2 (parent's 32-record number, own 32-record number), five `92`s (main marker; adopted marker or -1; UpdatedY↔UpdatedX link / Lookup-chain prev; UpdatedX↔UpdatedY link / Lookup-chain next; chain link to prev/next extended record).

This 2008 reading is **consistent with, but less precise than, the edge-list interpretation verified above**: in a 2007-era sample the two coincide (record numbers and edge indices are small and correlated), but the modern samples unambiguously use the four node `92`s as **in/out edge-list indices** (verified programmatically on all 10 samples). The 2008 post's `91`-pair reading of the extended record (parent/own record number) matches the verified `FromNodeIndex`/`ToNodeIndex` exactly, and its "parent = the element the value comes from" matches the verified value-flow direction.

**`entmod` rules (empirical, 2008–2013):** `1071` and `1010` are rejected by `entmod` (strip, they regenerate); the `1011` count must equal the `72` pair; after `entmod` on the graph the block must also be `entmod`-ed to refresh `vla-getdynamicblockproperties`; `entmod` reorders records by `91`. 2012 SP2: `entmod` no longer updates `BLOCKLOOKUPACTION` (bug or policy, unknown).

**Version history (statements actually found in the thread):** 2006 cannot open 2007 dynamic blocks ("Блок содержит объекты-заместители"); 2009-01: "the 2010 file format is the 2007 format, no principal changes to dynamic blocks" (Supermax) vs. "every three versions Autodesk changes the DWG format, so 2010 will be new" (Polischuk); 2010: new dynamic element = **property tables**; 2012: regressions (cannot create new extra visibility sets; `entmod` broken for lookup actions); 2013: "edit blocks with sets up to 2011 at best".

---

## Open questions

1. ~~`Edge.TrackedCount` (94)~~ — **decoded** (wires: port connections / grip-binding fallback / lookup columns); the "max vs. fallback" ambiguity needs a sample with both a `GripIds` slot *and* port connections for the same grip to fully disambiguate.
2. `NodeFlags` — confirm `0x20` is universal or find other values; fix the `NodeFlags` enum.
3. `96` in the lookup column definition (2 for double, 0 for string) — meaning.
4. Per-class evaluation formulas (how `140` etc. are computed from the context) — the core of Phase 3.
5. Unevaluated sentinels per class (only the component's `1.797693134862314E+99` is confirmed so far).
6. Whether the `98`/`99` tag values are version-dependent (2007: 27/31/25/8; modern: 33/329).

## Research sources

| Source | URL | What it provides | Status |
|---|---|---|---|
| ObjectARX API reference (Autodesk) — **fully captured** | https://help.autodesk.com/view/OARX/2025/ENU/?guid=OARX-RefGuide-__MEMBERTYPE_Methods_AcDbEvalGraph (+ AcDbEvalExpr/Context/IdMap/Variant/ContextPair) | the **complete official** API: `activate()`/`evaluate()` semantics, `AcDbEvalExpr::evaluate()` (default no-op), `value()` → `AcDbEvalVariant`, all lifecycle callbacks. Collected via the beehive content API (the site is a JS app); 2024/2025 + C++/.NET verified identical | **done** → [`objectarx-eval-api.md`](objectarx-eval-api.md) |
| A. Lazebny, "Mysteries of Autodesk's Caves" parts 6–12 — [part 6 EN](http://d107535.00067.h001.peterlink.ru/cad/2009/tainypod06e.htm) / [part 7 EN](http://d107535.00067.h001.peterlink.ru/cad/2009/tainypod07e.htm) … [part 12 EN](http://d107535.00067.h001.peterlink.ru/cad/2009/tainypod12e.htm) / [Russian originals](http://poleshchuk.spb.ru/cad/2009/tainypod07.htm) (koi8-r) | field-level reverse-engineering with full DXF dumps (visibility sets, lookup, the 183-record real-world graph); the AutoLISP navigation snippet. Part 7 = "Secrets of the ACAD_EVALUATION_GRAPH room" (record surgery), part 8 = "Where block elements are buried" (code meanings, evaluation semantics), part 9 = "Lookup and his friends" (lookup tables), parts 10–12 = more surgery + version history | **done** (all 6 English + 6 Russian parts retrieved; note the Russian pages are **koi8-r** encoded) |
| forum.dwg.ru thread t=24597 | https://forum.dwg.ru/showthread.php?t=24597 | the original 2008 thread (717 posts): Supermax's master record-structure explanation (post #7), 96/97 semantics (#10), visibility-parameter dumps (#12), code meanings (#13), evaluation semantics (#14), the multi-visibility-set insertion recipe (#108), lookup semantics (#70, #73), 2010/2012/2013 version statements, `BLOCKSTRETCHACTION` dumps + `entmod` procedure (#461–468) | **done** (all 717 posts extracted → `.tmp_research/dwg_full_thread.txt`) |
| adn-cis.org topic 1069 | https://adn-cis.org/forum/index.php?topic=1069.0 | a 183-record real-world `ACAD_EVALUATION_GRAPH` dump + a complete `BLOCKLINEARPARAMETER` dump (the only source showing `170–174`, `140/141–143`, `98=31/99=8` in action); also: the AutoCAD .NET API does **not** expose `AcDbEvalExpr` sub-records (P/Invoke `acdbEntGet` required) | **done** |
| adn-cis.org "poisk-sosednix-komnat" | https://adn-cis.org/poisk-sosednix-komnat.html | **red herring** — a Revit room-adjacency article, unrelated to `AcDbEvalGraph` (documented so it is not re-chased) | done (negative) |
| forum.abok.ru topic 14612 | https://forum.abok.ru/index.php?showtopic=14612&st=570 | (per external references) a long dynamic-block thread (540+ posts) | **unreachable** — DDoS-Guard JS challenge via every method (web_fetch/mcp/curl → 403; Wayback → 429/timeout). Only external references recorded; nothing quoted from it |
| FRX SDK documentation (Graebert) | https://docs.dev.graebert.com/html/2025.0.1/frx/files.html | the commercial FRX SDK ships `AcDbEvalExpr.h` / `AcDbEvalGraph.h` / `AcDbEvalVariant.h` (ODA's copy of the headers) | checked — ODA's copy, superseded by the complete official capture |
| acdb24.dll symbol dump | https://pedump.me/e5051a7fad3b72a36135f6c0cbf3e443/ | mangled C++ symbols confirming the class family (`adjacentNodeRemoved`, `copiedIntoGraph`, `AcDbEvalIdMap`, `AcDbEvalVariant`, `AcDbEvalContextPair`) | found; corroborated by the official API |
| acdb18.dll symbol dump | https://www.opendll.com/index.php?file-download=acdb18.dll&arch=32bit&version=18.2.51.0 | symbol listing of the older acdb18 (same class family) | found, not needed |
| SmartObjectARX (GitHub) | https://github.com/kevinzhwl/SmartObjectARX | its `inc/AcDbEval*` headers are 19-byte stubs (`#include "dbeval.h"`) — dead end | done (negative) |
| ezdxf DXF-tags reference | https://ezdxf.readthedocs.io/en/stable/dxfinternals/dxftags.html | the group-code → value-type table used in the "Group-code data types" section above (mirrors the Autodesk group-code reference) | **done** |
| Real-world DWG sample | e.g. https://www.rothoblaas.com/attachments/293317-product-1363/VGS | input for the empirical campaign (additional real-world DWGs) | candidate |
| Repo references | `reference/OpenDesign_Specification_for_.dwg_files.pdf`, `reference/autocad_2012_pdf_dxf-reference_enu.pdf`, `reference/ACadFileExploration.xlsx` | **negative result, documented to avoid re-checking:** the ODA DWG spec and the AutoCAD 2012 DXF reference do *not* document the eval graph object | checked |
| Repo samples | `samples/dynamic-blocks/` — 10 DWG+DXF pairs, one per parameter type | the ground truth used for all verifications in this document | used |
| Research working files | `.tmp_research/lazebny-and-forums.md` (1592 lines: verbatim quotes + translations + 8 DXF dumps + a 21-row source table), `.tmp_research/objectarx-eval-api.md` → promoted to `docs/articles/objectarx-eval-api.md`, `.tmp_research/raw/` (90+ official API pages), `.tmp_research/dwg_full_thread.txt` (all 717 forum posts) | the raw material behind the findings above | kept for re-verification |

---

## Plan

### Phase 1 — Deepen the format research (resolve the open questions) — *largely done*

1. ✅ Scrape the ObjectARX API reference (all six classes) → `docs/articles/objectarx-eval-api.md`.
2. ✅ Read Lazebny's parts 7–12 + the forum.dwg.ru thread + adn-cis 1069 → findings merged into this document.
3. ✅ **Empirical campaign**: built the C# analysis tool (`src/ACadSharp.Examples/EvaluationGraphExamples.cs`, run via `dotnet run --project src/ACadSharp.Examples -- evalgraph <file>`); re-verified the node/edge invariants on all 10 samples (DXF + DWG) — **all pass**; decoded the connection model, `TrackedCount`, edge flags, and the lookup-table structure.
4. ⬜ Collect 10–30 additional real-world dynamic-block DWGs and run the tool on them — in particular to disambiguate the `TrackedCount` "max vs. fallback" edge case and to find non-`0x20` `NodeFlags`.
5. ⬜ Optional, if AutoCAD is available: differential testing — create dynamic blocks with varied connections, save, diff the graphs; move grips and capture how stored `EvaluatedValue`s change.
6. ⬜ Optional: acdb DLL symbol/string analysis to confirm internal struct layout.

**Deliverable:** this document, completed — a format spec with all fields documented.

### Phase 2 — Clean up the model (`src/ACadSharp/Objects/Evaluations/`)

- Rename `Node.Data1–4` → `FirstInEdge/LastInEdge/FirstOutEdge/LastOutEdge`; `Edge.Data1–5` → `PrevInEdge/NextInEdge/PrevOutEdge/NextOutEdge/ReverseEdge` (DXF code attributes untouched)
- Fix `NodeFlags` to the real values
- Add `EvaluationGraph` helpers: build the per-node in/out edge lists from the data, with a consistency validator (node first/last == actual list ends; edge prev/next chains consistent) — used by tests and by the evaluator
- Fix the DWG reader comment on 96/97 (max ID, not count); verify the explicit DWG node/edge counts against real files
- Model the decoded connection layout: the `170`/`91`×n `GripIds` + `171–174` per-property connection counts (the reader currently reads a fixed 4-slot layout — needs to honor the counts)

### Phase 3 — Evaluation engine

- **Design** (mirroring ObjectARX): an `EvaluationContext` (expression-ID → value), `Activate(nodes)` + `Evaluate()` traversal in dependency order (topological; lookup reverse-edge cycles handled via the reverse-edge pairing / iterative relaxation), and per-class `Evaluate(context)` methods on the `Block*` classes writing into the context and the stored value fields (`140`/`70`/`1` = `EvaluatedValue`)
- **Implement per-class semantics**, simplest first:
  1. `BlockGripLocationComponent` + `BlockGrip` (location = base + displacement)
  2. `BlockLinearParameter` / `BlockPointParameter` / `BlockXYParameter` / `BlockPolarParameter` / `BlockRotationParameter`
  3. `BlockScaleAction` / `BlockMoveAction` / `BlockRotationAction`
  4. `BlockLookupParameter` / `BlockLookupAction` (table-driven, incl. reverse-edge semantics)
  5. `BlockVisibilityParameter` (the turn-all-off-then-on semantics) / `BlockFlipParameter` / `BlockAlignmentParameter` / `BlockStretchAction` / `BlockArrayAction`
- Persist evaluated values so round-trips keep them (writers already exist)

### Phase 4 — Validation

- Extend `DynamicBlockTests`: assert the new invariants (linked-list consistency) on all 10 samples (DWG + DXF)
- Evaluation tests: for each sample, activate the known "user-moved" node(s), evaluate, and compare results against the stored `EvaluatedValue`s / value fields
- Keep round-trip tests green (read → evaluate → write → re-read)

### Out of scope (for now)

- Writing new dynamic blocks from scratch (creating the graph programmatically) — the reader/writer already exists; evaluation is the gap
- Non-dynamic-block uses of `AcDbEvalGraph` (e.g. `AcDbField`-related evaluation) — investigate only if encountered during Phase 1

### Order of execution

Phase 1.4–1.6 (the remaining research) → Phase 2 (small) → Phase 3 (iterative per class) → Phase 4.
