#!/usr/bin/env python3
"""x11-chrome-probe — read a live X11/XWayland window's chrome hints.

Answers one question for the Viewer's app-drawn titlebar work
(docs/design/fluentavalonia3-tokens.md §10): **is the window manager decorating
this window, or is the app?**

  python3 probe.py [title-substring ...]      # default substring: ACadSharp

For every matching toplevel it prints the title, class, id, size, position, the
decoded _MOTIF_WM_HINTS, and whether _NET_FRAME_EXTENTS is present:

  _MOTIF_WM_HINTS flags=3 functions=0 decorations=0   +  no _NET_FRAME_EXTENTS
      => the client owns the frame (client-side decorations, what Avalonia 12's
         X11PlatformOptions.EnableDrawnDecorations produces; SourceGit does it too)
  _NET_FRAME_EXTENTS 8,8,34,8
      => the WM drew a frame (a server-side titlebar; our strip would stack on it)

Runs against XWayland from a Wayland session: set DISPLAY (KWin's XWayland is
usually :0). The X11 socket is reached over the abstract namespace, so a sandbox
without /tmp/.X11-unix still works.

Needs python3-Xlib (`python3 -c "import Xlib"`). To capture the window's own
pixels afterwards: `DISPLAY=:0 import -window <id> out.png` (ImageMagick).
"""

import sys

from Xlib import display

DEFAULT_TARGETS = ["ACadSharp"]

MOTIF_FLAGS = {"flags": "flags", "functions": "functions", "decorations": "decorations"}


def prop(display, name):
    return display.intern_atom(name)


def values(window, atom):
    if not atom:
        return None
    got = window.get_full_property(atom, 0)
    return None if got is None else list(got.value)


def utf8_text(value):
    """Xlib hands back UTF-8 window titles as latin-1-decoded str — put it back."""
    if value is None:
        return ""
    try:
        return value.encode("latin-1").decode("utf-8")
    except UnicodeError:
        return value


def motif_text(vals):
    if vals is None:
        return "_MOTIF_WM_HINTS <absent>  (WM decides: it decorates)"
    flags, functions, decorations = vals[0], vals[2], vals[3]
    verdict = "WM draws NOTHING (client owns the frame)" if decorations == 0 else "WM draws a titlebar"
    if not (flags & 1):
        verdict = "decorations field not specified (WM decides)"
    return (
        f"_MOTIF_WM_HINTS flags={flags} functions={functions} decorations={decorations}"
        f"  =>  {verdict}"
    )


def main():
    targets = sys.argv[1:] or DEFAULT_TARGETS
    d = display.Display()
    root = d.screen().root
    state_atom = prop(d, "_NET_WM_STATE")
    motif_atom = prop(d, "_MOTIF_WM_HINTS")
    extents_atom = prop(d, "_NET_FRAME_EXTENTS")

    found = 0
    for child in root.query_tree().children:
        try:
            name = utf8_text(child.get_wm_name()) or ""
            cls = " ".join(utf8_text(part) for part in (child.get_wm_class() or ()))
        except Exception:
            continue
        if not any(t.lower() in (name + " " + cls).lower() for t in targets):
            continue
        found += 1
        geo = child.get_geometry()
        pos = child.translate_coords(root, 0, 0)
        print(f"0x{child.id:x}  {name!r}  class={cls!r}")
        print(f"    size={geo.width}x{geo.height}  pos=({pos.x},{pos.y})")
        print(f"    {motif_text(values(child, motif_atom))}")
        extents = values(child, extents_atom)
        print(f"    _NET_FRAME_EXTENTS {extents if extents else '<absent>'}"
              f"{'' if extents else '  (no WM frame)'}")
        state = values(child, state_atom)
        if state:
            print("    _NET_WM_STATE " + ", ".join(d.get_atom_name(a) for a in state))
    if not found:
        print(f"no toplevel matching {targets}")
    d.close()
    return 0 if found else 1


if __name__ == "__main__":
    sys.exit(main())
