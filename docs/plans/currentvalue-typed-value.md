# Plan — Make `CurrentValue` carry the whole value (typed leaf view over a type-erased base)

## Goal

`EvaluationExpression.CurrentValue` is today a `double?` (a scalar). For **multi-valued** expressions
(`BlockXYParameter`, `BlockPointParameter`, `BlockLookupParameter`, `BlockGrip`) it holds only the
**X component** as a representative; the full value lives in the context ports. This is a documented
*simplification* of the ObjectARX model, where `AcDbEvalExpr::value()` returns an `AcDbEvalVariant`
(a resbuf wrapper) that can hold a **structured value** (`double`→`kDouble`, `AcGePoint3d`→`kPoint3d`,
`ACHAR*`→`kString`, `Adesk::Int32`→`kLong`, …).

Make `CurrentValue` carry the **whole value** — a point when the value is a point — while keeping the
type-erased base (so the heterogeneous `EvaluationGraph` still works) and giving each leaf a
compile-time-typed read under the **same property name**.

## Finalized decisions

| # | Decision | Choice |
|---|----------|--------|
| 1 | Polar parameter shape | **`XYZ`** — the (distance, angle) pair as a *polar-space* point (documented as such, not a Cartesian location). |
| 2 | Variant type name | **`EvaluationValue`** (non-generic) + **`EvaluationValue<T>`** (generic typed view). |
| 3 | Base property name | **`CurrentValue`** (unchanged — it is the most-used name in the codebase). It now holds the type-erased `EvaluationValue` "object". |
| 4 | Leaf property | **`new EvaluationValue<T> CurrentValue => base.CurrentValue.As<T>()`** — same name as the base, typed, a computed read (no second storage). |
| 5 | `As<T>()` semantics | **Strict** — returns `EvaluationValue<T>.None` when unset (`Type == None`); **throws** on a genuine shape mismatch. |

## The types

### `EvaluationValueType`

```csharp
public enum EvaluationValueType
{
    None,    // unset (before first evaluation) — mirrors AcDbEvalVariant::kNone
    Double,  // a scalar — mirrors kDouble
    Point,   // a 3D point — mirrors kPoint3d
}
```

### `EvaluationValue` (the "object" — non-generic, type-erased)

```csharp
public sealed class EvaluationValue
{
    public EvaluationValueType Type { get; }
    public double? DoubleValue { get; }
    public XYZ?    PointValue  { get; }

    private EvaluationValue(EvaluationValueType t, double? d = null, XYZ? p = null) { ... }

    public static EvaluationValue None        { get; } = new(EvaluationValueType.None);
    public static EvaluationValue FromDouble(double v) => new(EvaluationValueType.Double, doubleValue: v);
    public static EvaluationValue FromPoint(XYZ p)     => new(EvaluationValueType.Point,  pointValue:  p);

    // Strict: None -> None; matching shape -> typed value; mismatch -> throw.
    public EvaluationValue<T> As<T>() { ... }

    public override string ToString() => /* "<unset>" | "8" | "(2,3,0)" */;
}
```

Invariants: `Type == Double` ⇒ `DoubleValue != null`; `Type == Point` ⇒ `PointValue != null`;
`Type == None` ⇒ both null.

### `EvaluationValue<T>` (the typed view)

```csharp
public sealed class EvaluationValue<T>
{
    private readonly bool _isSet;
    public T Value { get; }
    public bool IsSet => this._isSet;

    private EvaluationValue(T v, bool isSet) { this.Value = v; this._isSet = isSet; }

    public static EvaluationValue<T> None { get; } = new(default, false);
    public static EvaluationValue<T> Of(T v) => new(v, true);

    public override string ToString() => IsSet ? Value?.ToString() ?? "null" : "<unset>";
}
```

> `IsSet` is a **separate flag**, not derived from `Equals(value, default)` — a legitimate `0.0`
> (a zero displacement / distance) must read as *set*.

## The base + a leaf

```csharp
public abstract class EvaluationExpression : NonGraphicalObject
{
    // ... Id, EvaluatedValue, etc.

    /// The node's value as a shape-agnostic object (mirrors AcDbEvalExpr::value()).
    /// Updated during Evaluate(); None before the first evaluation.
    public EvaluationValue CurrentValue { get; internal set; } = EvaluationValue.None;

    public virtual bool Evaluate(EvaluationContext context) => true;
}

public class BlockPointParameter : Block1PtParameter
{
    /// The current value, correctly typed (hides the base CurrentValue; a computed read).
    public new EvaluationValue<XYZ> CurrentValue => base.CurrentValue.As<XYZ>();

    public override bool Evaluate(EvaluationContext context)
    {
        this.GetDisplacement(context, out XYZ displacement);
        // ...
        base.CurrentValue = EvaluationValue.FromPoint(displacement); // write the ONE storage
        return true;
    }
}
```

- **One storage.** The base `CurrentValue` is the single source of truth; the leaf `CurrentValue` is
  a computed read (`base.CurrentValue.As<T>()`). Nothing to keep in sync.
- **The write goes to the base.** `Evaluate` assigns `base.CurrentValue = …` (the `internal set`),
  never the leaf (which is read-only by construction).
- **`new` is load-bearing.** `this.CurrentValue` in the leaf = the typed view; `base.CurrentValue` =
  the object. Code holding a base/intermediate reference sees the object; code holding a leaf
  reference sees the typed value.

## Per-class semantics

Each `Evaluate` stores the **whole** value it already computes; the context ports are **unchanged**.

| Leaf | `Evaluate` stores | Leaf `CurrentValue` type |
|------|-------------------|--------------------------|
| `BlockGrip` | `FromPoint(displacement)` | `EvaluationValue<XYZ>` |
| `BlockGripLocationComponent` | `FromDouble(value)` | `EvaluationValue<double>` |
| `BlockLinearParameter` | `FromDouble(value)` | `EvaluationValue<double>` |
| `BlockXYParameter` | `FromPoint(new XYZ(x, y, 0))` | `EvaluationValue<XYZ>` |
| `BlockPolarParameter` | `FromPoint(new XYZ(distance, angle, 0))` *(polar-space)* | `EvaluationValue<XYZ>` |
| `BlockRotationParameter` | `FromDouble(angle)` | `EvaluationValue<double>` |
| `BlockAlignmentParameter` | `FromDouble(angle)` | `EvaluationValue<double>` |
| `BlockPointParameter` | `FromPoint(displacement)` | `EvaluationValue<XYZ>` |
| `BlockFlipParameter` | `FromDouble(flip)` | `EvaluationValue<double>` |
| `BlockVisibilityParameter` | `FromDouble(stateIndex)` | `EvaluationValue<double>` |
| `BlockLookupParameter` | `FromPoint(displacement)` | `EvaluationValue<XYZ>` |

Bold/changed rows (the actual fix): `BlockGrip`, `BlockXYParameter`, `BlockPolarParameter`,
`BlockPointParameter`, `BlockLookupParameter` move from "X only" to "the whole value".

## Implementation steps

1. **Add `EvaluationValue.cs`** — `EvaluationValueType` + `EvaluationValue` + `EvaluationValue<T>`.
2. **Change `EvaluationExpression.CurrentValue`** to `EvaluationValue` (default `EvaluationValue.None`); update the XML doc.
3. **Update each `Evaluate` override** (11 classes) to `base.CurrentValue = EvaluationValue.From…(…)`; **add** the leaf `new EvaluationValue<T> CurrentValue => base.CurrentValue.As<T>();`.
4. **Fix the consumers** that assume `double?`:
   - `EvaluationGraphExamples.cs:104` → `expr.CurrentValue.Type == EvaluationValueType.None ? "-" : expr.CurrentValue.ToString()`.
   - `EvaluationTests.cs` → `assertClose`/`assertCloseEval`/`assertPointClose` helpers; the point test now asserts the whole point (X **and** Y); the polar test asserts the distance component.
5. **Update the docs** (`docs/articles/evaluation-graph.md`): the per-class `CurrentValue` table, the "Notes" bullet (drop the "simplification / X-component-as-representative" wording), and the "Verified results" table.
6. **Build + run the test suite** to confirm everything is green.

## What does *not* change

- **DXF/DWG group codes** — `CurrentValue` is an in-memory evaluation result, not a persisted field (the persisted value is `EvaluatedValue`, code `40`, which stays a `DxfValuePair`).
- **`EvaluationContext`** port model — stays `double`-keyed; only the per-node `CurrentValue` representative becomes richer.
- **`EvaluatedValue`** property — untouched.
- **The class hierarchy** — no class becomes generic; the intermediate `Block*` bases are untouched.

## Implementation status — ✅ done

- All **19** `Evaluate` overrides updated (11 parameters/grips + 8 actions) to write `base.CurrentValue = EvaluationValue.From…(…)`; each leaf carries a `new EvaluationValue<T> CurrentValue` view.
- `As<T>()` is **strict** (returns `None` when unset, throws on a shape mismatch) and uses a double-cast (`(EvaluationValue<T>)(object)EvaluationValue<…>.Of(…)`) to bridge a closed construct to the open generic (C# invariance).
- `EvaluationValue<T>.IsSet` is a dedicated flag (not `Equals(value, default)`) so a legitimate `0.0` reads as set.
- **`#`-format quirk:** the .NET 11 / C# 13 RC compiler rejects a `#` inside an interpolated-string format specifier (`$"({x:0.###}")` → `CS8076`). `EvaluationValue.ToString()` therefore formats with `F2` (no `#`).
- **Verified:** all 3 projects build (net9.0 / net6.0); all 6 `EvaluationTests` pass; full suite green except 2 pre-existing `ArcTests` floating-point failures (confirmed present on a clean checkout, unrelated to this change).
