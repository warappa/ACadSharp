# The Evaluation Graph (AcDbEvalGraph) — Format Analysis & Evaluation Plan

ACadSharp can read the AutoCAD **evaluation graph** (`ACAD_EVALUATION_GRAPH` / `AcDbEvalGraph`) that dynamic blocks store in the block record's XDictionary under the key `ACAD_ENHANCEDBLOCK`, but it cannot **evaluate** the graph. This document records the format analysis performed so far, the open questions, the research sources, and the plan to close the gap.

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

The official ObjectARX model: `AcDbEvalGraph::activate(nodes)` → `AcDbEvalGraph::evaluate(context)` — *"Evaluates the class by traversing the graph and invoking `AcDbEvalExpr::evaluate()` on all of the visited nodes. Applications must activate one or more nodes in the graph by calling `AcDbEvalGraph::activate()` before calling this method."* Supporting classes: `AcDbEvalContext`, `AcDbEvalIdMap`, `AcDbEvalVariant`, `AcDbEvalContextPair`, plus `AcDbEvalExpr::copiedIntoGraph` / `adjacentNodeRemoved`.

> The ODA DWG specification and the AutoCAD 2012 DXF reference **do not document** this object at all. It is an undocumented AutoCAD internal.

---

## Decoded format (verified against all 10 files in `samples/dynamic-blocks/`)

### Object header

| DXF code | Meaning |
|---|---|
| `96` | **max node ID** (size of the graph's ID space — *not* the node count; e.g. the lookup sample has 96=39 but only 19 nodes, because IDs are never reused after deletion) |
| `97` | same value as 96 in every sample observed |
| (implicit) | edge count — not stored in DXF; the edge records simply follow the node records until the object ends. In DWG the node/edge counts are explicit fields. |

### Node record

| DXF code | Field | Meaning |
|---|---|---|
| `91` | `Index` | position of the node in the node list (0-based) |
| `93` | `Flags` | `0x20` for every node in every sample; the current `NodeFlags` enum names (Visited/OutsideRefed/Selected/…) look copy-pasted from an unrelated ODA enum and are likely wrong |
| `95` | `Id` | unique expression ID (the "creation sequence number"; never reused) |
| `360` | `Expression` | handle of the `AcDbEvalExpr` this node represents |
| `92` ×4 | **`Data1–4`** | **`(firstInEdge, lastInEdge, firstOutEdge, lastOutEdge)`** — indices into the edge list, `-1` = empty list |

✅ *Verified: for all 10 sample files, the node data equals (first, last) of the actual in/out edge lists of that node.*

### Edge record

| DXF code | Field | Meaning |
|---|---|---|
| `92` | `Index` | position of the edge in the edge list (0-based) |
| `93` | `Flags` | `0` in every sample |
| `94` | `TrackedCount` | **unknown** — 1 for simple edges, 2 for grip→parameter edges, 5 for parameter→action edges in the linear sample; varied values in the lookup sample |
| `91` | `FromNodeIndex` | source node index |
| `91` | `ToNodeIndex` | target node index |
| `92` ×5 | **`Data1–5`** | **`(prevInEdge, nextInEdge, prevOutEdge, nextOutEdge, reverseEdge)`** — prev/next are the doubly-linked in/out edge lists of the from/to nodes; the 5th is a paired *reverse* edge (only set for bidirectional lookup connections, e.g. the `0↔6`, `10↔6`, `2↔6`, `15↔6` pairs in the lookup sample) |

✅ *Verified: for all 10 sample files, the edge data equals the prev/next of the actual per-node in/out edge lists, and the 5th field pairs reverse edges.*

### Topology

- The graph is usually a **tree** (edges = nodes − 1): the linear/point/rotation/xy/polar/alignment/flip/visibility samples all satisfy this.
- **Lookup** parameters add reverse-edge pairs (22 edges for 19 nodes), creating cycles.

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

### Connections (partially decoded)

The `Block*` classes carry "connection" fields that bind ports between elements:

| Class | Fields | Decoded meaning |
|---|---|---|
| parameter (`Block2PtParameter`) | `170` + `91`×n, then `171–174` blocks, `301–304` | list of (connected-expression-**ID**, port-name) — e.g. the linear sample: `(5, "DisplacementX"), (5, "DisplacementY"), (2, "DisplacementX"), (2, "DisplacementY")` = the two grips bound to the parameter's ports |
| grip (`BlockGrip`) | `91`, `92` | expression **IDs** of the grip's two `BlockGripLocationComponent`s (`UpdatedX`/`UpdatedY`) |
| action (`AcDbBlockScaleAction`) | `94/95/96`, `303/304/305` | current scale factors + the action's own port names (`Scale`/`XScale`/`YScale`) |
| action (`AcDbBlockActionWithBasePt`) | `92/93`, `301/302` | base-point bindings by component name (`UpdatedBaseX`/`UpdatedBaseY`) |
| all | `1071` | unknown (0 in all samples) |

**Open:** the exact rule by which an action's ports match a parameter's ports (name-based? order-based? via the graph edges?), and the semantics of `TrackedCount` (94).

### "Not yet evaluated" sentinels

`BlockGripLocationComponent.140 = 1.797693134862314E+38` (= DBL_MAX) in the linear sample → sentinel for "not yet evaluated". The semantics of the per-class value fields (`140`, `70`, `1`, `1010–1032`, …) still need to be pinned down per class.

---

## Open questions

1. `Edge.TrackedCount` (94) — build a hypothesis table and verify (candidate: number of port-connections the edge carries)
2. `NodeFlags` — confirm 0x20 is universal or find other values; fix the `NodeFlags` enum
3. Connection-field semantics per class (table above) — verify across more files
4. Port-matching rules between action ports and parameter ports
5. Per-class evaluation formulas (how `140` etc. are computed from the context)
6. Unevaluated sentinels per class

## Research sources

| Source | URL | What it provides | Status |
|---|---|---|---|
| ObjectARX API reference (Autodesk) | https://help.autodesk.com/view/OARX/2025/ENU/?guid=OARX-RefGuide-__MEMBERTYPE_Methods_AcDbEvalGraph · [AcDbEvalExpr methods](https://help.autodesk.com/view/OARXMAC/2024/ENU/?guid=OARXMAC-RefGuide-__MEMBERTYPE_Methods_AcDbEvalExpr) · [AcDbEvalGraph::evaluate](https://help.autodesk.com/view/OARXMAC/2024/ENU/?guid=OARXMAC-RefGuide-AcDbEvalGraph__evaluate_AcDbEvalContext__const) · [AcDbEvalExpr::evaluate](https://help.autodesk.com/view/OARXMAC/2024/ENU/?guid=OARXMAC-RefGuide-AcDbEvalExpr__evaluate_AcDbEvalContext_) | the **official** evaluation model: `activate()`/`evaluate()` semantics, `AcDbEvalExpr::evaluate(context)` ("called for a graph-resident node when the node is visited during a call to AcDbEvalGraph::evaluate(); the default implementation does nothing"), `copiedIntoGraph`, `adjacentNodeRemoved` | partially scraped via search snippets; the site is a JS app — full scrape pending (Phase 1.1) |
| A. Lazebny, "Mysteries of Autodesk's Caves" part 6 — [English](http://d107535.00067.h001.peterlink.ru/cad/2009/tainypod06e.htm) / [Russian](http://poleshchuk.spb.ru/cad/2009/tainypod06.htm) | field-level reverse-engineering of `AcDbEvalGraph`/`AcDbEvalExpr` with full DXF dumps (visibility-set example); the AutoLISP "incantation" to navigate block → dictionary → graph (quoted in the "Where the object lives" section). Note: the author's "family tree" reading of the node 92s is consistent with, but less precise than, the edge-list interpretation verified above | read (part 6); parts 7–12 pending (Phase 1.2) |
| forum.dwg.ru thread t=24597 | https://forum.dwg.ru/showthread.php?t=24597 | the original thread behind Lazebny part 6; real-world eval-graph DXF dumps (visibility sets, lookup) | found, not yet read in full |
| forum.abok.ru topic 14612 | https://forum.abok.ru/index.php?showtopic=14612&st=570 | real-world eval-graph DXF dumps | found, not yet read |
| adn-cis.org | https://adn-cis.org/forum/index.php?topic=1069.0 · https://adn-cis.org/poisk-sosednix-komnat.html | more real-world eval-graph DXF dumps | found, not yet read |
| FRX SDK documentation (Graebert) | https://docs.dev.graebert.com/html/2025.0.1/frx/files.html | the commercial FRX SDK ships `AcDbEvalExpr.h` / `AcDbEvalGraph.h` / `AcDbEvalVariant.h` — its API docs may be more complete than Autodesk's for these ODA classes | found, not yet read — promising for Phase 1.1 |
| acdb24.dll symbol dump | https://pedump.me/e5051a7fad3b72a36135f6c0cbf3e443/ | mangled C++ symbols: `AcDbEvalExpr::adjacentNodeRemoved`, `AcDbEvalExpr::copiedIntoGraph`, `AcDbEvalIdMap`, `AcDbEvalVariant`, `AcDbEvalContextPair` | found via search; full symbol analysis pending (Phase 1.5) |
| acdb18.dll symbol dump | https://www.opendll.com/index.php?file-download=acdb18.dll&arch=32bit&version=18.2.51.0 | symbol listing of the older acdb18 (same class family) | found, not yet used |
| SmartObjectARX (GitHub) | https://github.com/kevinzhwl/SmartObjectARX/blob/master/inc/AcDbHostApplicationServices | header listing `AcDbEvalExpr` / `AcDbEvalGraph` / `AcDbEvalIdMap` / `AcDbEvalVariant` | found, not yet read |
| Real-world DWG sample | e.g. https://www.rothoblaas.com/attachments/293317-product-1363/VGS (a DWG containing `ACAD_EVALUATION_GRAPH`, found via web search) | input for the empirical campaign (Phase 1.3) | candidate |
| Repo references | `reference/OpenDesign_Specification_for_.dwg_files.pdf`, `reference/autocad_2012_pdf_dxf-reference_enu.pdf`, `reference/ACadFileExploration.xlsx` | **negative result, documented to avoid re-checking:** the ODA DWG spec and the AutoCAD 2012 DXF reference do *not* document the eval graph object (full-text search found only `AcDbField` evaluation flags); the xlsx is a generic class-table exploration with no eval-graph data | checked |
| Repo samples | `samples/dynamic-blocks/` — 10 DWG+DXF pairs, one per parameter type (alignment, basepoint, flip, linear, lookup, point, polar, rotation, visibility, xy) | the ground truth used for all verifications in this document | used |

---

## Plan

### Phase 1 — Deepen the format research (resolve the open questions)

1. Scrape the ObjectARX API reference (all six classes above) → fix the official evaluation model and naming.
2. Read Lazebny's remaining parts (7–12) + the forum.dwg.ru thread.
3. **Empirical campaign** (core):
   - Build a small analysis tool (Python or C# test utility) that dumps, for any DWG/DXF: the full eval graph with resolved expression classes and all connection/value fields (`170–174`, `91/92/93`, `94–96`, `301–305`, `1071`, `140`/`70`/`1`).
   - Re-verify the node/edge invariants on the 10 bundled samples and on **additional real-world DWGs** (collect 10–30 dynamic-block DWGs) — in particular `TrackedCount`, `NodeFlags`, and the 5th edge field on non-lookup blocks.
   - Build the connection-semantics table and verify across samples.
   - Identify the unevaluated sentinels and the per-class "current value" fields.
4. Optional, if AutoCAD is available: differential testing — create dynamic blocks with varied connections, save, diff the graphs; move grips and capture how stored `EvaluatedValue`s change.
5. Optional: acdb DLL symbol/string analysis to confirm internal struct layout.

**Deliverable:** this document, completed — a format spec with all fields documented.

### Phase 2 — Clean up the model (`src/ACadSharp/Objects/Evaluations/`)

- Rename `Node.Data1–4` → `FirstInEdge/LastInEdge/FirstOutEdge/LastOutEdge`; `Edge.Data1–5` → `PrevInEdge/NextInEdge/PrevOutEdge/NextOutEdge/ReverseEdge` (DXF code attributes untouched)
- Fix `NodeFlags` to the real values
- Add `EvaluationGraph` helpers: build the per-node in/out edge lists from the data, with a consistency validator (node first/last == actual list ends; edge prev/next chains consistent) — used by tests and by the evaluator
- Fix the DWG reader comment on 96/97 (max ID, not count); verify the explicit DWG node/edge counts against real files

### Phase 3 — Evaluation engine

- **Design** (mirroring ObjectARX): an `EvaluationContext` (expression-ID → value), `Activate(nodes)` + `Evaluate()` traversal in dependency order (topological; lookup reverse-edge cycles handled via the reverse-edge pairing / iterative relaxation), and per-class `Evaluate(context)` methods on the `Block*` classes writing into the context and the stored value fields (`140`/`70`/`1` = `EvaluatedValue`)
- **Implement per-class semantics**, simplest first:
  1. `BlockGripLocationComponent` + `BlockGrip` (location = base + displacement)
  2. `BlockLinearParameter` / `BlockPointParameter` / `BlockXYParameter` / `BlockPolarParameter` / `BlockRotationParameter`
  3. `BlockScaleAction` / `BlockMoveAction` / `BlockRotationAction`
  4. `BlockLookupParameter` / `BlockLookupAction` (table-driven, incl. reverse-edge semantics)
  5. `BlockVisibilityParameter` / `BlockFlipParameter` / `BlockAlignmentParameter` / `BlockStretchAction` / `BlockArrayAction`
- Persist evaluated values so round-trips keep them (writers already exist)

### Phase 4 — Validation

- Extend `DynamicBlockTests`: assert the new invariants (linked-list consistency) on all 10 samples (DWG + DXF)
- Evaluation tests: for each sample, activate the known "user-moved" node(s), evaluate, and compare results against the stored `EvaluatedValue`s / value fields
- Keep round-trip tests green (read → evaluate → write → re-read)

### Out of scope (for now)

- Writing new dynamic blocks from scratch (creating the graph programmatically) — the reader/writer already exists; evaluation is the gap
- Non-dynamic-block uses of `AcDbEvalGraph` (e.g. `AcDbField`-related evaluation) — investigate only if encountered during Phase 1

### Order of execution

Phase 1 (the bulk of the work) → Phase 2 (small) → Phase 3 (iterative per class) → Phase 4. Phase 1.3 can start immediately with the bundled samples; additional real-world DWGs are the main external input needed.
