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
- **App-defined design tokens** (your own keys alongside FA's): the verified XAML forms,
  the broken ones, and the v12 API moves are in [§9](#9-app-defined-design-tokens-verified-avalonia-1213--fa-302).

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
- **Default accent is `#9b8aff`** (the Windows 11 purple) — verified at runtime:
  `AccentFillColorDefaultBrush` resolves to `#9b8aff` in **both** variants with no
  `CustomAccentColor` set. (The older Win10-era `#0078D4` is *not* the 3.0.2 default.)
  `CustomAccentColor` overrides the `PreferUserAccentColor` system value; setting it to
  `null` restores the system accent.
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

---

## 9. App-defined design tokens (verified: Avalonia 12.1.3 + FA 3.0.2)

End-to-end verified on headless Linux (`UseHeadless` + `UseSkia`): every "works" form below
compiled **and** resolved to its expected value at runtime, including a captured frame
confirming the shadows actually render. This is the pattern the Viewer should follow.

### Architecture

- **Theme-invariant tokens** (font sizes, corner radii) → plain resources in a
  `ResourceDictionary` with `x:Class` (+ a partial C# class calling
  `AvaloniaXamlLoader.Load(this)`).
- **Theme-dependent tokens** (colors, brushes) → `ResourceDictionary.ThemeDictionaries`
  with **plain keys** in `Default`/`Dark` sub-dictionaries — the exact FA 3.0.2 pattern
  (no `Theme=...` attribute on the keys).
- **Shadows** → **C# static `BoxShadows`** members, referenced in XAML via `{x:Static}` —
  *not* XAML resources (see "What does not").

### What works (verified)

| Form | Evidence |
| --- | --- |
| `<x:Double x:Key="BodyFontSize">14</x:Double>` (value resources) | compiles + resolves at runtime |
| `<CornerRadius x:Key="InPageCornerRadius">4</CornerRadius>` (plain element) | compiles + resolves (mirrors FA's 4/8) |
| `ThemeDictionaries` `Default`/`Dark` with plain `Color` / `SolidColorBrush` keys | per-variant: `#0F6CBD`/`#3D7EBF`, `#0F7B0F`/`#6CCB5F` |
| Cross-dictionary reference: app dict → FA dict, e.g. `Color="{DynamicResource SystemFillColorCaution}"` | resolves (Light `#9D5D00` / Dark `#FCE100`) |
| `BoxShadows` C# static + `BoxShadow="{x:Static local:ProbeTokens.CardShadow}"` | 2 `Border`s with non-empty `BoxShadow`; shadow visible in a captured 520×420 frame |
| `x:Class` on a `ResourceDictionary` + `AvaloniaXamlLoader.Load` | compiles + loads |

### What does not (verified broken)

| Form | Failure |
| --- | --- |
| `<BoxShadows x:Key="…">` as a XAML resource | **ICE** — `BoxShadows` has **no TypeConverter** in Avalonia 12.1.3 (reflection-verified), so there is no string attribute form either. Use a C# static + `{x:Static}` |
| `<x:CornerRadius x:Key="…">` | **AVLN2000** — the `x:` prefix resolves to a `System.*` type; `CornerRadius` is an Avalonia type. Use the plain `<CornerRadius>` element |
| `Avalonia.Styling.ResourceDictionary` (in C#) | **CS0234** — the type moved in v12 |
| `Padding="{StaticResource D},{StaticResource D2}"` / `Margin="{StaticResource D}"` where `D` is an `x:Double` token | **InvalidCastException at app construction** — the generated `XamlDynamicSetter` does a direct `(Thickness)` cast of the resolved value (a boxed `double`); a reference in a *mid-list* position additionally fails at compile time (**AVLN2005**). An `x:Double` token feeds only Double-typed properties (`Spacing`, `FontSize`, …); for `Margin`/`Padding` use literals normalized to the spacing scale |

### Avalonia 12 API moves (reflection-verified)

| v11-era spelling | Avalonia 12.1.3 reality |
| --- | --- |
| `Avalonia.Styling.ResourceDictionary` | **`Avalonia.Controls.ResourceDictionary`** (in `Avalonia.Base.dll`) |
| `Avalonia.Object` (base type) | **`Avalonia.AvaloniaObject`** |
| `AvaloniaXamlLoader` | **`Avalonia.Markup.Xaml.AvaloniaXamlLoader`** |
| `visual.GetVisualChildren()` (instance method) | **extension** `Avalonia.VisualTree.VisualExtensions.GetVisualChildren(visual)` — `using Avalonia.VisualTree;` |
| `Avalonia.Media.BoxShadows` | unchanged (in `Avalonia.Base.dll`) |
| `Control.ToolTip` (a property) | **removed** — use the `ToolTip.Tip` attached property (`AttachedProperty<object?>`); the `ToolTipService` is created in the `Application` ctor, so a XAML-set `ToolTip.Tip` is picked up |

### App token values (current, in `ACadSharp.Viewer/DesignTokens.axaml` + `DesignTokenShadows.cs`)

The app's own keys, normalized onto the NuGet's scales (the design language's
"8/4/0 corners, 12–72 type ramp, 4–48 spacing"). Keep in sync with the source:

| Token | Value | Scale it sits on |
| --- | --- | --- |
| `CaptionFontSize` | **12** | type ramp (12 14 16 18 20 …) |
| `BodyFontSize` | **14** | type ramp |
| `SubtitleFontSize` | **16** | type ramp |
| `CodeFontSize` | **12** | type ramp |
| `CardCornerRadius` | **4** | corner scale (8 4 0), in-page |
| `OverlayCornerRadius` | **8** | corner scale, top-level surfaces |
| `SwatchCornerRadius` | **4** | corner scale, in-page |
| `Space2/4/6/8/12` | **4 8 12 16 24** | spacing scale |
| `DesignTokenShadows.CardShadow` | two-part (cast `0 10 20 #26000000` + contact `0 3 6 #1A000000`) | elevation: Card |
| `DesignTokenShadows.TooltipShadow` | two-part, scaled up (cast `0 16 32 #2E000000` + contact `0 5 10 #1F000000`) | elevation: Tooltip |

### Headless runtime notes

- .NET on Linux: `Thread.SetApartmentState` throws `PlatformNotSupportedException` — the
  headless platform does not need STA, so do not set it.
- FA 3.0.2 with no OS accent (headless): the accent resolves to **`#9b8aff`** (Win11
  purple) in both variants — see §3.
