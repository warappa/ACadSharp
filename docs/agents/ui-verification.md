# UI verification (headless)

The viewer can be run **headless** (no display/X11/GPU) to render and capture the real UI.
**When to read this:** rendering or capturing the Viewer UI without a display, or debugging what the node viewer draws.

The toolkit (each self-documenting in its `README.md`):

| Step | Tool | What it does |
| --- | --- | --- |
| 1. Capture | the Viewer's built-in `--screenshot` mode (`src/ACadSharp.Viewer/Screenshot.cs`, wired in `Program.cs`) | renders the real UI headless and saves a PNG + `diag.txt` |
| 2. Analyze | `tools/ui-check` | pixel-scans a screenshot for the node colors + bounding boxes; audits the `GraphModel` layout for provider-right-of-consumer anomalies; dumps parsed connections / raw edges |
| 3. Inspect | `tools/graph-dump` | dumps the `GraphModel` display model directly (no rendering): edge lines with wire index + `lookup`/`value` kind + label, ports with wire indices |

## Capturing (the Viewer's `--screenshot` mode)

Usage: `--screenshot <out.png> [file.dwg|file.dxf] [mode]`

- `dialog` — open the node viewer for the first dynamic block
- `tip` — the first-run teaching tip
- `flyout` — the settings flyout
- `hc` / `accent` — high-contrast theme / red accent
- `interact` — simulate wheel zoom, drag pan, click select, hover through the real input pipeline
- `zoombug` / `minimap` — mini-map rectangle tracking + navigation
- `fingerprint` — a **deterministic layout fingerprint** (node positions, edge labels + multiplicity, scale/pan, visible rect): a cheap regression check without pixels

It writes a `diag.txt` (setup, load, dialog, capture timestamps) next to the output.

## Gotchas learned while building this

- **`UseHeadlessDrawing=true` (the default) produces no pixels**: it installs a no-op render-interface stub that never locks the window's framebuffer, so `GetLastRenderedFrame()` is always null. Instead build the app manually with `UseHeadlessDrawing=false` + the real Skia renderer (the `Avalonia.Skia` package): Skia's `FramebufferRenderTarget` locks the headless window's framebuffer (`Lock()`) and draws real pixels that `CaptureRenderedFrame()` can read back.
- **Everything must run on one dedicated thread**: the `Dispatcher` created during `SetupWithoutStarting()` binds to that thread and becomes the UI thread.
- **Pump the dispatcher to let work run**: `Dispatcher.UIThread.RunJobs()` + a short sleep (the `Pump(n)` helper in `Screenshot.cs`) so posted continuations and background-priority work (e.g. the dialog auto-fit) complete before capturing.
- `Avalonia.Headless` 12.1.3 (and `Avalonia.Skia` 12.1.3) restore fine from the normal feed into `.tmp-nuget`; the hand-assembled `headless.nupkg` that used to sit in `.tmp-headless/` was a throwaway workaround.
