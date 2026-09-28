# Evaluation engine (implementation)

The evaluation engine is implemented in `src/ACadSharp/Objects/Evaluations/`, mirroring the ObjectARX `AcDbEval*` model.

**When to read this:** adding or fixing an evaluation node type, or reasoning about engine behavior (values, evaluation order, the class hierarchy).
**The AutoCAD specification side** — the on-disk format, node/edge records, the connection model, the lookup table, 2008 history — is analyzed in [`docs/articles/evaluation-graph.md`](../articles/evaluation-graph.md). This doc is about *what we do* with the parsed data; the article is about *what AutoCAD writes*.

## Value model

- `EvaluationValue` (sealed) is a shape-agnostic value holder. `EvaluationValueType` has **8 shapes**: `None`, `Double`, `Point` (3D), `Point2d`, `String`, `Int`, `Char`, `ObjectId`.
- `EvaluationValue` factories: `None`/`FromDouble`/`FromPoint(XYZ)`/`FromPoint2d(XY)`/`FromString`/`FromInt`/`FromChar`/`FromObjectId(long)`; safe accessors (`DoubleValue`, `PointValue`, `Point2dValue`, `StringValue`, `IntValue`, `CharValue`, `ObjectIdValue`); `As<T>()` throws on a shape mismatch.
- `EvaluationContext` is a `Dictionary<int, Dictionary<string, EvaluationValue>>` (node id → port name → value), mirroring `AcDbEvalContext`. `SetValue(int, string, EvaluationValue)` + `double`/`string` overloads (there are **no** `int`/`long`/`char` overloads — use the `EvaluationValue` overload for those), plus `TryGetValue`/`HasValue`/`Clear`.
- `EvaluationExpression` (base node) has `CurrentValue` (`EvaluationValue`, `internal set`, not serialized) and `virtual bool Evaluate(EvaluationContext)` (default no-op, matching `AcDbExpr::evaluate()`). A leaf with one value shape adds a typed `new EvaluationValue<T> CurrentValue => base.CurrentValue.As<T>()` override (same name, correctly typed; the base property is the single storage `Evaluate` writes).
- `EvaluationExpression` also has `protected abstract GetDefaultValue()`: when a node has not yet been evaluated (`_currentValue.Type == None`), `CurrentValue` falls back to this, so **every concrete node must explicitly declare a default** (a stateful node → its initial value, e.g. a grip's zero `Displacement`; a stateless one → `EvaluationValue.None`). The abstract method makes a missing default a compile error (CS0534).

## Engine semantics

- `EvaluationGraph.Activate(nodes)` / `IsActivated(node)` / `Evaluate()` — marks the user-touched nodes and evaluates the **reachable subgraph** (following outgoing edges) in **topological order**, invoking each node's `Evaluate(context)`. Mirrors `AcDbEvalGraph::activate()` + `evaluate()`. A node's failure **aborts** the evaluation (matching ObjectARX); **no activated nodes = a no-op (returns true)** (the reachable subgraph is empty, not a cycle).
- **Topological order** (`GetTopologicalOrder`) — a reachability BFS from the activated nodes (skipping `flag=4` lookup/reverse edges to break lookup cycles) + Kahn's algorithm; returns an empty list on a cycle.
- Multi-valued expressions carry the **whole** value (the full (X, Y) point, not a representative component); the context ports are the fine-grained X/Y channels downstream nodes read.

## Graph structure

- `EvaluationGraph` holds `Nodes` (each wraps an `EvaluationExpression`) and `Edges`.
- An `Edge` connects `FromNodeIndex` (source/output) → `ToNodeIndex` (target/input).
- A `Node` has `FirstInEdge`/`LastInEdge` (incoming-edge list) and `FirstOutEdge`/`LastOutEdge` (outgoing-edge list); an `Edge` is linked via `PrevInEdge`/`NextInEdge` (per ToNode) and `PrevOutEdge`/`NextOutEdge` (per FromNode). Walk a list by following `Next*Edge` until it is `-1` (`Node.GetIncomingEdges()`/`GetOutgoingEdges()` do this).
- A node's **inputs** = incoming edges; **outputs** = outgoing edges. An `EvalConnection` (on a node) is a port-level input: `Id` = source node id, `Name` = the source port to read.

## Class hierarchy (verified against real files)

The ACadSharp classes mirror the ObjectARX `AcDb*` class names 1:1. The **inheritance structure itself was verified against real AutoCAD files**: a DXF object carries its full ARX subclass chain as a sequence of `100` (marker) groups, so the samples give the true parent→child order. Evidence from `samples/dynamic-blocks/`:

```
BLOCKLINEARPARAMETER object:              BLOCKGRIPLOCATIONCOMPONENT object:
  100 AcDbEvalExpr                          100 AcDbEvalExpr
  100 AcDbBlockElement                        100 AcDbBlockGripExpr   ← direct child of the base
  100 AcDbBlockParameter
  100 AcDbBlock2PtParameter
  100 AcDbBlockLinearParameter
```

Two findings stand out. First, the **component is *not* a `BlockElement`**: its chain is `AcDbEvalExpr → AcDbBlockGripExpr` (it carries no `300` name / `1071` group, unlike a `BlockElement`), so in the ACadSharp code it inherits `EvaluationExpression` **directly**, not `BlockElement`. Second, `AcDbBlockGripExpr` is a **single concrete class** (the DXF object name `BLOCKGRIPLOCATIONCOMPONENT` is just that class's DXF name) — there is no separate "component family" in ARX.

The full tree:

```
EvaluationExpression  (≡ AcDbEvalExpr)  — abstract; holds CurrentValue + abstract GetDefaultValue()
└── BlockElement      (≡ AcDbBlockElement)  — abstract; adds ElementName (300) + Value1071 (1071)
    ├── BlockParameter (≡ AcDbBlockParameter) — abstract
    │   ├── Block1PtParameter / Block2PtParameter  — abstract (point-count specialisation)
    │   └── 16 concrete leaves (XY, User, Char, Text, Handle, Linear, Point,
    │       BasePoint, Visibility, Polar, Rotation, Alignment, HorizontalConstraint,
    │       VerticalConstraint, Lookup, Flip)
    ├── BlockGrip     (≡ AcDbBlockGrip) — abstract
    │   └── 8 concrete leaves (Linear, Polar, XY, Visibility, Lookup, Rotation, Alignment, Flip)
    └── BlockAction   (≡ AcDbBlockAction) — abstract
        └── BlockActionBasePt — abstract (base-point specialisation)
            └── 8 concrete leaves (Move, Scale, Rotation, Stretch, Flip, Array, PolarStretch, Lookup)
+ EvaluationExpression's direct children (single concrete classes, NOT under BlockElement):
    ├── BlockGripLocationComponent  (≡ AcDbBlockGripExpr)
    ├── BlockPropertiesTable        (≡ AcDbBlockPropertiesTable)      — data-only stub
    ├── BlockPropertiesTableGrip    (≡ AcDbBlockPropertiesTableGrip)  — data-only stub
    └── BlockDynamicBlockProxyNode  (≡ AcDbDynamicBlockProxyNode)     — placeholder
```

## The three value-semantics archetypes

Grouped by *what a node's value means before the graph has been evaluated*, the leaves fall into three archetypes, and the default-value design follows the grouping:

| Archetype | Members | Pre-evaluation value | `GetDefaultValue()` |
|---|---|---|---|
| **Stateful — parameter** | the 16 `BlockParameter` leaves | a stored value/geometry, but the *shape and initial value differ per type* (stored value, zero displacement, `atan2` of stored points, …) | **abstract** — each leaf implements its own |
| **Stateful — grip** | the 8 `BlockGrip` leaves | the stored `Displacement` (zero initially) | **shared at the archetype**: `BlockGrip` overrides it to `FromPoint(Displacement)`; leaves inherit |
| **Stateless** | the 8 `BlockAction` leaves, the component, the 2 table stubs, the proxy | none — pure computation / data-only / placeholder | **shared at the archetype**: `BlockAction` overrides it to `EvaluationValue.None`; the 4 single-class children each carry an explicit, documented `=> None` |

`EvaluationExpression.GetDefaultValue()` is **`protected abstract`**, so the decision is *compile-time-enforced*: a new concrete node that forgets a default is a `CS0534` build error. `BlockParameter` stays abstract (propagating the requirement to every parameter leaf); `BlockGrip` and `BlockAction` supply a shared default their leaves inherit; the 4 single-class direct children of `EvaluationExpression` cannot share an intermediate class (ARX has none for them), so each states `=> None` explicitly.

The **`CurrentValue` fallback** reads `GetDefaultValue()` when the node has not yet been evaluated, so a node exposes its meaningful initial value *before* `Evaluate()` runs — this is what makes the viewer render a stored parameter value (or `<unset>` for a stateless node) rather than a blank.

## Per-class `Evaluate` formulas

`CurrentValue` is an `EvaluationValue` (the "object"); the **shape** column is the `T` of the leaf's typed `CurrentValue` view (`EvaluationValue<T>`). **Point** = the whole (X, Y) value; **Scalar** = a single `double`.

| Class | `CurrentValue` (shape) | Writes to the context |
|-------|------------------------|----------------------|
| `BlockGrip` | the full (X, Y) **displacement** (Point) | `DisplacementX/Y` = `(ActivatedLocation ?? Location) − Location` (zero when not activated) |
| `BlockGripLocationComponent` | the read coordinate (Scalar) | reads the connected parameter's updated coordinate (the port named by `Connection`, e.g. `UpdatedEndX`) → `EvaluatedValue` (code `40`) |
| `BlockLinearParameter` | the signed **distance** along the axis (Scalar) | `Scale`/`XScale`/`YScale` = `(updatedSecond − updatedFirst)·axis`; `UpdatedBaseX/Y`, `UpdatedEndX/Y` |
| `BlockXYParameter` | the (X, Y) **offset** (Point) | `XScale`/`YScale` = `(firstDisp.X, firstDisp.Y)`; updated points |
| `BlockPolarParameter` | the (distance, angle) **pair** (Point, polar-space: X = distance, Y = angle) | `Scale`/`AngleDelta` = `(‖delta‖, atan2)`; updated points |
| `BlockRotationParameter` | the **angle** (Scalar) | `AngleDelta` = `atan2`; updated points |
| `BlockAlignmentParameter` | the **angle** (Scalar) | `AngleDelta` = `atan2`; updated points |
| `BlockPointParameter` | the full (X, Y) **displacement** (Point) | `XDelta`/`YDelta`; `UpdatedX/Y` |
| `BlockFlipParameter` | the **flip state** (0/1, Scalar) | `UpdatedFlip` = 0 (default); updated points |
| `BlockVisibilityParameter` | the **state index** (0, Scalar) | `Value` = 0 (default); updated location |
| `BlockLookupParameter` | the full (X, Y) **displacement** (Point) | `UpdatedX/Y` (the table is not decoded, so table-driven selection is not implemented) |
| `BlockScaleAction` | the **scale** factor (Scalar) | reads the `Scale` port |
| `BlockMoveAction` | the (X, Y) **displacement** (Point) | reads `XDelta`/`YDelta` |
| `BlockRotationAction` | the **angle** (Scalar) | reads `AngleDelta` |
| `BlockStretchAction` | the (X, Y) **displacement** (Point) | reads `EndXDelta`/`EndYDelta` |
| `BlockPolarStretchAction` | the (X, Y) **displacement** (Point) | reads `BaseXDelta`/`BaseYDelta` |
| `BlockArrayAction` | the base value (Scalar) | reads the `Base` port |
| `BlockFlipAction` | the **flip** state (Scalar) | reads the `Flip` port |
| `BlockLookupAction` | the **matched row** index (−1 = none, Scalar) | reads each column's input value, finds the matching row (simplified; chained lookups / default-on-no-match not implemented) |

**Notes:**
- **Lookup actions** are **excluded from the forward evaluation**: they are only reachable via `flag=4` (reverse) edges, which the topological order skips. So a lookup action's `CurrentValue` stays unset after a forward `evaluate()` — the lookup is a separate (reverse) evaluation.
- **Actions** read the connected parameter's value and store it as their `CurrentValue`; the full transform application to the block's entities is **out of scope** for the core engine.

## Verified results (all 10 samples)

Running the evaluator on all 10 samples (`dotnet run --project src/ACadSharp.Examples -- eval <file>`, activating all grips with zero displacement) produces consistent values:

| Sample | Parameter value | Interpretation |
|--------|----------------|---------------|
| `BLOCKLINEARPARAMETER` | 5 | distance between base (0,0,0) and end (5,0,0) |
| `BLOCKPOLARPARAMETER` | 9.082 | distance between base (2.007,2.346) and end (8.429,8.767) |
| `BLOCKROTATIONPARAMETER` | 1.571 | angle = atan2 = 90° (1.571 rad) |
| `BLOCKALIGNMENTPARAMETER` | 0.524 | angle = atan2 = 30° (0.524 rad) |
| `BLOCKXYPARAMETER` / `BLOCKPOINTPARAMETER` | (0,0,0) | zero (X, Y) displacement (grips not moved) — now the **whole point**, not just X |
| `BLOCKFLIPPARAMETER` / `BLOCKVISIBILITYPARAMETER` | 0 | default state |

End-to-end `EvaluationTests` (activate a grip with a known `ActivatedLocation`, evaluate, compare against the geometry) confirm: linear 5→8 (move end grip by (3,0,0)), polar 9.08→10.59 (move by (2,0,0)), rotation 90°→135° (rotate by 45°), point (0,0,0)→(2,3,0) (move by (2,3,0) — the **whole** (X, Y) displacement, not just X), and that a component's stored `EvaluatedValue` (code `40`) is updated from the `1.797693134862314E+99` sentinel to the computed value.

## Adding a new evaluation node type (8 touch points)

1. `DxfFileToken.cs` — `ObjectXxx = "XXX"` (the DXF object name).
2. `DxfSubclassMarker.cs` — `Xxx = "AcDbXxx"` (the subclass marker).
3. `Objects/Evaluations/Xxx.cs` — the class. For a parameter, inherit `BlockParameter`; add `[DxfName]`/`[DxfSubClass]` attrs, a `Value` with a `[DxfCodeValue]`, an `Evaluate` that writes the `"Value"` port (`context.SetValue(this.Id, "Value", EvaluationValue.FromX(...)` + set `base.CurrentValue`), a typed `CurrentValue` override, a `GetDefaultValue()` override (**required** — the method is abstract: a stateful node returns its initial value, e.g. a grip's zero `Displacement`; a stateless node returns `EvaluationValue.None`), and `GetDxfClass()` (ItemClassId=499, MaintenanceVersion=55, ProxyFlags=EraseAllowed|CloningAllowed|DisablesProxyWarningDialog).
4. `IO/Templates/CadXxxTemplate.cs` — the template (`: CadBlockParameterTemplate`, typed property + parameterless and object-taking ctors).
5. `IO/DXF/DxfStreamReader/DxfObjectsSectionReader.cs` — `readXxx` (specific codes; `default` falls through to `readBlockParameter`) + a dispatch case.
6. `IO/DXF/DxfStreamWriter/DxfObjectsSectionWriter.cs` — `writeXxx` (`writeBlockParameter` + `Write(100, marker)` + per-code `Write`) + a dispatch case.
7. `IO/DWG/DwgStreamReaders/DwgObjectReader.Objects.cs` — `readXxx` + a dispatch case in `DwgObjectReader.cs`.
8. `IO/DWG/DwgStreamWriters/DwgObjectWriter.Objects.cs` — `writeXxx` + a dispatch case.

Then: build all 6 TFMs, add tests (evaluate → assert `CurrentValue.Type` + payload; a DXF round-trip), run the suite, commit.

**DWG-primitive gotcha:** `WriteBitLong`/`ReadBitLong` are the DWG 64-bit primitives, but `WriteBitLong` takes an `int` — cast a `long` value.
