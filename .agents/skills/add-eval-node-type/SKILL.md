---
name: add-eval-node-type
description: Add a new dynamic-block evaluation node type (an ObjectARX AcDbEvalXxx analog) to the ACadSharp evaluation engine — the complete 8-touch-point recipe including DXF and DWG IO.
whenToUse: When you must add or fix an evaluation node type (a new BlockXxxParameter / BlockXxxAction / evaluation component) or add a new block-parameter object with DXF+DWG read/write support.
---

# Adding a new evaluation node type (8 touch points)

The engine lives in `src/ACadSharp/Objects/Evaluations/` (mirroring ObjectARX `AcDbEval*`). Reference material: [`docs/articles/evaluation-engine.md`](../../docs/articles/evaluation-engine.md) (value model, engine semantics, class hierarchy, per-class `Evaluate` formulas, verified sample results); [`docs/articles/evaluation-graph.md`](../../docs/articles/evaluation-graph.md) (the AutoCAD on-disk specification).

## The 8 touch points (in order)

1. `DxfFileToken.cs` — `ObjectXxx = "XXX"` (the DXF object name).
2. `DxfSubclassMarker.cs` — `Xxx = "AcDbXxx"` (the subclass marker).
3. `Objects/Evaluations/Xxx.cs` — the class. For a parameter, inherit `BlockParameter`; add `[DxfName]`/`[DxfSubClass]` attrs, a `Value` with a `[DxfCodeValue]`, an `Evaluate` that writes the `"Value"` port (`context.SetValue(this.Id, "Value", EvaluationValue.FromX(...)` + set `base.CurrentValue`), a typed `CurrentValue` override, a `GetDefaultValue()` override (**required** — the method is abstract: a stateful node returns its initial value, e.g. a grip's zero `Displacement`; a stateless node returns `EvaluationValue.None`), and `GetDxfClass()` (ItemClassId=499, MaintenanceVersion=55, ProxyFlags=EraseAllowed|CloningAllowed|DisablesProxyWarningDialog).
4. `IO/Templates/CadXxxTemplate.cs` — the template (`: CadBlockParameterTemplate`, typed property + parameterless and object-taking ctors).
5. `IO/DXF/DxfStreamReader/DxfObjectsSectionReader.cs` — `readXxx` (specific codes; `default` falls through to `readBlockParameter`) + a dispatch case.
6. `IO/DXF/DxfStreamWriter/DxfObjectsSectionWriter.cs` — `writeXxx` (`writeBlockParameter` + `Write(100, marker)` + per-code `Write`) + a dispatch case.
7. `IO/DWG/DwgStreamReaders/DwgObjectReader.Objects.cs` — `readXxx` + a dispatch case in `DwgObjectReader.cs`.
8. `IO/DWG/DwgStreamWriters/DwgObjectWriter.Objects.cs` — `writeXxx` + a dispatch case.

## Gotchas

- `GetDefaultValue()` is **abstract** — a missing default is a compile error (CS0534). Stateful → its initial value (a grip's zero `Displacement`); stateless → `EvaluationValue.None`.
- `WriteBitLong`/`ReadBitLong` are the DWG 64-bit primitives, but `WriteBitLong` takes an **`int`** — cast a `long` value.
- **Lookup actions** are **excluded from the forward evaluation** (reachable only via `flag=4` reverse edges, which the topological order skips) — do not expect a lookup's `CurrentValue` after a forward `evaluate()`; it is a separate reverse pass.

## Then

Build all 6 TFMs, add tests (evaluate → assert `CurrentValue.Type` + payload; a DXF round-trip), run the suite, commit.

## Sanity check

```bash
NUGET_PACKAGES=$PWD/.tmp-nuget DOTNET_ROLL_FORWARD=Major dotnet run --project src/ACadSharp.Examples -- eval <file>
```

The 10 `samples/dynamic-blocks/BLOCK<NAME>PARAMETER.*` pairs must still evaluate to the verified values documented in `docs/articles/evaluation-engine.md` (e.g. linear 5, polar 9.082, rotation 1.571).
