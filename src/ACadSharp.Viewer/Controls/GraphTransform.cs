using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace ACadSharp.Viewer.Controls;

/// <summary>
/// The zoom/pan transform for the node graph: a scale + translate applied to
/// the graph canvas so a content point c appears at c*scale + pan. Encapsulates
/// the transform state (scale, pan, natural size) and the operations that change
/// it (zoom in/out, fit, wheel zoom, drag-pan, center-on-point), and raises
/// <see cref="Changed"/> whenever the transform changes (the host uses this for
/// the mini-map's visible-area indicator). Keeping the transform out of the
/// control leaves the control focused on the graph content and the hover/select
/// interaction.
/// </summary>
public class GraphTransform
{
    public const double MinScale = 0.1;
    public const double MaxScale = 3.0;

    private double _scale = 1.0;
    private double _naturalWidth;
    private double _naturalHeight;
    private double _panX;
    private double _panY;
    private Vector? _panStartPan;
    private Point _panStartPos;

    private readonly ScrollViewer _scroll;
    private readonly Canvas _canvas;

    /// <summary>
    /// Raised whenever the transform changes, with the visible content rect in
    /// content coordinates (the mini-map's visible-area indicator hook).
    /// </summary>
    public Action<Rect>? Changed;

    public GraphTransform(ScrollViewer scroll, Canvas canvas)
    {
        _scroll = scroll;
        _canvas = canvas;
    }

    /// <summary>The current zoom factor (1.0 = natural size).</summary>
    public double Scale
    {
        get => _scale;
        set
        {
            _scale = Math.Clamp(value, MinScale, MaxScale);
            Apply();
        }
    }

    /// <summary>The current content pan (the graph is positioned by a
    /// scale+translate transform, not the scroll offset).</summary>
    public Vector ScrollOffset => new(_panX, _panY);

    /// <summary>The natural (unzoomed) content size, as a vector.</summary>
    public Vector NaturalSize => new(_naturalWidth, _naturalHeight);

    /// <summary>True while a drag-pan is in progress (the box hover handlers
    /// use this to suppress hover while the view is moving).</summary>
    public bool IsPanning => _panStartPan is not null;

    /// <summary>Sets the natural (unzoomed) content size; call after a layout pass.</summary>
    public void SetNaturalSize(double width, double height)
    {
        _naturalWidth = width;
        _naturalHeight = height;
    }

    /// <summary>Resets the pan to the origin (a fresh graph load) and cancels any in-progress pan.</summary>
    public void ResetPan()
    {
        _panX = 0;
        _panY = 0;
        EndPan();
    }

    /// <summary>Cancels an in-progress pan (ends the drag; does not change the pan position).</summary>
    public void EndPan()
    {
        _panStartPan = null;
        _canvas.Cursor = null;
    }

    /// <summary>Zooms in by one step (×1.15), anchored at the viewport center.</summary>
    public void ZoomIn() => ZoomAt(new Point(_scroll.Bounds.Width / 2, _scroll.Bounds.Height / 2), 1);

    /// <summary>Zooms out by one step (÷1.15), anchored at the viewport center.</summary>
    public void ZoomOut() => ZoomAt(new Point(_scroll.Bounds.Width / 2, _scroll.Bounds.Height / 2), -1);

    /// <summary>Scales the graph to fit the visible scroll-view area (never zooms in beyond the natural size).</summary>
    public void FitToView()
    {
        if (_naturalWidth <= 0 || _naturalHeight <= 0)
        {
            return;
        }

        if (_scroll.Bounds.Width <= 0 || _scroll.Bounds.Height <= 0)
        {
            return; // not measured yet
        }

        double factor = Math.Min(
            1.0,
            Math.Min(_scroll.Bounds.Width / _naturalWidth, _scroll.Bounds.Height / _naturalHeight));
        _scale = Math.Max(MinScale, factor);

        // Center the graph in the viewport.
        _panX = (_scroll.Bounds.Width - _naturalWidth * _scale) / 2;
        _panY = (_scroll.Bounds.Height - _naturalHeight * _scale) / 2;
        Apply();
    }

    /// <summary>Zooms (1.15× per step) so the point under the cursor stays fixed.</summary>
    public void ZoomAt(Point at, double delta)
    {
        if (_naturalWidth <= 0 || delta == 0)
        {
            return;
        }

        double factor = delta > 0 ? 1.15 : 1.0 / 1.15;
        double newScale = Math.Clamp(_scale * factor, MinScale, MaxScale);
        if (newScale == _scale)
        {
            return;
        }

        // Keep the point under the cursor fixed: a content point c appears at
        // c*scale + pan, so after scaling by ratio the pan must become
        // pan' = at - (at - pan) * ratio to keep c at the same viewport point.
        double ratio = newScale / _scale;
        _panX = at.X - (at.X - _panX) * ratio;
        _panY = at.Y - (at.Y - _panY) * ratio;
        _scale = newScale;
        Apply();
    }

    /// <summary>
    /// Wheel-zooms about the cursor. The container is a plain Grid (no padding,
    /// no border), so the client area IS the content area; e.GetPosition(scroll)
    /// gives the position relative to the canvas's render origin directly.
    /// </summary>
    public void OnWheelZoom(PointerWheelEventArgs e)
    {
        ZoomAt(e.GetPosition(_scroll), e.Delta.Y);
        e.Handled = true; // do not let the scroll viewer scroll
    }

    public void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        // Only right-button pans the canvas.
        if (!e.Properties.IsRightButtonPressed)
        {
            return;
        }

        e.Pointer.Capture(_canvas);
        _panStartPan = new Vector(_panX, _panY);
        _panStartPos = e.GetPosition(_scroll);
        _canvas.Cursor = new Cursor(StandardCursorType.Hand);
    }

    public void OnPointerMoved(PointerEventArgs e)
    {
        if (_panStartPan is not Vector start)
        {
            return;
        }

        Point pos = e.GetPosition(_scroll);
        _panX = start.X + pos.X - _panStartPos.X;
        _panY = start.Y + pos.Y - _panStartPos.Y;
        UpdateTransform();
    }

    public void OnPointerReleased(PointerReleasedEventArgs e)
    {
        _panStartPan = null;
        _canvas.Cursor = null;
        e.Pointer.Capture(null);
    }

    /// <summary>Ends an in-progress pan (the host also clears its own pending-select state).</summary>
    public void OnPointerCaptureLost() => EndPan();

    /// <summary>Centers the view on the given content point (the mini-map's navigation target).</summary>
    public void CenterOnContentPoint(Point p)
    {
        if (_scroll.Bounds.Width <= 0 || _scroll.Bounds.Height <= 0)
        {
            return; // not measured yet
        }

        _panX = _scroll.Bounds.Width / 2 - p.X * _scale;
        _panY = _scroll.Bounds.Height / 2 - p.Y * _scale;
        Apply();
    }

    /// <summary>
    /// The visible content rect in content coordinates: (0-pan)/scale,
    /// (0-pan)/scale, viewport/scale. Empty before the first layout.
    /// </summary>
    public Rect GetVisibleContentRect()
    {
        double w = _scroll.Bounds.Width;
        double h = _scroll.Bounds.Height;
        if (w <= 0 || h <= 0 || _scale <= 0)
        {
            return default; // a zero rect: the mini-map hides its indicator
        }

        return new Rect(-_panX / _scale, -_panY / _scale, w / _scale, h / _scale);
    }

    /// <summary>Verification: reset the zoom to 1.0 and scroll back to the origin.</summary>
    public void ScrollToOrigin()
    {
        _scale = 1.0;
        _panX = 0;
        _panY = 0;
        Apply();
    }

    /// <summary>
    /// Sizes the canvas to the viewport and applies the transform. The canvas is
    /// always the viewport size (it never scrolls): the graph is positioned
    /// inside it by a scale+translate transform, so the empty space around the
    /// graph is part of the canvas too and the pointer handlers (wheel zoom,
    /// drag pan) work anywhere in the viewport.
    /// </summary>
    public void Apply()
    {
        if (_naturalWidth <= 0)
        {
            return;
        }

        if (_scroll.Bounds.Width > 0 && _scroll.Bounds.Height > 0)
        {
            _canvas.Width = _scroll.Bounds.Width;
            _canvas.Height = _scroll.Bounds.Height;
        }

        UpdateTransform();
    }

    // Scales the graph about the canvas origin and translates it by the
    // current pan, so a content point c appears at c*scale + pan. Every
    // pan/zoom path funnels through here, so the Changed event (the
    // mini-map's visible-area indicator) is raised in one place.
    private void UpdateTransform()
    {
        var group = new TransformGroup();
        group.Children.Add(new ScaleTransform(_scale, _scale));
        group.Children.Add(new TranslateTransform(_panX, _panY));
        _canvas.RenderTransform = group;
        Changed?.Invoke(GetVisibleContentRect());
    }
}
