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

**The enum values are logical IDs, NOT the font's code points (verified 2026-09-29).**
Empirically rendering the bundled font (`avares://FluentAvalana/Fonts/FluentAvalana.ttf`,
the *bare* key — `#SymbolIconManager`/`#Symbols`/`#FluentAvalana` suffixes all fail with
`Could not create glyphTypeface`) shows the raw code points map to *different* glyphs than
the enum values imply:

| Raw code point | Renders as | 2023/2026 upstream name at that point |
| --- | --- | --- |
| `0xE18A` (57626) | chevron `>` | (upstream `search_24` is elsewhere) |
| `0xF4A2` (62626) | smiley face | upstream `info_16` |
| `0xF4A4` (62628) | check-in-circle | upstream `info_24` |
| `0xEA7C` (60028) | `⋯` three dots | upstream `search_16` |
| `0xF690` (63120) | `.notdef` | upstream `search_24` |

So `FASymbolIcon` **remaps** the enum value to an internal code point (e.g. `Find`=57626
renders the magnifier even though raw `0xE18A` is a chevron). The bundled font is a
**custom-built** FontForge font ("SymbolIconManager", 2023-03-29) with its **own** code
point assignments — it is **not** the upstream `FluentSystemIcons-Regular` at upstream
code points. The font is also **larger than the enum**: a full sweep of `0xE000–0xF8FF`
finds thousands of glyphs (brand logos, weather, clocks, moon phases, badges) far beyond
the 442-value catalog.

**Practical consequences:**
- To use an icon that is **in the enum** → use `FASymbolIcon Symbol="..."` (the official
  API). Do not try to reproduce it with a raw `FontIcon` code point.
- To use an icon that is **not in the enum** (e.g. the **info i-in-circle**) → reference
  the bundled font directly with a `FontIcon`/`TextBlock` at the font's actual code point.
  The info i-in-circle is at **`0xF05A`** (verified by rendering). Its circled-badge
  neighbors: `0xF055` plus, `0xF056` minus, `0xF057` ×, `0xF058` check, `0xF059` ?,
  `0xF06A` !.
- The 2023 font does **not** contain the tree icons (`cube_tree`, `list_bar_tree`,
  `text_bullet_list_tree`) or `question` — so for expand/collapse, `ChevronDown`/
  `ChevronUp` are the correct (only tree-ish) enum choices.

Related icon controls: `FASymbol`, `FASymbolIcon`, `FAFontIcon`, `FAPathIcon`, `FABitmapIcon`,
`FAImageIcon`.

**`SearchBox`:** the 3.0.2 control inventory (§7) has **no `SearchBox`** (it existed in the
1.x/2.x era and was dropped in the 3.0 rewrite). The idiomatic search composition is a
standard `TextBox` + `FASymbolIcon Symbol="Find"` (a real magnifier).

---

## 6. Resource-key inventory

- **1737 unique `x:Key` resource keys** in `Fluentv2.axaml` (verified `wc -l` = 1737).
- These are the named brushes/sizes/strings a custom style can reference. When in doubt,
  grep the key list (`.tmp-fluent2/v2-resource-keys.txt`) rather than inventing a name.

---

## 7. FA\* control inventory (user-facing, 3.0.2)

The controls available for the Viewer, grouped:

- **Window / chrome:** `FAAppWindow`, `FAAppWindowTitleBar` *(not used — the Viewer draws
  its own caption strip; see §10)*.
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
- **`ExtendClientAreaTitleBarHeightHint` overrides the theme's titlebar height** (it feeds
  `WindowDrawnDecorations.TitleBarHeightOverride`), so with client-side decorations it makes
  Avalonia draw its own caption on top of ours. Leave it unset — see §10.
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
"8/4/0 corners, 12–72 type ramp, 4–48 spacing"). Keep in sync with the source.
The chrome styles that consume them live in
[`ACadSharp.Viewer/FluentStyles.axaml`](../../src/ACadSharp.Viewer/FluentStyles.axaml)
— class-keyed styles (`.caption`, `.pane`, `.statusBar`, `.emptyTitle`, the DataGrid
density) merged in `App.axaml` **after** `<sty:FluentAvaloniaTheme />`.

| Token | Value | Scale it sits on |
| --- | --- | --- |
| `CaptionFontSize` | **12** | type ramp (12 14 16 18 20 …) |
| `BodyFontSize` | **14** | type ramp |
| `SubtitleFontSize` | **20** | type ramp (the ramp's Subtitle is 20/28 SemiBold; 16 was off-ramp) |
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

---

## 10. App-owned window chrome (Avalonia 12 client-side decorations on Linux)

**The goal:** the app draws its own caption bar (the SourceGit look) on every platform,
including KDE Plasma / Wayland, with the Fluent title strip
(`MainWindow.axaml` → `AppTitleStrip`) as the *only* titlebar.

**The platform path.** Avalonia 12 runs on the **X11 backend on Linux, i.e. through
XWayland** inside a Wayland session. The native `Avalonia.Wayland` backend is a separate,
experimental opt-in package and is *not* referenced here; upstream has no
`EnableDrawnDecorations` equivalent for it, so a Wayland-native window on KWin is always
decorated server-side. The X11 backend has the switch that fixes this:

```csharp
.With(new X11PlatformOptions { EnableDrawnDecorations = true })   // Program.cs
```

It makes Avalonia draw the border, shadow and resize grips and stops it asking the WM for
a frame. It is flagged experimental, so the analyzer diagnostic is suppressed deliberately:
`<NoWarn>$(NoWarn);AVALONIA_X11_CSD</NoWarn>` in `ACadSharp.Viewer.csproj`.

**Verified on this machine** (KWin on Wayland, Avalonia 12.1.3) by reading the live
XWayland window properties: `_MOTIF_WM_HINTS flags=3 functions=0 decorations=0` and **no**
`_NET_FRAME_EXTENTS` — identical to SourceGit's window on the same session, and the
signature of "the WM draws nothing".

**The gotcha that produces a *second* titlebar.** `ExtendClientAreaTitleBarHeightHint`
feeds `WindowDrawnDecorations.TitleBarHeightOverride`, and

```
TitleBarHeight = TitleBarHeightOverride == -1 ? DefaultTitleBarHeight : TitleBarHeightOverride
HasTitleBar    = TitleBarHeight > 0   // gates PART_TitleBar, PART_TitleTextPanel, PART_OverlayPanel
```

(`src/Avalonia.Controls/Chrome/WindowDrawnDecorations.cs` @12.1.3; the parts are in
`src/Avalonia.Themes.Fluent/Controls/WindowDrawnDecorations.xaml` @12.1.3). With the hint
set, Avalonia paints its *own* caption — title text plus its own fullscreen/minimize/
maximize/close buttons — on top of our strip: two app-drawn captions stacked, which is
exactly what the first live test showed. **Do not set the hint.** Ship a decorations theme
that zeroes the default height instead (`MainWindow.axaml`):

```xml
<Window.WindowDecorationsTheme>
    <ControlTheme TargetType="chrome:WindowDrawnDecorations"
                  BasedOn="{StaticResource {x:Type chrome:WindowDrawnDecorations}}">
        <Setter Property="DefaultTitleBarHeight" Value="0" />
    </ControlTheme>
</Window.WindowDecorationsTheme>
```

with `WindowDecorations="Full"` and `ExtendClientAreaToDecorationsHint="True"`. Avalonia
keeps the frame (1px border, 8px shadow, the resize grip zones) and draws no caption.

**Non-client input is routed by role** (`Avalonia.Controls.Chrome.WindowDecorationProperties.
ElementRole`): the strip declares `TitleBar` (the drag area) and each caption button
declares `MinimizeButton` / `MaximizeButton` / `CloseButton`, so a press on a button is a
button press and not a window move. The theme's own caption buttons carry the same roles —
a reason the caption parts must stay hidden: role routing hands the input to the topmost
role owner in the chrome overlay.

**Double-click to maximize is app code.** With X11 client-side decorations Avalonia 12.1
does not maximize on a titlebar double-click (upstream avalonia#22239), and v12 removed
`Tapped` / `DoubleTapped` and `ClickCount`, so `OnTitleBarPointerPressed` pairs two left
presses itself (≤500 ms apart by `PointerEventArgs.Timestamp`, ≤8 px apart) and toggles
`WindowState`. The maximize glyph follows `WindowStateProperty` through
`AvaloniaObject.PropertyChanged` (v12 has no `Window.StateChanged` event).

**Dev switches** (env, no rebuild needed):

| Variable | Effect |
| --- | --- |
| `ACADSHARP_VIEWER_TITLEBAR=system` | hand the caption back to the OS (strip hidden, file name moves into `Window.Title`); `=custom` forces the app-drawn strip — the default on all platforms |
| `ACADSHARP_VIEWER_X11_CSD=0` | drop `EnableDrawnDecorations` → the WM decorates again (A/B comparison, reproduces the old double bar) |

### 10.5 Child (dialog) windows — the same chrome, one rank lower

The two child windows (`Controls/NodeViewerDialog`, `Controls/BlockPropertiesTableDialog`)
own their caption exactly like the main window — same `.appChrome` style
(`FluentStyles.axaml`: `WindowDecorations="Full"` + `ExtendClientAreaToDecorationsHint`),
same inline `DefaultTitleBarHeight=0` decorations theme, same `ElementRole` routing — and
the strip is then **demoted so a dialog does not read as a second application window**:

| Rank | main strip | child strip (`.childTitleStrip`) |
| --- | --- | --- |
| height | 34px (Win11 app titlebar) | **30px** (Win11 dialog titlebar) |
| fill | `SolidBackgroundFillColorBaseBrush` (window level) | **`CardBackgroundFillColorDefaultBrush` + 1px bottom hairline** — a surface *inside* the app |
| title | app name (secondary) + file name (tertiary), Semibold | **Normal weight, `TextFillColorSecondaryBrush`** — a label, not a brand |
| inset | 16px (aligns with the panes) | **12px** (aligns with the dialogs' own 12px header padding) |
| glyph | 14px `AccentFillColorDefaultBrush` | 12px `AccentFillColorSecondaryBrush` (identity, quieter) |
| caption | Minimize + Maximize + Close, 46x32 | **Close only** on the modal node viewer; **Minimize + Close** on the modeless tables dialog; 46x**29** (`.captionChild` — the strip Border spends 1px of its 30 on the hairline) |
| double-click | maximizes/restores (app code, #22239) | **none** — a dialog has no maximize button, so double-clicking its bar does nothing |

Three more things make it read as a *child*, not as a peer window:

- **Owner.** The modeless tables dialog is shown with `Show(this)` and the verification
  node viewer with `Show(this)`; `ShowDialog(this)` already sets it. On X11/XWayland the
  owner is the **transient-for** hint, so KWin keeps the dialog above the main window and
  moves it with the parent; with `ShowInTaskbar="False"` it never gets its own taskbar
  entry. `WindowStartupLocation="CenterOwner"` only centers on the owner once there is one.
- **The title carries identity.** The strip binds to `Window.Title`
  (`{Binding $parent[Window].Title}`) and the code-behind sets it from the same data as the
  in-content header (`"Node viewer — dynamic-diameter"`, `"Block properties tables (1)"`), so
  the WM title matches the caption (`tools/x11-chrome-probe` reads it back).
- **Two ranks of header.** The strip is the *window* label; the dialogs' own header Border
  stays the *document* header ("Nodes building 'dynamic-diameter' — block my-dynamic-block").
  The strip never duplicates that text, so the old single-header look loses nothing.

`ChildWindowChrome.cs` holds the shared input plumbing (`BeginDrag` = the
`BeginMoveDrag` fallback for renderers without role routing, `ConsumeCaptionPress` = the
caption-button press swallower). XAML event handlers need the two-parameter form
`(object? sender, PointerPressedEventArgs e)` — a one-parameter handler fails at compile
with AVLN3000.

Verify headlessly: `--screenshot out.png samples/sample_AC1015.dwg dialog` (node viewer) and
`--screenshot out.png samples/bpt-forensics/1kVKeetKOPIE.dwg bpt` (tables dialog).
