using Avalonia.Controls;
using Avalonia.Input;

namespace ACadSharp.Viewer;

/// <summary>
/// Shared input plumbing for the app-drawn titlebars of the Viewer's child
/// (dialog) windows. The visual side lives in `FluentStyles.axaml`
/// (`.childTitleStrip`, `.childTitle`, `.captionChild`); the mechanism —
/// Avalonia 12 client-side decorations, the zeroed `DefaultTitleBarHeight`
/// decorations theme and the `WindowDecorationProperties.ElementRole` routing —
/// is documented in docs/design/fluentavalonia3-tokens.md §10.
/// </summary>
public static class ChildWindowChrome
{
	/// <summary>
	/// Start a window move from a press on the child titlebar. Under client-side
	/// decorations the `TitleBar` element role routes the move itself; the
	/// `BeginMoveDrag` call is the fallback for renderers without role routing
	/// (the headless renderer used by --screenshot), and marking the event handled
	/// is what keeps the dialogs' own content (graph pan, list drags) untouched.
	/// A child window has no maximize button, so — unlike the main window — a
	/// child titlebar has no double-click-to-maximize.
	/// </summary>
	public static void BeginDrag(Window window, PointerPressedEventArgs e)
	{
		if (!e.Properties.IsLeftButtonPressed)
		{
			return;
		}

		window.BeginMoveDrag(e);
		e.Handled = true;
	}

	/// <summary>
	/// Swallow a press that landed on a caption button, so it never reaches the
	/// titlebar role as a window move. The role properties on the buttons already
	/// keep the decorations layer from moving the window; this is what keeps the
	/// click a single click.
	/// </summary>
	public static void ConsumeCaptionPress(PointerPressedEventArgs e)
	{
		e.Handled = true;
	}
}
