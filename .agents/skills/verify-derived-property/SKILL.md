---
name: verify-derived-property
description: Before asserting a property (value type, shape, default) of one item by analogy to a sibling, or after a "systematic" change assigns a uniform value across many similar items, derive the property from the data's own type codes and source model instead of from the class.
whenToUse: When inferring a property of one item from a sibling (e.g. "all `BlockXxxParameter` share a base, so this one must have the same value type"), or when a refactor assigns a uniform value across many similar items, or when locking in a design claim derived from a base class rather than the on-disk data.
---

# Verify a derived property against the data, not the class

A property you *derive* for an item — a value's type, a shape, a default — is a property of the item's **data**, not of its C# base class. Do not conclude "this is a `Point`" because the class is a `Block1PtParameter`. Derive it from the data's own type codes and the source-of-truth model, and cross-check it against what you already recorded.

## The 4 rules

1. **Value type ≠ base class.** If a class inherits a geometric base (e.g. `Block1PtParameter` has a `Location` + grip) but its *value* is data-driven (a table, a resbuf, an ObjectARX `AcDbEvalVariant`), the value's type comes from the data's own type codes and the source model — **not** the base class. A 1-pt param's *location* being a point does not make its *value* a point.
2. **Verify data-derived types against the file's type codes and the source model** — not just the class. A 2-minute check ("my design says `Point`, but my research says this column is `95=1` = string — reconcile?") catches the mismatch before it ships.
3. **A suspiciously uniform pattern is a red flag, not reassurance.** In a "systematic" change, the item that *shouldn't* fit the pattern cleanly is exactly where the edge case lives. When everything fits too neatly, hunt for the one that was force-fit.
4. **Reconcile your own research notes against your design claims** before locking in a decision — don't let a design choice contradict a fact you already recorded.

## The case that motivated it

The **`BlockLookupParameter` value-type miss** (2026-09). During a "typed `CurrentValue`" refactor, every expression was assigned a shape (`Point` or `Scalar`). `BlockLookupParameter` was pattern-matched to `BlockPointParameter` because it is a `Block1PtParameter`, and concluded "it's a point displacement." That was wrong: a lookup parameter's **value** is the *lookup-table column's output* — the column's `95` group code is the value type (`40` = double, `1` = string), and the parameter record stores **no** value type at all. So the value is a **string or a scalar, never a point**. The string-column finding was already in the research notes (`docs/articles/evaluation-graph.md` — the `95` code and the string column) and was never reconciled with the "Point" shape that got assigned. The failure was *interpretation*, not incomplete research.

**Fix:** `BlockLookupParameter` now resolves its bound `BlockLookupAction` column (`param.ActionId` (94) == the action's node id) and stores the value in the column's own shape (string via `UnmatchedName`/`ParseCell`, or scalar). See `src/ACadSharp/Objects/Evaluations/BlockLookupParameter.cs`.

## How to apply

When you are about to write "X has type T because its base class is C":

1. Find the **on-disk type code** for the value in question (e.g. `95` for a lookup column) and read it in a real sample.
2. Check the **source model** (ObjectARX `AcDbEvalVariant`: `kDouble`/`kPoint2d`/`kPoint3d`/`kString`/`kLong`/…) — what types can this value actually take?
3. If your design claim and a note in your own research disagree, **stop and reconcile** before proceeding.
4. If the whole set of items fit the pattern *too* cleanly, re-inspect the one that fit least well.
