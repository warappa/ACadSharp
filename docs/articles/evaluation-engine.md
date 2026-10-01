# Evaluation engine (implementation)

The evaluation engine is implemented in `src/ACadSharp/Objects/Evaluations/`, mirroring the ObjectARX `AcDbEval*` model.

**When to read this:** reasoning about engine behavior (values, evaluation order, the class hierarchy) or a node's `Evaluate` formula. *Adding or fixing a node type* is a self-triggered skill: [`.agents/skills/add-eval-node-type/SKILL.md`](../../.agents/skills/add-eval-node-type/SKILL.md).
**The AutoCAD specification side** — the on-disk format, node/edge records, the connection model, the lookup table, 2008 history — is analyzed in [`evaluation-graph.md`](evaluation-graph.md) (this directory). This doc is about *what we do* with the parsed data; the article is about *what AutoCAD writes*.

## Value model

- `EvaluationValue` (sealed) is a shape-agnostic value holder. `EvaluationValueType` has **8 shapes**: `None`, `Double`, `Point` (3D), `Point2d`, `String`, `Int`, `Char`, `ObjectId`.
- `EvaluationValue` factories: `None`/`FromDouble`/`FromPoint(XYZ)`/`FromPoint2d(XY)`/`FromString`/`FromInt`/`FromChar`/`FromObjectId(long)`; safe accessors (`DoubleValue`, `PointValue`, `Point2dValue`, `StringValue`, `IntValue`, `CharValue`, `ObjectIdValue`); `As<T>()` throws on a shape mismatch.
- `EvaluationContext` is a `Dictionary<int, Dictionary<string, EvaluationValue>>` (**expression object id** → port name → value), mirroring `AcDbEvalContext`. The top-level key is `EvaluationExpression.Id` (the object id the reader assigns — the same number the file stores in the target's `EvalConnection.Id`), **not** the node's position in the graph's node array (a `Node`'s `Index`, code 91, is an unrelated number). Verified empirically across three dynamic blocks in `samples/bpt-forensics/1kVKeetKOPIE.dwg`: the context's top-level keys are exactly the ids of the expressions that wrote them (e.g. 119/133/134 exceed the 27-node array's max index, so they cannot be positions). `SetValue(int, string, EvaluationValue)` + `double`/`string` overloads (there are **no** `int`/`long`/`char` overloads — use the `EvaluationValue` overload for those), plus `TryGetValue`/`HasValue`/`Clear`, and `Clone()` (a full copy; safe to share the nested dictionaries' immutable `EvaluationValue`s) + `AddMissingFrom(other)` (adds the entries `other` holds that this one is missing; existing entries win — merge a snapshot of an earlier pass for the nodes a later pass did not re-evaluate).
- `EvaluationExpression` (base node) has `CurrentValue` (`EvaluationValue`, `internal set`, not serialized) and `virtual bool Evaluate(EvaluationContext)` (default no-op, matching `AcDbExpr::evaluate()`). A leaf with one value shape adds a typed `new EvaluationValue<T> CurrentValue => base.CurrentValue.As<T>()` override (same name, correctly typed; the base property is the single storage `Evaluate` writes).
- `EvaluationExpression` also has `protected abstract GetDefaultValue()`: when a node has not yet been evaluated (`_currentValue.Type == None`), `CurrentValue` falls back to this, so **every concrete node must explicitly declare a default** (a stateful node → its initial value, e.g. a grip's zero `Displacement`; a stateless one → `EvaluationValue.None`). The abstract method makes a missing default a compile error (CS0534).

## Engine semantics

- `EvaluationGraph.Activate(nodes)` / `IsActivated(node)` / `Evaluate()` — marks the user-touched nodes and evaluates the **reachable subgraph** (following outgoing edges) in **topological order**, invoking each node's `Evaluate(context)`. Mirrors `AcDbEvalGraph::activate()` + `evaluate()`. A node's failure **aborts** the evaluation (matching ObjectARX); **no activated nodes = a no-op (returns true)** (the reachable subgraph is empty, not a cycle).
- `EvaluateReverse()` (`Evaluate(reverse: true)`) — clears the context, then evaluates the **reverse** pass: the reverse direction of the flag-4 (lookup) edges, in topological order from the activated nodes. **The two passes are not nested in general** (empirical, per the samples tested): on the Stud block the reverse pass evaluates more nodes than the forward pass; on `1kVKeetKOPIE.dwg`'s `*Model_Space` (28 nodes) both passes evaluate the same six producer nodes (the grips + the parameters they drive, ids 11/12/15/119/133/134) and the lookup action (id 137) is evaluated in **neither** pass — its flag-4 edge has **no activated endpoint**, so the edge is inactive in both directions.
- **Topological order** (`GetTopologicalOrder`) — a reachability BFS from the activated nodes (skipping `flag=4` lookup/reverse edges to break lookup cycles; a flag-4 edge whose two endpoints are both unactivated is **inactive** and skipped in both directions) + Kahn's algorithm; returns an empty list on a cycle.
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
    ├── BlockPropertiesTable        (≡ AcDbBlockPropertiesTable)      — data-only; reads/writes RowCount only (opaque tail)
    ├── BlockPropertiesTableGrip    (≡ AcDbBlockPropertiesTableGrip)  — data-only; reads/writes GripId only (opaque tail)
    └── BlockDynamicBlockProxyNode  (≡ AcDbDynamicBlockProxyNode)     — full DWG + DXF (ProxyName 300, ProxyData 309)
```

**IO completeness:** the **User / HorizontalConstraint / VerticalConstraint** parameters now read and write their **full DWG + DXF** records (value, value set, and — for the constraints — label, description, label offset). The on-disk layout is decoded in [evaluation-graph.md](evaluation-graph.md) ("The evaluation object's DWG layout"). The `BLOCKPROPERTIESTABLE` row and `BLOCKPROPERTIESTABLEGRIP` payloads remain an opaque, undecoded tail (data-only objects, not part of the evaluation graph).

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

`CurrentValue` is an `EvaluationValue` (the "object"); the **shape** column is the `T` of the leaf's typed `CurrentValue` view (`EvaluationValue<T>`). **Point** = the whole (X, Y) value; **Scalar** = a single `double`. (The `BlockLookupParameter` is the one exception: its shape is **table-driven and type-variable** — a string for a text column, a scalar for a numeric column — so it has no fixed typed view and is read through the type-agnostic base `CurrentValue`.)

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
| `BlockLookupParameter` | **type-variable** — the table-driven value of its column: **String** for a text column (DXF `95` = 1), **Scalar** for a numeric column (`95` = 40) — so it has *no fixed typed view* | reads the bound column's matched cell (or its `UnmatchedName` default, type-shaped); `UpdatedX/Y` for the 1-pt location |
| `BlockScaleAction` | the **scale** factor (Scalar) | reads the `Scale` port |
| `BlockMoveAction` | the (X, Y) **displacement** (Point) | reads `XDelta`/`YDelta` |
| `BlockRotationAction` | the **angle** (Scalar) | reads `AngleDelta` |
| `BlockStretchAction` | the (X, Y) **displacement** (Point) | reads `EndXDelta`/`EndYDelta` |
| `BlockPolarStretchAction` | the (X, Y) **displacement** (Point) | reads `BaseXDelta`/`BaseYDelta` |
| `BlockArrayAction` | the base value (Scalar) | reads the `Base` port |
| `BlockFlipAction` | the **flip** state (Scalar) | reads the `Flip` port |
| `BlockLookupAction` | the **matched row** index (−1 = none, Scalar) | reads each column's input value, finds the matching row (simplified; chained lookups / default-on-no-match not implemented) |

**Notes:**
- **Lookup actions** are **excluded from the forward evaluation**: they are only reachable via `flag=4` (reverse) edges, which the forward topological order skips — and they are also excluded from the **reverse** pass when their flag-4 edge has no activated endpoint (the edge is inactive in both directions; see `EvaluateReverse` above). So a lookup action's `CurrentValue` can stay unset after both passes.
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

## Adding a new evaluation node type

The 8-touch-point recipe (all touch points, the `GetDefaultValue()`/`WriteBitLong` gotchas, the test + sanity-check checklist) now lives in the **`add-eval-node-type` skill**: [`.agents/skills/add-eval-node-type/SKILL.md`](../../.agents/skills/add-eval-node-type/SKILL.md) — open it directly.
