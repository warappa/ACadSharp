# The Fluent 2 design language (spec reference)

What Fluent 2 is, its principles, its concrete values, and the taste rules we apply on top.
Fluent 2 is Microsoft's current design language — the one behind **Windows 11** and **WinUI 3**.
**FluentAvalonia 3.x** (the `FluentAvaloniaUI` NuGet this repo uses, 3.0.2) is a rewrite that
targets this language, so the values here and the values in
[`fluentavalonia3-tokens.md`](fluentavalonia3-tokens.md) describe the same system from two sides:
this doc is *what Microsoft specifies*; the tokens doc is *what our NuGet actually ships*.

**When to read this:** making any deliberate UI decision (color, size, weight, spacing, corner,
shadow, motion, hierarchy) or judging whether a screen "looks right". The companion
[`.agents/skills/fluentavalonia`](../../.agents/skills/fluentavalonia/SKILL.md) skill is the
*how to wire it up in Avalonia* side.

> **Target users.** The ACadSharp Viewer's users are **developers** who want to debug block
> structure, view the evaluation graph, and read evaluated values. This is a **dense,
> data-heavy developer tool**: the design goal is *information density, clarity, and
> scannability* — not decoration. Windows 11 is the taste reference (a developer tool that
> feels calm, modern, and professional).

---

## 1. Design principles

The five principles Microsoft states for Windows 11 / Fluent 2. Each maps to a concrete
decision in a dense dev tool.

| Principle | Microsoft's words | What it means for us |
| --- | --- | --- |
| **Effortless** | Faster, more intuitive; easy to do what I want, with focus and precision. | Fast first-responsiveness; keyboard paths for the common flows; no hunting. |
| **Calm** | Softer, decluttered; fades into the background; warm, ethereal, approachable. | De-emphasize chrome (borders, dividers, secondary text); let the data be the loudest thing on screen. |
| **Personal** | Adapts to how I use the device; bends to my preferences. | Respect the user's light/dark theme; persist layout state (column widths, last selection). |
| **Familiar** | Refreshed look, but no learning curve; pick it up and go. | Keep the mental model of a standard IDE/graph view: tree on the left, graph in the middle, values on the right. |
| **Complete + Coherent** | Visually seamless across platforms. | One consistent token set; the same control styled the same way everywhere. |

---

## 2. Signature experiences

Fluent 2 expresses itself through seven signature experiences. Each has a **what** (the spec)
and a **do** (the concrete values or rule).

### 2.1 Color

**What.** Color indicates hierarchy and structure. It provides a *calming foundation* and
*emphasizes significant items only when necessary*. Two modes — **light** and **dark** — each
with a neutral palette auto-tuned for contrast. **Accent color** emphasizes important elements
and indicates an interactive object's state; it is used *sparingly*.

**Do (the rules):**
- **One accent color**, used sparingly. In a dev tool the accent marks *the* primary action and
  the selected/active state — not everything.
- **Hierarchy by lightness, not by hue.** In both modes, *darker = less important background*,
  *lighter/brighter = more important surface*. (In dark mode this inverts: brighter = more
  important.)
- **Contrast is a hard floor.** Text vs. its surface ≥ **4.5:1** (normal text), ≥ **3:1** for
  large text and functional (non-text) borders/indicators. Verify with the math, not the eye.
- **Never encode meaning by red/green alone** — ~8% of men are red-green colorblind. Pair color
  with a shape, label, or position. (Directly relevant: our graph palette uses
  red-ish/green-ish for node categories; each category must also differ by *label or shape*.)
- **Personal + cultural.** Honor the user's accent and theme; note that color meaning varies by
  culture (blue = virtue in some, mourning in others).

The exact per-mode neutral palette values are in [`fluentavalonia3-tokens.md`](fluentavalonia3-tokens.md)
(the FA color token table).

### 2.2 Typography

**What.** Typography's job is to communicate information; its style should never get in the way.
Use **one font** throughout — the default **Segoe UI Variable** (a variable font with two axes:
**weight** `wght` 100–700 and **optical size** `opsz`, automatic, 8–36pt). On non-Windows,
Segoe UI/Variable is unavailable and the platform default font is used instead.

**Do — the type ramp** (size/line-height in effective px; weight as named). This is the single
most important table in the doc — every piece of text maps to one row.

| Style | Weight | Size / line |
| --- | --- | --- |
| Caption | Regular (Small) | 12 / 16 |
| Body | Regular (Text) | 14 / 20 |
| Body (strong) | SemiBold (Text) | 14 / 20 |
| Subtitle | SemiBold (Display) | 20 / 28 |
| Title | SemiBold (Display) | 28 / 36 |
| Title (large) | SemiBold (Display) | 40 / 52 |
| Display | SemiBold (Display) | 68 / 92 |

Microsoft's best-practice line: **Regular weight for most text, SemiBold for titles; minimum
12 px Regular / 14 px SemiBold** (smaller is illegible in some languages). **Bold and Italic are
not in the ramp** — use SemiBold for emphasis; drop Italic (it reduces legibility, e.g. for
dyslexia).

Additional typography rules:
- **Casing:** sentence case for all UI text, including titles.
- **Alignment:** left by default; center only in rare cases (e.g. text under an icon).
- **Line length:** 50–60 characters per line ideal; not fewer than 20, not more than 60.
- **Truncation:** ellipsis by default; clip only in rare cases.
- **Monospace for data.** Values, IDs, and code are **monospace + `tabular-nums`** so columns
  align. (This is a dev-tool extension of the ramp, not a WinUI default — see §3.)

> **Note on the ramp vs. the NuGet.** Microsoft's full ramp also lists *Body Large* (18/24) and
> *Body Large Strong* (18/24). The FluentAvalonia 3.0.2 `TypographyPage` samples a slightly
> reduced set (Caption 12/16, Body 14/20, BodyStrong 14/20, Subtitle 20/28, Title 28/36,
> TitleLarge 40/52, Display 68/92). Use the values the NuGet actually exposes; add 18/24
> manually if a screen needs a step between Body and Subtitle.

### 2.3 Geometry (shapes)

**What.** Shape, size, and position, crafted for a soft, calm, approachable feel. **Progressive
rounding** + **consistent gutters**.

**Do — the corner-radius scale** (three levels):

| Radius | Usage |
| --- | --- |
| **8 px** | Top-level containers: app windows, flyouts, dialogs, overlays. |
| **4 px** | In-page elements: Button, CheckBox, ComboBox, TextBox, ListView backplates; and *bar* elements (ProgressBar, ScrollBar, Slider). |
| **0 px** | Straight edges that touch other straight edges; window corners when snapped/maximized. |

**When not to round:** touching elements inside one container (the two halves of a SplitButton —
no gap when they contact); a flyout connected to its invoker on one side. FA exposes this as
two globals: `ControlCornerRadius` = **4** and `OverlayCornerRadius` = **8** (see the tokens doc).

### 2.4 Elevation & layering

**What.** Depth. When two surfaces overlap, the higher one is rendered on top and casts a shadow.
Shadows + contour (outline) *subtly* communicate elevation and draw focus. Higher elevation =
larger, softer shadow.

**Do — the elevation values** (a discrete scale; do not invent values between these):

| Surface | Elevation value | Stroke |
| --- | --- | --- |
| Window / Dialog | 128 | 1 |
| Flyout | 32 | 1 |
| Tooltip | 16 | 1 |
| Card | 8 | 1 |
| Control | 2 | 1 |
| Layer | 1 | 1 |

**Two-layer app structure.** Every app has a **base** layer (menus, commands, navigation — the
foundation) and a **content** layer (the central experience; contiguous or split into cards).
This maps directly to our Viewer: *base* = the top command bar + the left tree pane;
*content* = the graph canvas + the right value pane.

Control **states** shift elevation subtly: *rest* (2), *hover* (2), *pressed* (1).

**Rule:** use shadows *purposefully*, not decoratively. Overusing them creates visual noise and
diminishes their signal. In a dense tool, prefer **contour + lightness** over drop shadows for
in-page separation; reserve real elevation for the few genuinely floating surfaces.

### 2.5 Materials

**What.** Visual effects that make surfaces resemble real artifacts. Two families: **occluding**
(beneath controls) and **transparent** (over immersive surfaces).

| Material | Character | Use |
| --- | --- | --- |
| **Mica** (new in Win11) | *Opaque*, subtly tinted by the user's **desktop background color**; mode-aware; indicates window focus (active/inactive) built in. | App **base/background** layer. |
| **Acrylic** | *Semi-transparent* frosted-glass; brighter + more translucent in Win11; mode-aware. | **Transient, light-dismiss** surfaces: flyouts, context menus. |
| **Smoke** | Always **translucent black** (not mode-aware); dims what's beneath. | **Modal** surfaces: signals a blocking dialog. |

**For a dense dev tool** the practical guidance: a calm, near-solid base (Mica-like) with
*subtle* material on the floating layers; never let material fight with the data for attention.

### 2.6 Motion

**What.** How the interface animates and responds. **Reactive, direct, context-appropriate** —
provides feedback and reinforces spatial way-finding.

**Do — the five principles:**
- **Connected** — elements that move/resize connect seamlessly between states (a window stays
  "the same window" across floating/snapped/maximized).
- **Consistent** — surfaces sharing an entry point invoke and dismiss the same way (all
  taskbar flyouts slide up to open, down to close).
- **Responsive** — the system visibly acknowledges different input/posture.
- **Delightful** — brief, fleeting moments of joy with purpose (minimize = icon bounces down).
- **Resourceful** — **reuse existing control animations; avoid custom ones.** Use the platform's
  page-transition, connected-animation, and micro-interaction resources.

**Timing + easing** (fast, direct, context-appropriate):

| Purpose | Ease | Timing | Used for |
| --- | --- | --- | --- |
| Direct entrance (fast-in) | `cubic-bezier(0,0,0,1)` | 167 / 250 / 333 ms | position, scale, rotation |
| Existing elements (point-to-point) | `cubic-bezier(0.55,0.55,0,1)` | 167 / 250 / 333 ms | position, scale, rotation |
| Direct exit (fast-out) | `cubic-bezier(0,0,0,1)` | 167 ms | always **combined with fade-out** |
| Gentle exit (soft-out) | `cubic-bezier(1,0,1,1)` | 167 ms | position, scale |
| Bare minimum (fade) | linear | 83 ms | opacity |
| Strong entrance (elastic, 3 keyframes) | `(.85,0,0,1)` 167 → `(.85,0,.75,1)` 167 → `(.85,0,0,1)` 333 ms | — | position, scale |

**Dev-tool rule of thumb:** most UI state changes here are **≤ 250 ms**, direct, and often just
a **fade** (83 ms) or a **slide** (167–250 ms). Motion that would take > 400 ms is too slow to
feel responsive (the Doherty threshold, §3). Respect `prefers-reduced-motion`.

### 2.7 Iconography

**What.** Symbols that help users understand and navigate. Every system-icon glyph was
redesigned in Win11 for **softer geometry and more modern metaphors**. Use the **Segoe Fluent
Icons** font (in Avalonia, the **FASymbol / FASymbolIcon** — 442 values, Segoe-Fluent-compatible;
see the tokens doc).

**Do:** one icon system, consistent stroke weight, ~24 px nominal grid, meaningful (not
decorative) glyphs. Prefer a **label** over a glyph for anything the user must *act* on.

---

## 3. Taste rules for a dense developer tool

Fluent 2 tells us *what good looks like* in a consumer OS. Our tool is denser and more
utilitarian, so we layer on the rule-based "taste" that the design-psychology literature
distills. These are the concrete, checkable rules — the "principal" judgment made explicit.

### 3.1 Spacing & type scales (Refactoring UI)

Use the scales, not ad-hoc values. The current ad-hoc `FontSize`s in the Viewer
(11/12/14/15/16) are the exact anti-pattern this fixes.

- **Spacing scale (px):** `4 8 12 16 24 32 48 64 96 128 192 256 384 512 640 768`.
- **Type scale (px, rem-only — never `em`):** `12 14 16 18 20 24 30 36 48 60 72`.
- **Weights:** 400/500 for body, 600/700 for emphasis; **never below 400**.
- **De-emphasis = lighter color or smaller size, never lighter weight.** To push something into
  the background, drop its *contrast* or *size* — don't thin its weight.
- **Greys:** 8–10 neutral shades, starting dark; **accents:** 5–10 tints 100→900 around a 500
  base, written as **HSL, never runtime `lighten()`/`darken()`**.
- **Shadows:** 5 discrete elevations, each **two-part** — a *cast* (`0 10px 20px hsla(0,0%,0%,.15)`)
  + a *contact* (`0 3px 6px hsla(0,0%,0%,.10)`).

### 3.2 The "why" (Laws of UX, the ones that bite a dense tool)

From the 29 laws, the ones that matter most for a data-heavy developer surface:

- **Fitts** — a target's size + distance determine acquisition time. *Big, close targets for
  primary actions; keep the graph's interactive nodes large enough to hit.*
- **Hick** — decision time grows with the number of choices. *Group commands; don't put 20
  buttons in one row.*
- **Miller / Chunking** — working memory holds ~7±2 items; **chunk** beyond that. *Group graph
  nodes and parameters into sub-graphs / expandable groups; paginate or fold long lists.*
- **Von Restorff** — the distinctive item is remembered. *Make the one selected node / the one
  error stand out (accent + contour); keep the rest uniform.*
- **Doherty threshold (~400 ms)** — the mind stays in "flow" only if the system responds in
  < ~400 ms; **progressive feedback** (skeleton, spinner, partial result) keeps the user engaged
  beyond it. *Evaluation results that take > 400 ms must show progress.*
- **Cognitive load** — minimize what the user must hold in working memory. *One thing at a time;
  keep related data adjacent; don't force cross-referencing across panes.*
- **Jakob's law** — users expect your site to work like all the other sites they use. *Follow
  the standard IDE/graph-tool layout; don't reinvent navigation.*
- **Gestalt** — the grouping *mechanism*: **Proximity** (near = related), **Similarity**
  (same look = same meaning), **Closure**, **Continuity**, **Figure/Ground**, **Common region**,
  **Uniform connectedness**. *These are how you group a dense canvas without drawing boxes
  everywhere.*

### 3.3 The "usability" (Nielsen's 10 heuristics — the hard ones for a dense tool)

Of Nielsen's ten, the ones a dense developer tool most often violates:

- **#1 Visibility of system status** — *always* show evaluation progress, selection state, and
  which values are stale vs. fresh.
- **#5 Error prevention** — confirm destructive actions; make the graph hard to mis-edit.
- **#6 Recognition rather than recall** — *show* available node types, parameters, and recent
  values; don't make the user remember them.
- **#7 Flexibility & efficiency of use** — keyboard shortcuts, accelerators, and "power-user"
  paths alongside the discoverable ones.
- **#8 Aesthetic & minimalist design** — *only* the information that helps the task; every
  extra element competes with the data. (This is the "Calm" principle, made a test.)

*(#10 "Help and documentation" was added in 2020; the other five heuristics are the everyday
core.)*

### 3.4 The "web / implementation" layer (Vercel web-interface guidelines)

The rules that map directly onto a data-dense desktop view:

- **`tabular-nums`** on every numeric column (values, IDs, addresses) so they align.
- **The URL/state reflects the screen** — deep-link to the selected block/node.
- **Virtualize** any list > 50 rows (the parameter list, the value table).
- **`:focus-visible`** — never `outline: none` without a replacement; keyboard focus must be
  visible.
- **`prefers-reduced-motion`** — disable decorative motion when requested.
- **`min-width: 0`** on flex children (prevents overflow blowouts in dense rows).
- **Specific button labels** — "Evaluate block 3" not "Go".

### 3.5 The bottom line

For a dense developer tool, the stack is:

1. **Refactoring UI** values = *what good looks like* (the concrete scales).
2. **Fitts / Hick / Miller / Chunking / Von Restorff / Doherty / Jakob's / cognitive load** =
   *why* (the user's attention and memory).
3. **Nielsen #1 / #5 / #6 / #7 / #8** = *usability* (does the tool actually work for a pro).
4. **Gestalt** = *the grouping mechanism* (how you organize a dense canvas).

---

## 4. Sources

Microsoft primary sources (the authoritative Fluent 2 / Windows 11 spec):

- **Design hub** — <https://learn.microsoft.com/windows/apps/design/>
- **Design principles** — <https://learn.microsoft.com/windows/apps/design/design-principles>
- **Typography** — <https://learn.microsoft.com/windows/apps/design/signature-experiences/typography>
- **Color** — <https://learn.microsoft.com/windows/apps/design/signature-experiences/color>
- **Geometry** — <https://learn.microsoft.com/windows/apps/design/signature-experiences/geometry>
- **Elevation & layering** — <https://learn.microsoft.com/windows/apps/design/signature-experiences/layering>
- **Materials (Mica/Acrylic/Smoke)** — <https://learn.microsoft.com/windows/apps/design/signature-experiences/materials>
- **Motion** — <https://learn.microsoft.com/windows/apps/design/signature-experiences/motion>
- **Fluent 2 design language (interactive)** — <https://fluent2.microsoft.design/>
- **WinUI 3 Gallery (reference implementation, 3.6k stars)** — <https://github.com/microsoft/WinUI-Gallery>

Taste / rules literature:

- **Refactoring UI** — <https://refactoringui.com/> (the 50-chapter "what good looks like").
- **Laws of UX** — <https://lawsofux.com/> (29 laws).
- **Nielsen's 10 usability heuristics** — <https://www.nngroup.com/articles/ten-usability-heuristics/> (1994, rev. Jan 2024).
- **Gestalt principles** — Proximity, Similarity, Closure, Continuity, Figure/Ground, Common region, Uniform connectedness (+ Prägnanz, Common fate).
- **Vercel Web Interface Guidelines** — <https://github.com/vercel-labs/web-interface-guidelines> (~70 rules / 14 categories).
