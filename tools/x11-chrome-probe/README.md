# x11-chrome-probe

Reads a **live** X11/XWayland window's chrome hints and answers the one question the
app-drawn titlebar depends on: **is the window manager decorating this window, or is the
app?** (The Viewer's caption strip is app-drawn; a WM caption on top of it is the double
titlebar. See `docs/design/fluentavalonia3-tokens.md` §10.)

Written in **Python**, not C#: it needs Xlib bindings and `python3-Xlib` is installed on
this machine, while no .NET Xlib binding is.

## How to run

```bash
DISPLAY=:0 python3 tools/x11-chrome-probe/probe.py            # default: match "ACadSharp"
DISPLAY=:0 python3 tools/x11-chrome-probe/probe.py SourceGit  # compare against a known-good app
```

Under a Wayland session `DISPLAY=:0` is KWin's XWayland. The X11 socket is reached over
the abstract namespace, so it works from a sandbox that has no `/tmp/.X11-unix`.

Exit status: `0` if at least one toplevel matched, `1` if nothing matched.

## What it prints

For each matching toplevel: `id`, title, WM class, size, position, the decoded
`_MOTIF_WM_HINTS`, `_NET_FRAME_EXTENTS` and `_NET_WM_STATE` (atom names).

| Reading | Meaning |
| --- | --- |
| `_MOTIF_WM_HINTS flags=3 functions=0 decorations=0` + `_NET_FRAME_EXTENTS <absent>` | **the client owns the frame** — no WM titlebar. This is what Avalonia 12's `X11PlatformOptions.EnableDrawnDecorations` produces, and it is exactly what SourceGit shows on the same session. |
| `_NET_FRAME_EXTENTS 8,8,34,8` | **the WM drew a frame** — a server-side titlebar exists, so an app-drawn strip stacks on it. |
| `_MOTIF_WM_HINTS <absent>` | the WM decides; on KWin that means it decorates. |

## Capture the window's own pixels

```bash
DISPLAY=:0 import -window <id-from-the-probe> window.png     # ImageMagick
```

KWin does not implement the Wayland screen-capture extension (`grim` fails), so this X11
path is the way to see a single window on this machine. It shows the app's pixels
**including** Avalonia's drawn border/shadow, which is how the caption ownership is
confirmed visually.
