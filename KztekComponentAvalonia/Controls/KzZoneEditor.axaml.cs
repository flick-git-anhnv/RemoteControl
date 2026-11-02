using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KztekComponentAvalonia.Controls;

public class ZoneEventArgs : EventArgs
{
    public KzZone Zone { get; }
    public ZoneEventArgs(KzZone zone) => Zone = zone;
}

/// <summary>
/// Canvas-based zone editor: draw, move, resize rectangular zones. Supports select, delete, undo/redo, snap-to-grid.
/// </summary>
public partial class KzZoneEditor : UserControl
{
    // ── Styled Properties ──────────────────────────────────────────────────────

    public static readonly StyledProperty<int> SnapSizeProperty =
        AvaloniaProperty.Register<KzZoneEditor, int>(nameof(SnapSize), 8);

    public static readonly StyledProperty<bool> SnapEnabledProperty =
        AvaloniaProperty.Register<KzZoneEditor, bool>(nameof(SnapEnabled), true);

    public static readonly StyledProperty<string> DefaultZoneColorProperty =
        AvaloniaProperty.Register<KzZoneEditor, string>(nameof(DefaultZoneColor), "#F05922");

    public int    SnapSize         { get => GetValue(SnapSizeProperty);         set => SetValue(SnapSizeProperty, value); }
    public bool   SnapEnabled      { get => GetValue(SnapEnabledProperty);      set => SetValue(SnapEnabledProperty, value); }
    public string DefaultZoneColor { get => GetValue(DefaultZoneColorProperty); set => SetValue(DefaultZoneColorProperty, value); }

    // ── Events ─────────────────────────────────────────────────────────────────

    public event EventHandler<ZoneEventArgs>? ZoneAdded;
    public event EventHandler<ZoneEventArgs>? ZoneSelected;
    public event EventHandler<ZoneEventArgs>? ZoneDeleted;
    public event EventHandler?                ZonesChanged;

    // ── Interaction modes ──────────────────────────────────────────────────────

    private enum InteractionMode { None, Drawing, Moving, Resizing }
    private InteractionMode _mode = InteractionMode.None;

    // Resize direction flags (valid when _mode == Resizing)
    private bool _resizeL, _resizeR, _resizeT, _resizeB;

    // Shared drag state (move + resize)
    private Point _dragOrigin;           // canvas position where drag started
    private Rect  _boundsAtDragStart;    // zone bounds at press moment

    // For lazy undo: snapshot taken at press; pushed to stack only on first actual move
    private List<KzZone>? _preInteractionSnapshot;
    private bool          _interactionChangedBounds;

    // Handle hit-test tolerance in pixels
    private const double HandleSize  = 8.0;
    private const double MinZoneSize = 10.0;

    // ── State ──────────────────────────────────────────────────────────────────

    private readonly List<KzZone> _zones = new();
    private readonly LinkedList<List<KzZone>> _undoStack = new();
    private readonly LinkedList<List<KzZone>> _redoStack = new();
    private const int MaxUndoHistory = 50;

    private KzZone? _selectedZone;

    // AXAML parts
    private Canvas?    _canvas;
    private KzButton?  _btnUndo, _btnRedo, _btnDelete;
    private CheckBox?  _chkSnap;
    private TextBlock? _txtSnapSize, _txtInfo;

    private EventHandler<RoutedEventArgs>? _onBtnUndoClick;
    private EventHandler<RoutedEventArgs>? _onBtnRedoClick;
    private EventHandler<RoutedEventArgs>? _onBtnDeleteClick;
    private EventHandler<RoutedEventArgs>? _onChkSnapChanged;

    // Overlay rectangle while drawing
    private Rectangle? _draftRect;
    private Point      _drawStart;      // snap-adjusted press point for drawing

    // Visual map: zone → its Rectangle on canvas
    private readonly Dictionary<Guid, Rectangle> _rects = new();

    // ── Public API ─────────────────────────────────────────────────────────────

    public IReadOnlyList<KzZone> Zones => _zones.AsReadOnly();

    public void AddZone(KzZone zone)
    {
        SaveUndoState();
        _zones.Add(zone);
        RenderZone(zone);
        UpdateInfo();
        ZonesChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RemoveZone(Guid id)
    {
        var zone = _zones.FirstOrDefault(z => z.Id == id);
        if (zone is null) return;
        SaveUndoState();
        _zones.Remove(zone);
        if (_rects.TryGetValue(id, out var rect))
        {
            _canvas?.Children.Remove(rect);
            _rects.Remove(id);
        }
        if (_selectedZone?.Id == id) SelectZone(null);
        UpdateInfo();
        ZoneDeleted?.Invoke(this, new ZoneEventArgs(zone));
        ZonesChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ClearZones()
    {
        if (_zones.Count == 0) return;
        SaveUndoState();
        _zones.Clear();
        foreach (var r in _rects.Values) _canvas?.Children.Remove(r);
        _rects.Clear();
        SelectZone(null);
        UpdateInfo();
        ZonesChanged?.Invoke(this, EventArgs.Empty);
    }

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    public KzZoneEditor() => InitializeComponent();

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _canvas      = this.FindControl<Canvas>   ("PART_Canvas")!;
        _btnUndo     = this.FindControl<KzButton> ("PART_BtnUndo")!;
        _btnRedo     = this.FindControl<KzButton> ("PART_BtnRedo")!;
        _btnDelete   = this.FindControl<KzButton> ("PART_BtnDelete")!;
        _chkSnap     = this.FindControl<CheckBox> ("PART_ChkSnap")!;
        _txtSnapSize = this.FindControl<TextBlock>("PART_TxtSnapSize")!;
        _txtInfo     = this.FindControl<TextBlock>("PART_TxtInfo")!;

        _onBtnUndoClick   = (_, _) => Undo();
        _onBtnRedoClick   = (_, _) => Redo();
        _onBtnDeleteClick = (_, _) => DeleteSelected();
        _btnUndo.Click   += _onBtnUndoClick;
        _btnRedo.Click   += _onBtnRedoClick;
        _btnDelete.Click += _onBtnDeleteClick;

        _onChkSnapChanged = (_, _) =>
        {
            SnapEnabled = _chkSnap!.IsChecked == true;
            _txtSnapSize!.IsVisible = SnapEnabled;
        };
        _chkSnap.IsCheckedChanged += _onChkSnapChanged;

        _canvas.PointerPressed  += OnCanvasPointerPressed;
        _canvas.PointerMoved    += OnCanvasPointerMoved;
        _canvas.PointerReleased += OnCanvasPointerReleased;

        this.AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);

        _txtSnapSize!.Text = $"({SnapSize}px)";
        UpdateInfo();
        SyncUndoRedo();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_btnUndo   is not null) _btnUndo.Click            -= _onBtnUndoClick;
        if (_btnRedo   is not null) _btnRedo.Click            -= _onBtnRedoClick;
        if (_btnDelete is not null) _btnDelete.Click          -= _onBtnDeleteClick;
        if (_chkSnap   is not null) _chkSnap.IsCheckedChanged -= _onChkSnapChanged;
        if (_canvas    is not null)
        {
            _canvas.PointerPressed  -= OnCanvasPointerPressed;
            _canvas.PointerMoved    -= OnCanvasPointerMoved;
            _canvas.PointerReleased -= OnCanvasPointerReleased;
        }
        this.RemoveHandler(KeyDownEvent, OnKeyDown);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SnapEnabledProperty && _chkSnap is not null)
            _chkSnap.IsChecked = SnapEnabled;
    }

    // ── Keyboard ───────────────────────────────────────────────────────────────

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if      (e.Key == Key.Z && e.KeyModifiers.HasFlag(KeyModifiers.Control)) { Undo();          e.Handled = true; }
        else if (e.Key == Key.Y && e.KeyModifiers.HasFlag(KeyModifiers.Control)) { Redo();          e.Handled = true; }
        else if (e.Key == Key.Delete || e.Key == Key.Back)                        { DeleteSelected(); e.Handled = true; }
    }

    // ── Mouse handling ─────────────────────────────────────────────────────────

    private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_canvas is null) return;
        _canvas.Focus();

        var pt  = e.GetPosition(_canvas);
        var hit = HitTestZone(pt);

        if (hit is not null)
        {
            SelectZone(hit);

            _dragOrigin               = pt;
            _boundsAtDragStart        = hit.Bounds;
            _preInteractionSnapshot   = _zones.Select(z => z.Clone()).ToList();
            _interactionChangedBounds = false;

            var (l, r, t, b) = HitTestHandle(pt, hit.Bounds);
            bool onEdge = l || r || t || b;

            if (onEdge)
            {
                _mode    = InteractionMode.Resizing;
                _resizeL = l; _resizeR = r; _resizeT = t; _resizeB = b;
            }
            else
            {
                _mode = InteractionMode.Moving;
            }

            e.Pointer.Capture(_canvas);
            e.Handled = true;
            return;
        }

        // Empty area → start drawing
        SelectZone(null);
        _mode      = InteractionMode.Drawing;
        _drawStart = Snap(pt);

        _draftRect = new Rectangle
        {
            Stroke           = new SolidColorBrush(Color.Parse("#F05922")),
            StrokeThickness  = 2,
            Fill             = new SolidColorBrush(Color.Parse("#33F05922")),
            StrokeDashArray  = new Avalonia.Collections.AvaloniaList<double> { 4, 3 },
            IsHitTestVisible = false
        };
        Canvas.SetLeft(_draftRect, _drawStart.X);
        Canvas.SetTop (_draftRect, _drawStart.Y);
        _canvas.Children.Add(_draftRect);

        e.Pointer.Capture(_canvas);
        e.Handled = true;
    }

    private void OnCanvasPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_canvas is null) return;

        var pt = e.GetPosition(_canvas);

        if (_mode == InteractionMode.None)
        {
            UpdateCursor(pt);
            return;
        }

        if (_mode == InteractionMode.Drawing)
        {
            if (_draftRect is null) return;
            var snapped = Snap(pt);
            Canvas.SetLeft(_draftRect, Math.Min(_drawStart.X, snapped.X));
            Canvas.SetTop (_draftRect, Math.Min(_drawStart.Y, snapped.Y));
            _draftRect.Width  = Math.Abs(snapped.X - _drawStart.X);
            _draftRect.Height = Math.Abs(snapped.Y - _drawStart.Y);
            e.Handled = true;
            return;
        }

        if ((_mode == InteractionMode.Moving || _mode == InteractionMode.Resizing) && _selectedZone is not null)
        {
            // Push undo snapshot lazily on first actual movement
            if (!_interactionChangedBounds)
            {
                _undoStack.AddLast(_preInteractionSnapshot!);
                if (_undoStack.Count > MaxUndoHistory) _undoStack.RemoveFirst();
                _redoStack.Clear();
                SyncUndoRedo();
                _interactionChangedBounds = true;
            }

            var snapped = Snap(pt);
            var origin  = Snap(_dragOrigin);
            double dx   = snapped.X - origin.X;
            double dy   = snapped.Y - origin.Y;
            var orig    = _boundsAtDragStart;

            double newX = orig.X, newY = orig.Y, newW = orig.Width, newH = orig.Height;

            if (_mode == InteractionMode.Moving)
            {
                newX = orig.X + dx;
                newY = orig.Y + dy;
            }
            else
            {
                if (_resizeL)
                {
                    newX = orig.X + dx;
                    newW = orig.Width - dx;
                    if (newW < MinZoneSize) { newW = MinZoneSize; newX = orig.Right - MinZoneSize; }
                }
                if (_resizeR)
                {
                    newW = orig.Width + dx;
                    if (newW < MinZoneSize) newW = MinZoneSize;
                }
                if (_resizeT)
                {
                    newY = orig.Y + dy;
                    newH = orig.Height - dy;
                    if (newH < MinZoneSize) { newH = MinZoneSize; newY = orig.Bottom - MinZoneSize; }
                }
                if (_resizeB)
                {
                    newH = orig.Height + dy;
                    if (newH < MinZoneSize) newH = MinZoneSize;
                }
            }

            // Clamp to canvas
            double cw = _canvas.Bounds.Width;
            double ch = _canvas.Bounds.Height;
            if (cw > 0)
            {
                newX = Math.Max(0, newX);
                if (newX + newW > cw) newW = cw - newX;
                newW = Math.Max(MinZoneSize, newW);
            }
            if (ch > 0)
            {
                newY = Math.Max(0, newY);
                if (newY + newH > ch) newH = ch - newY;
                newH = Math.Max(MinZoneSize, newH);
            }

            var newBounds = new Rect(newX, newY, newW, newH);
            _selectedZone.UpdateBounds(newBounds);

            if (_rects.TryGetValue(_selectedZone.Id, out var rect))
            {
                Canvas.SetLeft(rect, newX);
                Canvas.SetTop (rect, newY);
                rect.Width  = newW;
                rect.Height = newH;
            }

            e.Handled = true;
        }
    }

    private void OnCanvasPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_canvas is null) { ResetInteractionState(); return; }

        if (_mode == InteractionMode.Drawing)
        {
            if (_draftRect is not null)
            {
                _canvas.Children.Remove(_draftRect);
                _draftRect = null;
            }

            var pt  = Snap(e.GetPosition(_canvas));
            var x   = Math.Min(_drawStart.X, pt.X);
            var y   = Math.Min(_drawStart.Y, pt.Y);
            var w   = Math.Abs(pt.X - _drawStart.X);
            var h   = Math.Abs(pt.Y - _drawStart.Y);

            if (w >= MinZoneSize && h >= MinZoneSize)
            {
                var zone = new KzZone(new Rect(x, y, w, h), color: DefaultZoneColor);
                SaveUndoState();
                _zones.Add(zone);
                RenderZone(zone);
                SelectZone(zone);
                UpdateInfo();
                ZoneAdded?.Invoke(this, new ZoneEventArgs(zone));
                ZonesChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        else if (_interactionChangedBounds)
        {
            ZonesChanged?.Invoke(this, EventArgs.Empty);
        }

        ResetInteractionState();
        e.Pointer.Capture(null);
        e.Handled = true;

        UpdateCursor(e.GetPosition(_canvas));
    }

    private void ResetInteractionState()
    {
        _mode                     = InteractionMode.None;
        _resizeL = _resizeR = _resizeT = _resizeB = false;
        _interactionChangedBounds = false;
        _preInteractionSnapshot   = null;
    }

    // ── Cursor ─────────────────────────────────────────────────────────────────

    private void UpdateCursor(Point pt)
    {
        if (_canvas is null) return;

        var hit = HitTestZone(pt);
        if (hit is null)
        {
            _canvas.Cursor = new Cursor(StandardCursorType.Cross);
            return;
        }

        var (l, r, t, b) = HitTestHandle(pt, hit.Bounds);
        _canvas.Cursor = new Cursor(GetCursorType(l, r, t, b));
    }

    private static (bool l, bool r, bool t, bool b) HitTestHandle(Point pt, Rect bounds)
    {
        bool l = pt.X <= bounds.Left   + HandleSize;
        bool r = pt.X >= bounds.Right  - HandleSize;
        bool t = pt.Y <= bounds.Top    + HandleSize;
        bool b = pt.Y >= bounds.Bottom - HandleSize;
        return (l, r, t, b);
    }

    private static StandardCursorType GetCursorType(bool l, bool r, bool t, bool b)
    {
        if (t && l) return StandardCursorType.TopLeftCorner;
        if (t && r) return StandardCursorType.TopRightCorner;
        if (b && l) return StandardCursorType.BottomLeftCorner;
        if (b && r) return StandardCursorType.BottomRightCorner;
        if (t || b) return StandardCursorType.SizeNorthSouth;
        if (l || r) return StandardCursorType.SizeWestEast;
        return StandardCursorType.SizeAll;  // inside = move
    }

    // ── Selection ──────────────────────────────────────────────────────────────

    private void SelectZone(KzZone? zone)
    {
        if (_selectedZone is not null && _rects.TryGetValue(_selectedZone.Id, out var prev))
            ApplyZoneStyle(prev, false);

        _selectedZone = zone;

        if (zone is not null && _rects.TryGetValue(zone.Id, out var cur))
            ApplyZoneStyle(cur, true);

        _btnDelete!.IsEnabled = zone is not null;

        if (zone is not null)
            ZoneSelected?.Invoke(this, new ZoneEventArgs(zone));
    }

    private KzZone? HitTestZone(Point pt)
    {
        for (int i = _zones.Count - 1; i >= 0; i--)
            if (_zones[i].Bounds.Contains(pt)) return _zones[i];
        return null;
    }

    // ── Delete ─────────────────────────────────────────────────────────────────

    private void DeleteSelected()
    {
        if (_selectedZone is null) return;
        RemoveZone(_selectedZone.Id);
    }

    // ── Undo / Redo ────────────────────────────────────────────────────────────

    private void SaveUndoState()
    {
        _undoStack.AddLast(_zones.Select(z => z.Clone()).ToList());
        if (_undoStack.Count > MaxUndoHistory) _undoStack.RemoveFirst();
        _redoStack.Clear();
        SyncUndoRedo();
    }

    private void Undo()
    {
        if (_undoStack.Count == 0) return;
        _redoStack.AddLast(_zones.Select(z => z.Clone()).ToList());
        var state = _undoStack.Last!.Value;
        _undoStack.RemoveLast();
        RestoreState(state);
        SyncUndoRedo();
    }

    private void Redo()
    {
        if (_redoStack.Count == 0) return;
        _undoStack.AddLast(_zones.Select(z => z.Clone()).ToList());
        if (_undoStack.Count > MaxUndoHistory) _undoStack.RemoveFirst();
        var state = _redoStack.Last!.Value;
        _redoStack.RemoveLast();
        RestoreState(state);
        SyncUndoRedo();
    }

    private void RestoreState(List<KzZone> state)
    {
        foreach (var r in _rects.Values) _canvas?.Children.Remove(r);
        _rects.Clear();
        _zones.Clear();
        SelectZone(null);

        foreach (var z in state)
        {
            _zones.Add(z);
            RenderZone(z);
        }

        UpdateInfo();
        ZonesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SyncUndoRedo()
    {
        if (_btnUndo is null) return;
        _btnUndo.IsEnabled  = _undoStack.Count > 0;
        _btnRedo!.IsEnabled = _redoStack.Count > 0;
    }

    // ── Rendering ─────────────────────────────────────────────────────────────

    private void RenderZone(KzZone zone)
    {
        if (_canvas is null) return;

        var color  = Color.Parse(zone.Color);
        var fill   = new SolidColorBrush(Color.FromArgb(50, color.R, color.G, color.B));
        var stroke = new SolidColorBrush(color);

        var rect = new Rectangle
        {
            Width            = zone.Bounds.Width,
            Height           = zone.Bounds.Height,
            Fill             = fill,
            Stroke           = stroke,
            StrokeThickness  = 2,
            IsHitTestVisible = false,   // canvas receives all pointer events
            Tag              = zone.Id
        };

        Canvas.SetLeft(rect, zone.Bounds.X);
        Canvas.SetTop (rect, zone.Bounds.Y);
        _canvas.Children.Add(rect);
        _rects[zone.Id] = rect;
    }

    private static void ApplyZoneStyle(Rectangle rect, bool selected)
    {
        rect.StrokeThickness = selected ? 3 : 2;
        rect.StrokeDashArray = selected
            ? new Avalonia.Collections.AvaloniaList<double> { 6, 3 }
            : null;
    }

    // ── Snap ──────────────────────────────────────────────────────────────────

    private Point Snap(Point pt)
    {
        if (!SnapEnabled || SnapSize <= 1) return pt;
        double Snap(double v) => Math.Round(v / SnapSize) * SnapSize;
        return new Point(Snap(pt.X), Snap(pt.Y));
    }

    // ── Info ──────────────────────────────────────────────────────────────────

    private void UpdateInfo()
    {
        if (_txtInfo is null) return;
        _txtInfo.Text = $"{_zones.Count} zone{(_zones.Count != 1 ? "s" : "")}";
    }
}
