using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Elfive.Core.FBD;
using Elfive.Core.L5X.Base;

namespace Elfive.App.Views;

public class FBDViewer : UserControl
{
    // Layout constants
    private const double CoordScale = 2.2;   // L5X coord units → screen pixels
    private const double BlockMinWidth = 145; // 150 * 0.70
    private const double BlockMinHeight = 50; // 60  * 0.50
    private const double PinSpacing = 15;     // 22  / 2
    private const double HeaderHeight = 40;   // 36  * 0.50
    private const double RefWidth = 140;
    private const double RefHeight = 28;
    private const double PinDotSize = 7;
    private const double AreaPadding = 80;

    // Zoom/pan state
    private double _zoom = 1.0;
    private readonly ScaleTransform _scaleTransform;
    private readonly Canvas _canvas;
    private bool _isPanning;
    private Point _panStartMouse;
    private Point _panStartScroll;

    // Pin position cache — populated during element drawing,
    // consumed during wire drawing
    private readonly Dictionary<Connection, Point> _pinPositions = new();

    // Computed block sizes — needed for pin positioning
    private readonly Dictionary<ulong, (double Width, double Height)> _blockSizes = new();
    
    private FbdSheet[] _sheets = [];
    private int _currentSheet = 0;
    private readonly StackPanel _sheetTabs;
    private readonly ScrollViewer _scroll;
    private readonly TextBlock _header;


    public FBDViewer()
    {
        _scaleTransform = new ScaleTransform(1, 1);
        _canvas = new Canvas
        {
            Background = Brushes.White,
            LayoutTransform = _scaleTransform  // LayoutTransform so ScrollViewer tracks true scaled size
        };
        _scroll = new ScrollViewer
        {
            Content = _canvas,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        _scroll.PreviewMouseLeftButtonDown += OnPanStart;
        _scroll.PreviewMouseMove += OnPanMove;
        _scroll.PreviewMouseLeftButtonUp += OnPanEnd;
        _scroll.MouseLeave += (s, e) => EndPan();

        // Sheet tab strip along the bottom
        _sheetTabs = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Background = new SolidColorBrush(Color.FromRgb(240, 240, 240)),
            Height = 30
        };

        _header = new TextBlock
        {
            FontFamily = new FontFamily("Consolas"),
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Padding = new Thickness(8, 4, 8, 4),
            Background = new SolidColorBrush(Color.FromRgb(235, 240, 250)),
            Foreground = new SolidColorBrush(Color.FromRgb(30, 60, 120))
        };

        var dock = new DockPanel();
        DockPanel.SetDock(_header, Dock.Top);
        DockPanel.SetDock(_sheetTabs, Dock.Bottom);
        dock.Children.Add(_header);
        dock.Children.Add(_sheetTabs);
        dock.Children.Add(_scroll); // fills remaining space

        Content = dock;
        DataContextChanged += (s, e) => LoadSheets();
        PreviewMouseWheel += OnMouseWheel;
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control) return;
        e.Handled = true;

        var oldZoom = _zoom;
        _zoom = Math.Clamp(_zoom * (e.Delta > 0 ? 1.15 : 0.87), 0.2, 5.0);

        // Canvas point under the cursor in unscaled coords
        var mouseInScroll = e.GetPosition(_scroll);
        var canvasX = (_scroll.HorizontalOffset + mouseInScroll.X) / oldZoom;
        var canvasY = (_scroll.VerticalOffset  + mouseInScroll.Y) / oldZoom;

        _scaleTransform.ScaleX = _zoom;
        _scaleTransform.ScaleY = _zoom;

        // Scroll after layout so the same canvas point stays under the cursor
        Dispatcher.BeginInvoke(() =>
        {
            _scroll.ScrollToHorizontalOffset(canvasX * _zoom - mouseInScroll.X);
            _scroll.ScrollToVerticalOffset(canvasY  * _zoom - mouseInScroll.Y);
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void OnPanStart(object sender, MouseButtonEventArgs e)
    {
        _isPanning = true;
        _panStartMouse  = e.GetPosition(_scroll);
        _panStartScroll = new Point(_scroll.HorizontalOffset, _scroll.VerticalOffset);
        _scroll.CaptureMouse();
        _scroll.Cursor = Cursors.SizeAll;
        e.Handled = true;
    }

    private void OnPanMove(object sender, MouseEventArgs e)
    {
        if (!_isPanning) return;
        var cur = e.GetPosition(_scroll);
        _scroll.ScrollToHorizontalOffset(_panStartScroll.X - (cur.X - _panStartMouse.X));
        _scroll.ScrollToVerticalOffset(_panStartScroll.Y  - (cur.Y - _panStartMouse.Y));
        e.Handled = true;
    }

    private void OnPanEnd(object sender, MouseButtonEventArgs e) => EndPan();

    private void EndPan()
    {
        if (!_isPanning) return;
        _isPanning = false;
        _scroll.ReleaseMouseCapture();
        _scroll.Cursor = Cursors.Arrow;
    }
    
    private void LoadSheets()
    {
        _sheets = GetSheets();
        _currentSheet = 0;

        // If a specific sheet node was selected, jump straight to it
        if (DataContext is TreeNode<IFbdSheet> sheetNode)
        {
            var targetNumber = sheetNode.Source?.Number ?? 0;
            var idx = Array.FindIndex(_sheets, s => s.Number == targetNumber);
            if (idx >= 0) _currentSheet = idx;
        }

        UpdateHeader();
        BuildSheetTabs();
        Render();
    }

    private FbdSheet[] GetSheets()
    {
        if (DataContext is TreeNode<IRoutine> r && r.Source != null)
            return new FbdParser().ParseRoutineFbd(r.Source);

        if (DataContext is TreeNode<IFbdSheet> s && s.Parent is TreeNode<IRoutine> parent && parent.Source != null)
            return new FbdParser().ParseRoutineFbd(parent.Source);

        return [];
    }

    private void UpdateHeader()
    {
        TreeNode? routineNode = DataContext switch
        {
            TreeNode<IRoutine> n => n,
            TreeNode<IFbdSheet> s => s.Parent,
            _ => null
        };
        if (routineNode == null) { _header.Text = ""; return; }
        var programName = routineNode.Parent?.Name ?? "";
        _header.Text = string.IsNullOrEmpty(programName)
            ? routineNode.Name ?? ""
            : $"{routineNode.Name}   —   {programName}";
    }

    private void BuildSheetTabs()
    {
        _sheetTabs.Children.Clear();
        _sheetTabs.Visibility = _sheets.Length >= 1
            ? Visibility.Visible
            : Visibility.Collapsed;

        for (int i = 0; i < _sheets.Length; i++)
        {
            var index = i; // capture for closure
            var sheet = _sheets[i];

            var label = string.IsNullOrEmpty(sheet.Description)
                ? $"Sheet {sheet.Number}"
                : $"Sheet {sheet.Number}: {sheet.Description}";

            var btn = new Button
            {
                Content = label,
                Padding = new Thickness(12, 4, 12, 4),
                Margin = new Thickness(2, 3, 2, 3),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };

            btn.Click += (s, e) =>
            {
                _currentSheet = index;
                UpdateTabHighlight();
                Render();
            };

            _sheetTabs.Children.Add(btn);
        }

        UpdateTabHighlight();
    }
    
    private void UpdateTabHighlight()
    {
        for (int i = 0; i < _sheetTabs.Children.Count; i++)
        {
            if (_sheetTabs.Children[i] is Button btn)
            {
                var isActive = i == _currentSheet;
                btn.Background = isActive
                    ? new SolidColorBrush(Color.FromRgb(220, 235, 255))
                    : Brushes.White;
                btn.FontWeight = isActive
                    ? FontWeights.Bold
                    : FontWeights.Normal;
                btn.BorderBrush = isActive
                    ? Brushes.SteelBlue
                    : Brushes.LightGray;
            }
        }
    }

    private void Render()
    {
        _canvas.Children.Clear();
        _pinPositions.Clear();
        _blockSizes.Clear();

        if (_sheets.Length == 0 || _currentSheet >= _sheets.Length) return;

        var sheet = _sheets[_currentSheet];
        if (sheet.Elements.Length == 0) return;

        // ── Compute block sizes upfront (needed before drawing) ──
        foreach (var el in sheet.Elements)
            _blockSizes[el.Id] = ComputeElementSize(el);

        // ── Size the canvas from bounding box ──
        var maxX = sheet.Elements.Max(e =>
            e.X * CoordScale + _blockSizes[e.Id].Width) + AreaPadding;
        var maxY = sheet.Elements.Max(e =>
            e.Y * CoordScale + _blockSizes[e.Id].Height) + AreaPadding;
        _canvas.Width = maxX + AreaPadding;
        _canvas.Height = maxY + AreaPadding;

        // ── Draw all elements (this populates _pinPositions) ──
        foreach (var element in sheet.Elements)
            DrawElement(element);

        // ── Draw all wires ──
        foreach (var wire in sheet.Wires)
            DrawWire(wire);
    }

    // ═══════════════════════════════════════════
    //  Element sizing
    // ═══════════════════════════════════════════

    private (double Width, double Height) ComputeElementSize(FbdElement el)
    {
        if (IsReference(el.Type))
            return (RefWidth, RefHeight);

        var inputs = el.Connections.Count(c => c.IsInput);
        var outputs = el.Connections.Count(c => !c.IsInput);
        var pinRows = Math.Max(inputs, outputs);
        var height = Math.Max(BlockMinHeight, HeaderHeight + pinRows * PinSpacing + 10);

        return (BlockMinWidth, height);
    }

    private static bool IsReference(string type)
        => type is "IRef" or "ORef" or "ICon" or "OCon";

    // ═══════════════════════════════════════════
    //  Element drawing
    // ═══════════════════════════════════════════

    private void DrawElement(FbdElement el)
    {
        switch (el.Type)
        {
            case "IRef":
                DrawReference(el, isInput: true);
                break;
            case "ORef":
                DrawReference(el, isInput: false);
                break;
            case "ICon":
                DrawConnector(el, isInput: true);
                break;
            case "OCon":
                DrawConnector(el, isInput: false);
                break;
            default:
                DrawBlock(el);
                break;
        }
    }

    private void DrawBlock(FbdElement el)
    {
        var (width, height) = _blockSizes[el.Id];
        var x = el.X * CoordScale;
        var y = el.Y * CoordScale;

        // Block body
        var rect = new Rectangle
        {
            Width = width, Height = height,
            Stroke = new SolidColorBrush(Color.FromRgb(50, 80, 140)),
            StrokeThickness = 2,
            Fill = new SolidColorBrush(Color.FromRgb(235, 242, 255)),
            RadiusX = 4, RadiusY = 4
        };
        Canvas.SetLeft(rect, x);
        Canvas.SetTop(rect, y);
        _canvas.Children.Add(rect);

        // Instruction type name (centered, top)
        AddText(el.Type, x + width / 2, y + 8,
            12, FontWeights.Bold, Brushes.DarkBlue, centered: true);

        // Operand/tag name below type (if present)
        var operand = el.Operands.FirstOrDefault();
        if (!string.IsNullOrEmpty(operand))
        {
            AddText(operand, x + width / 2, y + 22,
                9.5, FontWeights.Normal,
                Brushes.DarkSlateGray, centered: true);
        }

        // Draw and cache pin positions
        var inputs = el.Connections.Where(c => c.IsInput).ToList();
        var outputs = el.Connections.Where(c => !c.IsInput).ToList();

        for (int i = 0; i < inputs.Count; i++)
        {
            var pinY = y + HeaderHeight + i * PinSpacing;
            var pinPos = new Point(x, pinY);
            _pinPositions[inputs[i]] = pinPos;

            DrawPinDot(pinPos, Brushes.DarkBlue);
            AddText(inputs[i].Name ?? "", x + 8, pinY,
                9, FontWeights.Normal, Brushes.DimGray,
                centered: false, vCenter: true);

            // Short stub wire into the block
            AddLine(x - 10, pinY, x, pinY,
                Brushes.Black, 1.5);
        }

        for (int i = 0; i < outputs.Count; i++)
        {
            var pinY = y + HeaderHeight + i * PinSpacing;
            var pinPos = new Point(x + width, pinY);
            _pinPositions[outputs[i]] = pinPos;

            DrawPinDot(pinPos, Brushes.DarkBlue);
            AddText(outputs[i].Name ?? "", x + width - 8, pinY,
                9, FontWeights.Normal, Brushes.DimGray,
                centered: false, vCenter: true,
                alignRight: true);

            AddLine(x + width, pinY, x + width + 10, pinY,
                Brushes.Black, 1.5);
        }
    }

    private void DrawReference(FbdElement el, bool isInput)
    {
        var x = el.X * CoordScale;
        var y = el.Y * CoordScale;
        var centerY = y + RefHeight / 2;
        var tagName = el.Operands.FirstOrDefault() ?? "";

        var stroke = isInput
            ? new SolidColorBrush(Color.FromRgb(40, 120, 60))
            : new SolidColorBrush(Color.FromRgb(160, 50, 50));
        var fill = isInput
            ? new SolidColorBrush(Color.FromRgb(230, 248, 235))
            : new SolidColorBrush(Color.FromRgb(255, 235, 235));

        // Pill-shaped background
        var rect = new Rectangle
        {
            Width = RefWidth, Height = RefHeight,
            Stroke = stroke, StrokeThickness = 1.5,
            Fill = fill,
            RadiusX = RefHeight / 2, RadiusY = RefHeight / 2
        };
        Canvas.SetLeft(rect, x);
        Canvas.SetTop(rect, y);
        _canvas.Children.Add(rect);

        // Tag name centered
        var textColor = isInput ? Brushes.DarkGreen : Brushes.DarkRed;
        AddText(tagName, x + RefWidth / 2, centerY,
            11, FontWeights.SemiBold, textColor,
            centered: true, vCenter: true);

        // Pin and stub wire on the connection side
        if (isInput)
        {
            // Output pin on right (feeds into the next block)
            var pinPos = new Point(x + RefWidth, centerY);
            foreach (var conn in el.Connections)
                _pinPositions[conn] = pinPos;
            DrawPinDot(pinPos, stroke);
            AddLine(x + RefWidth, centerY, x + RefWidth + 12, centerY,
                Brushes.Black, 1.5);
        }
        else
        {
            // Input pin on left (receives from previous block)
            var pinPos = new Point(x, centerY);
            foreach (var conn in el.Connections)
                _pinPositions[conn] = pinPos;
            DrawPinDot(pinPos, stroke);
            AddLine(x - 12, centerY, x, centerY,
                Brushes.Black, 1.5);
        }
    }

    private void DrawConnector(FbdElement el, bool isInput)
    {
        var x = el.X * CoordScale;
        var y = el.Y * CoordScale;
        var centerY = y + RefHeight / 2;
        var label = el.Operands.FirstOrDefault() ?? el.Type;

        var rect = new Rectangle
        {
            Width = RefWidth, Height = RefHeight,
            Stroke = Brushes.DarkOrange,
            StrokeThickness = 1.5,
            Fill = new SolidColorBrush(Color.FromRgb(255, 248, 225)),
            RadiusX = 4, RadiusY = 4
        };
        Canvas.SetLeft(rect, x);
        Canvas.SetTop(rect, y);
        _canvas.Children.Add(rect);

        AddText(label, x + RefWidth / 2, centerY,
            10, FontWeights.Normal, Brushes.DarkOrange,
            centered: true, vCenter: true);

        var pinPos = isInput
            ? new Point(x + RefWidth, centerY)
            : new Point(x, centerY);

        foreach (var conn in el.Connections)
            _pinPositions[conn] = pinPos;
        DrawPinDot(pinPos, Brushes.DarkOrange);
    }

    // ═══════════════════════════════════════════
    //  Wire drawing
    // ═══════════════════════════════════════════

    private void DrawWire(Wire wire)
    {
        if (wire.From == null || wire.To == null) return;
        if (!_pinPositions.TryGetValue(wire.From, out var start)) return;
        if (!_pinPositions.TryGetValue(wire.To, out var end)) return;

        // Route the wire with an L-bend or Z-bend
        var points = RouteWire(start, end);

        var polyline = new Polyline
        {
            Stroke = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
            StrokeThickness = 1.5,
            StrokeLineJoin = PenLineJoin.Round,
            Points = new PointCollection(points)
        };
        _canvas.Children.Add(polyline);
    }

    private static Point[] RouteWire(Point start, Point end)
    {
        const double stubLength = 12;

        // If nearly horizontal, straight line
        if (Math.Abs(start.Y - end.Y) < 2)
            return [start, end];

        // Normal left-to-right flow: Z-shaped route
        if (end.X > start.X + stubLength * 2)
        {
            var midX = (start.X + end.X) / 2;
            return
            [
                start,
                new Point(midX, start.Y),
                new Point(midX, end.Y),
                end
            ];
        }

        // Feedback / tight spacing: route around with offset
        var offsetX = Math.Max(start.X, end.X) + 40;
        var offsetY = Math.Min(start.Y, end.Y) - 30;
        return
        [
            start,
            new Point(offsetX, start.Y),
            new Point(offsetX, offsetY),
            new Point(end.X - stubLength, offsetY),
            new Point(end.X - stubLength, end.Y),
            end
        ];
    }

    // ═══════════════════════════════════════════
    //  Drawing helpers
    // ═══════════════════════════════════════════

    private void DrawPinDot(Point pos, Brush fill)
    {
        var dot = new Ellipse
        {
            Width = PinDotSize, Height = PinDotSize,
            Fill = fill
        };
        Canvas.SetLeft(dot, pos.X - PinDotSize / 2);
        Canvas.SetTop(dot, pos.Y - PinDotSize / 2);
        _canvas.Children.Add(dot);
    }

    private void AddLine(double x1, double y1, double x2, double y2,
        Brush stroke, double thickness)
    {
        _canvas.Children.Add(new Line
        {
            X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
            Stroke = stroke, StrokeThickness = thickness
        });
    }

    private void AddText(string text, double x, double y,
        double fontSize, FontWeight weight, Brush foreground,
        bool centered = false, bool vCenter = false,
        bool alignRight = false)
    {
        var tb = new TextBlock
        {
            Text = text,
            FontFamily = new FontFamily("Consolas"),
            FontSize = fontSize,
            FontWeight = weight,
            Foreground = foreground
        };
        tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        var left = x;
        if (centered) left = x - tb.DesiredSize.Width / 2;
        else if (alignRight) left = x - tb.DesiredSize.Width;

        var top = vCenter ? y - tb.DesiredSize.Height / 2 : y;

        Canvas.SetLeft(tb, left);
        Canvas.SetTop(tb, top);
        _canvas.Children.Add(tb);
    }
}