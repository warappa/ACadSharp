# FluentAvalonia 3.0.2 — local tokens (ground truth)

What the `FluentAvaloniaUI` **3.0.2** NuGet (the package this repo actually uses, on
Avalonia 12 / `net10.0`) **ships**: its theme setup, its resource keys, its verified color
table, its accent mechanism, and its `FASymbol` icon catalog. Everything here is read from
the installed package, not from the web — it is the concrete counterpart to
[`fluent2-design-language.md`](fluent2-design-language.md).

> **Version note.** The original question was about "FluentAvalonia 2.3.0". The repo uses
> **3.0.2**, which is the rewrite targeting the Fluent 2 / Windows 11 design language. The
> WinUI 3 WinRT library source is **not** in the public `microsoft/microsoft-ui-xaml`
> monorepo (its `src/` contains only `XamlCompiler/`, `projection/`, `BuildTools.sln`), so the
> authoritative *citable* spec is Microsoft Learn and the ground-truth *implementation* is this
> NuGet.

**When to read this:** wiring a new control, picking a brush by name, checking whether a
token exists, or auditing the palette. Use the
[`.agents/skills/fluentavalonia`](../../.agents/skills/fluentavalonia/SKILL.md) skill for the
Avalonia *how-to* patterns.

---

## 1. Theme setup (correct pattern)

From `src/ACadSharp.Viewer/App.axaml`:

```xaml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:sty="using:FluentAvalonia.Styling"
             RequestedThemeVariant="Dark">
  <Application.Styles>
    <sty:FluentAvaloniaTheme />
    <!-- additional styles ... -->
  </Application.Styles>
</Application>
```

- The theme is a **style**, `<sty:FluentAvaloniaTheme />` — **no `x:Key`**.
- `RequestedThemeVariant` is set on the `Application` root.
- There is **no `IsDark`** property on the theme. Read the variant with
  `Application.Current.RequestedThemeVariant`.
- `FluentAvaloniaTheme` properties: `CustomAccentColor` (`Color?`, highest precedence),
  `PreferSystemTheme`, `PreferUserAccentColor`, `TextVerticalAlignmentOverrideBehavior`
  (default `EnabledNonWindows`), `UseSystemFontOnWindows`, `MergedDictionaries`.

**Theme dictionaries.** Three: **Default = Light**, **Dark**, **HighContrast**.
(Note: in WinUI, *Default = Dark*; in FluentAvalonia, *Default = Light*.)
The `HighContrast` dictionary is a placeholder — its values are all `#FF0000` (black).

---

## 2. Core global tokens (verified)

From `Fluentv2.axaml` (6042 lines) — the globals a custom style most often keys off:

| Key | Value | Line |
| --- | --- | --- |
| `ControlCornerRadius` | **4** | 5972 |
| `OverlayCornerRadius` | **8** | 5973 |
| `ControlContentThemeFontSize` | **14** | 5975 |
| `ContentControlThemeFontFamily` | `"Default"` | 5969 |
| `TextControlThemeMinHeight` | 32 | — |
| `ButtonPadding` | `11,5,11,6` | — |
| `FlyoutContentThemePadding` | `12,11,12,12` | — |
| `ChevronDownGlyph` | `&#xE70D;` | — |

Two important caveats:
- `ContentControlThemeFontFamily="Default"` is **commented out** in the file → the font is the
  **default system font**, **not** Segoe UI Variable. On Windows the system default *is*
  Segoe UI Variable; on non-Windows it is the platform default. To force it, re-enable the
  key and set `"Segoe UI Variable"` (Windows only).
- `ControlCornerRadius=4` + `OverlayCornerRadius=8` **exactly match** the Fluent 2 geometry
  spec (4 px in-page, 8 px overlay) from the design-language doc.

---

## 3. Accent mechanism

- The `SystemAccentColor*` brushes are **not** defined in the XAML — they are **derived in C#**
  by `FluentAvaloniaTheme`.
- Base accent (Win11 default) is **`#0078D4`**; `CustomAccentColor` overrides the
  `PreferUserAccentColor` system value; setting it to `null` restores the system accent.
- **Per-theme brush mapping** (verified in the dictionaries):
  - **Light theme** → `SystemAccentColorDark1/2/3` (e.g. lines 17–19, 58–60).
  - **Dark theme** → `SystemAccentColorLight2/3` (e.g. lines 1997–2040).

i.e. the *same logical* accent resolves to a **darker** tint on light backgrounds and a
**lighter** tint on dark backgrounds, so accent elements keep contrast in both modes.

---

## 4. Verified color table

From `Fluentv2Colors.axaml` (302 lines), line-verified. These are the neutral palette
backing the design-language "color" section.

### Light

| Key | Value |
| --- | --- |
| `TextPrimary` | `#E4000000` |
| `TextSecondary` | `#9E000000` |
| `TextTertiary` | `#72000000` |
| `TextDisabled` | `#5C000000` |
| `TextInverse` | `#FFFFFF` |
| `ControlFillDefault` | `#B3FFFFFF` |
| `ControlFillSecondary` | `#80F9F9F9` |
| `ControlFillTertiary` / `ControlFillDisabled` | `#4DF9F9F9` |
| `ControlStrokeDefault` | `#0F000000` |
| `ControlStrokeSecondary` | `#29000000` |
| `CardStrokeDefault` | `#0F000000` |
| `CardStrokeDefaultSolid` | `#EBEBEB` |
| `SurfaceStrokeDefault` | `#66757575` |
| `FocusStrokeOuter` | `#E4000000` |
| `FocusStrokeInner` | `#B3FFFFFF` |
| `CardBackgroundFillDefault` | `#B3FFFFFF` |
| `LayerFillDefault` | `#80FFFFFF` |
| `LayerFillAlt` | `#FFFFFF` |
| `SolidBackgroundFillColorBase` | `#F3F3F3` |
| `SolidBackgroundFillColorSecondary` | `#EEEEEE` |
| `SolidBackgroundFillColorTertiary` | `#F9F9F9` |
| `SolidBackgroundFillColorQuarternary` | `#FFFFFF` |

### Dark

| Key | Value |
| --- | --- |
| `TextPrimary` | `#FFFFFF` |
| `TextSecondary` | `#C5FFFFFF` |
| `TextTertiary` | `#87FFFFFF` |
| `TextDisabled` | `#5DFFFFFF` |
| `TextInverse` | `#E4000000` |
| `ControlFillDefault` | `#0FFFFFFF` |
| `ControlFillInputActive` | `#B31E1E1E` |
| `SolidBackgroundFillColorBase` | `#202020` |
| `SolidBackgroundFillColorSecondary` | `#1C1C1C` |
| `SolidBackgroundFillColorTertiary` | `#282828` |
| `SolidBackgroundFillColorQuarternary` | `#2C2C2C` |
| `LayerFillDefault` | `#4C3A3A3A` |
| `CardStrokeDefaultSolid` | `#1C1C1C` |
| `FocusStrokeOuter` | `#FFFFFF` |
| `FocusStrokeInner` | `#B3000000` |

### HighContrast

All values are `#FF0000` — a **placeholder** black, not a real high-contrast palette. Do not
design against it.

> **Pattern to note:** the hierarchy is carried by **alpha over black/white**
> (`#E4…`/`#9E…`/`#72…`/`#5C…` in light; `#FFFFFF`/`#C5…`/`#87…`/`#5D…` in dark) —
> *darker = less important*. This is the "hierarchy by lightness" rule made concrete.

---

## 5. FASymbol icon catalog

- **`FASymbol`** is an **enum**; **`FASymbolIcon`** is the **control** that renders it.
- The catalog has **exactly 442 values** (442 rows in `FASymbol-values.txt`; 450 lines incl.
  the header).
- First value: `Previous` (`57600`); last: `CodeHTML` (`1016116`).
- It is derived from the **FluentUI Icons** font and is **compatible with the WinUI /
  Segoe Fluent Icons code points** — so an icon chosen in the WinUI gallery works here.

Related icon controls: `FASymbol`, `FASymbolIcon`, `FAFontIcon`, `FAPathIcon`, `FABitmapIcon`,
`FAImageIcon`.

---

## 6. Resource-key inventory

- **1737 unique `x:Key` resource keys** in `Fluentv2.axaml` (verified `wc -l` = 1737).
- These are the named brushes/sizes/strings a custom style can reference. When in doubt,
  grep the key list (`.tmp-fluent2/v2-resource-keys.txt`) rather than inventing a name.

---

## 7. FA\* control inventory (user-facing, 3.0.2)

The controls available for the Viewer, grouped:

- **Window / chrome:** `FAAppWindow`, `FAAppWindowTitleBar`.
- **Navigation:** `FANavigationView` (+`Item`/`Header`/`Pane`), `FABreadcrumbBar` (+`Item`).
- **Menus / flyouts:** `FAMenuFlyout` (+`Item`/`Separator`/`SubItem`), `FARadioMenuFlyoutItem`,
  `FAToggleMenuFlyoutItem`, `FACommandBar` family.
- **Inputs:** `FAComboBox` (+`Item`), `FANumberBox`, `FAExpanderExt`, `FASettingsExpander` (+`Item`).
- **Dialogs:** `FAContentDialog` (+`Button`/`Placement`/`Result`), `FADialogHost`,
  `FATaskDialog` family.
- **Feedback / status:** `FAInfoBar`, `FAInfoBadge`, `FAProgressRing`, `FATeachingTip`.
- **Content / tabs / layout:** `FATabView` (+`Item`/`CloseButtonOverlay`), `FANonVirtualizingLayout`,
  `FAUniformGridLayout`, `FAStackLayout`, `FAFlowLayout`, `FAItemsRepeater`, `FAConnectedAnimation`,
  `FAFRAME`, `FAUISettings`.
- **Icons:** `FASymbol`, `FASymbolIcon`, `FAFontIcon`, `FAPathIcon`, `FABitmapIcon`, `FAImageIcon`.

Already in use in the Viewer: `FASymbolIcon`, `FAMenuFlyout`, `FARadioMenuFlyoutItem`,
`FAInfoBar`, `FAProgressRing`, `FATeachingTip`.

---

## 8. Gotchas (the ones that will bite)

- **Default theme = Light** (not Dark, as in WinUI). Set `RequestedThemeVariant` explicitly.
- **No `IsDark`** — use `Application.Current.RequestedThemeVariant`.
- **`ContentControlThemeFontFamily="Default"`** = default system font, *not* Segoe UI Variable
  (the key is commented out in the shipped XAML).
- **`SystemAccentColor*` is C#-derived**, not a XAML resource — you cannot `ThemeResource` it
  by name in the same way; it resolves per theme (Dark tints in light, Light tints in dark).
- **`TextVerticalAlignmentOverrideBehavior`** (`Disabled` / `EnabledNonWindows` [default] /
  `AlwaysEnabled`) is only respected **at app start**.
- **HighContrast dictionary is a placeholder** (all `#FF0000`) — do not build on it.
- **Don't re-derive the type ramp.** Use the NuGet's `TypographyPage` values (Caption 12/16,
  Body 14/20, BodyStrong 14/20, Subtitle 20/28, Title 28/36, TitleLarge 40/52, Display 68/92);
  Microsoft's full ramp adds 18/24 steps that the NuGet does not expose.
