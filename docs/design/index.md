# Design

The UI design language for the ACadSharp Viewer.

The Viewer is built on **Avalonia 12** with the **FluentAvaloniaUI 3.0.2** NuGet — a rewrite
that targets Microsoft's **Fluent 2** design language (the one behind Windows 11 and WinUI 3).
Users are **developers** who debug block structure, view the evaluation graph, and read
evaluated values: the design goal is **information density, clarity, and scannability**, with
Windows 11 as the taste reference.

Two documents:

| Document | What it is |
| --- | --- |
| [`fluent2-design-language.md`](fluent2-design-language.md) | **The spec** — Fluent 2's principles, its seven signature experiences with concrete values (type ramp, corner radii, elevation, motion, materials, color), the Windows 11 reference, and the taste rules distilled on top for a dense dev tool. *What Microsoft specifies.* |
| [`fluentavalonia3-tokens.md`](fluentavalonia3-tokens.md) | **The ground truth** — what the installed FluentAvaloniaUI 3.0.2 package actually ships: theme setup, the verified color table (Light/Dark/HighContrast), core global tokens, the accent mechanism, the 442-value `FASymbol` catalog, the 1737-key inventory, and the gotchas. *What our NuGet actually does.* |

They are complementary: the first is the *target*, the second is the *reality we build on*.
The two line up almost exactly (e.g. `ControlCornerRadius=4` / `OverlayCornerRadius=8` match
the Fluent 2 geometry spec) — where they diverge, the tokens doc's §8 "Gotchas" flags it.

**When to read this:** making any deliberate UI decision (color, size, weight, spacing, corner,
shadow, motion, hierarchy), judging whether a screen "looks right", or wiring a new FluentAvalonia
control. The companion [`.agents/skills/fluentavalonia`](../../.agents/skills/fluentavalonia/SKILL.md)
skill covers the Avalonia *how-to* side.
