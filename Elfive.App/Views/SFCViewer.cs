using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Xml;
using Elfive.Core.L5X.Base;
using Elfive.Core.SFC;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;

namespace Elfive.App.Views;

public class SFCViewer : UserControl
{
    private const double CoordScale  = 2.2;
    private const double StepWidth   = 130;
    private const double StepHeight  = 50;
    private const double TransWidth  = 90;
    private const double TransHeight = 24;
    private const double StopRadius  = 18;
    private const double ActionRowH  = 18;
    private const double ActionQualW = 30;
    private const double ArrowSize   = 7;
    private const double AreaPadding = 80;

    private static IHighlightingDefinition? _stHighlighting;

    private double _zoom = 1.0;
    private readonly ScaleTransform _scaleTransform;
    private readonly Canvas _canvas;
    private bool _isPanning;
    private Point _panStartMouse;
    private Point _panStartScroll;
    private readonly ScrollViewer _scroll;
    private readonly TextBlock _header;

    // ID → (centerX, topY, bottomY) for all positioned elements (steps, transitions, stops, branches)
    private record ElementInfo(double CX, double TopY, double BottomY);
    private readonly Dictionary<ulong, ElementInfo> _positions = new();
    private readonly Dictionary<string, ElementInfo> _positionsByName = new(StringComparer.OrdinalIgnoreCase);

    // Leg ID → position on branch bar
    private readonly Dictionary<ulong, double> _legX = new();
    private readonly Dictionary<ulong, double> _legY = new();

    public SFCViewer()
    {
        _scaleTransform = new ScaleTransform(1, 1);
        _canvas = new Canvas
        {
            Background = Brushes.White,
            LayoutTransform = _scaleTransform
        };
        _scroll = new ScrollViewer
        {
            Content = _canvas,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility   = ScrollBarVisibility.Auto
        };
        _scroll.PreviewMouseLeftButtonDown += OnPanStart;
        _scroll.PreviewMouseMove           += OnPanMove;
        _scroll.PreviewMouseLeftButtonUp   += OnPanEnd;
        _scroll.MouseLeave                 += (_, _) => EndPan();

        _header = new TextBlock
        {
            FontFamily = new FontFamily("Consolas"),
            FontSize   = 11,
            FontWeight = FontWeights.SemiBold,
            Padding    = new Thickness(8, 4, 8, 4),
            Background = new SolidColorBrush(Color.FromRgb(235, 245, 235)),
            Foreground = new SolidColorBrush(Color.FromRgb(20, 90, 20))
        };

        var dock = new DockPanel();
        DockPanel.SetDock(_header, Dock.Top);
        dock.Children.Add(_header);
        dock.Children.Add(_scroll);
        Content = dock;

        DataContextChanged += (_, _) => Load();
        PreviewMouseWheel  += OnMouseWheel;
        LoadSyntaxHighlighting();
    }

    // ── Zoom / pan ────────────────────────────────────────────────────────────

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control) return;
        e.Handled = true;
        var oldZoom = _zoom;
        _zoom = Math.Clamp(_zoom * (e.Delta > 0 ? 1.15 : 0.87), 0.2, 5.0);
        var mouse   = e.GetPosition(_scroll);
        var canvasX = (_scroll.HorizontalOffset + mouse.X) / oldZoom;
        var canvasY = (_scroll.VerticalOffset   + mouse.Y) / oldZoom;
        _scaleTransform.ScaleX = _zoom;
        _scaleTransform.ScaleY = _zoom;
        Dispatcher.BeginInvoke(() =>
        {
            _scroll.ScrollToHorizontalOffset(canvasX * _zoom - mouse.X);
            _scroll.ScrollToVerticalOffset  (canvasY * _zoom - mouse.Y);
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void OnPanStart(object sender, MouseButtonEventArgs e)
    {
        _isPanning      = true;
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
        _scroll.ScrollToVerticalOffset  (_panStartScroll.Y - (cur.Y - _panStartMouse.Y));
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

    public void ScrollToElement(string name)
    {
        if (!_positionsByName.TryGetValue(name, out var info)) return;
        var cx = info.CX * _zoom;
        var cy = ((info.TopY + info.BottomY) / 2) * _zoom;
        _scroll.ScrollToHorizontalOffset(Math.Max(0, cx - _scroll.ViewportWidth / 2));
        _scroll.ScrollToVerticalOffset(Math.Max(0, cy - _scroll.ViewportHeight / 2));
    }

    // ── Load / header ─────────────────────────────────────────────────────────

    private void Load()
    {
        _header.Text = DataContext is TreeNode<IRoutine> node
            ? (string.IsNullOrEmpty(node.Parent?.Name)
                ? node.Name ?? ""
                : $"{node.Name}   —   {node.Parent!.Name}")
            : "";
        Render();
    }

    private ISfcContent? GetContent() =>
        (DataContext as TreeNode<IRoutine>)?.Source?.Content as ISfcContent;

    // ── Render ────────────────────────────────────────────────────────────────

    private void Render()
    {
        _canvas.Children.Clear();
        _positions.Clear();
        _positionsByName.Clear();
        _legX.Clear();
        _legY.Clear();

        var sfc = GetContent();
        if (sfc is null) return;

        var steps       = sfc.Steps.ToList();
        var transitions = sfc.Transitions.ToList();
        var branches    = sfc.Branches.ToList();
        var stops       = sfc.Stops.ToList();
        var links       = sfc.DirectedLinks.ToList();

        if (steps.Count == 0 && transitions.Count == 0) return;

        // ── Build position cache for all elements ──────────────────────────────

        foreach (var s in steps)
        {
            var x  = s.X * CoordScale;
            var y  = s.Y * CoordScale;
            var actionCount = s.Actions?.Count() ?? 0;
            var totalH = StepHeight + actionCount * ActionRowH;
            var info = new ElementInfo(x + StepWidth / 2, y, y + totalH);
            _positions[s.Id] = info;
            if (s.Operand is not null) _positionsByName[s.Operand] = info;
        }
        foreach (var t in transitions)
        {
            var x = t.X * CoordScale;
            var y = t.Y * CoordScale;
            var info = new ElementInfo(x + TransWidth / 2, y, y + TransHeight);
            _positions[t.Id] = info;
            if (t.Operand is not null) _positionsByName[t.Operand] = info;
        }
        foreach (var stop in stops)
        {
            var x = stop.X * CoordScale;
            var y = stop.Y * CoordScale;
            _positions[stop.Id] = new ElementInfo(x + StopRadius, y, y + StopRadius * 2);
        }

        // Branches need step/transition positions to already be known
        foreach (var branch in branches)
        {
            var branchY = branch.Y * CoordScale;
            var legIds  = branch.LegIds.ToHashSet();

            // Resolve each leg's X from its connected step/transition
            foreach (var legId in legIds)
            {
                var legLink = links.FirstOrDefault(l => l.FromId == legId || l.ToId == legId);
                if (legLink is null) continue;
                var connId = legLink.FromId == legId ? legLink.ToId : legLink.FromId;
                if (!_positions.TryGetValue(connId, out var conn)) continue;
                _legX[legId] = conn.CX;
                _legY[legId] = branchY;
            }

            var legXs    = legIds.Where(_legX.ContainsKey).Select(id => _legX[id]).ToList();
            var branchCX = legXs.Count > 0 ? (legXs.Min() + legXs.Max()) / 2 : 0;
            _positions[branch.Id] = new ElementInfo(branchCX, branchY - 4, branchY + 4);
        }

        // ── Size canvas ────────────────────────────────────────────────────────

        if (_positions.Count > 0)
        {
            _canvas.Width  = _positions.Values.Max(p => p.CX) + StepWidth / 2 + AreaPadding * 2;
            _canvas.Height = _positions.Values.Max(p => p.BottomY)             + AreaPadding * 2;
        }

        // ── Draw: branches first so they appear behind elements ───────────────

        foreach (var branch in branches)
            DrawBranch(branch);

        foreach (var s in steps)
            DrawStep(s);
        foreach (var t in transitions)
            DrawTransition(t);
        foreach (var stop in stops)
            DrawStop(stop);
        foreach (var link in links)
            DrawLink(link);
    }

    // ── Steps ─────────────────────────────────────────────────────────────────

    private void DrawStep(ISfcStep step)
    {
        var x = step.X * CoordScale;
        var y = step.Y * CoordScale;

        var rect = new Rectangle
        {
            Width  = StepWidth,
            Height = StepHeight,
            Stroke          = new SolidColorBrush(Color.FromRgb(30, 100, 30)),
            StrokeThickness = 2,
            Fill            = new SolidColorBrush(Color.FromRgb(228, 248, 228)),
            RadiusX = 3, RadiusY = 3
        };
        Canvas.SetLeft(rect, x);
        Canvas.SetTop(rect, y);
        _canvas.Children.Add(rect);

        AddText(step.Operand ?? $"[{step.Id}]",
            x + StepWidth / 2, y + StepHeight / 2,
            12, FontWeights.Bold,
            new SolidColorBrush(Color.FromRgb(15, 65, 15)),
            centered: true, vCenter: true);

        var actions = step.Actions?.ToList() ?? [];
        for (int i = 0; i < actions.Count; i++)
            DrawActionRow(actions[i], x, y + StepHeight + i * ActionRowH);
    }

    private void DrawActionRow(ISfcAction action, double stepX, double rowY)
    {
        var qualStroke = new SolidColorBrush(Color.FromRgb(90, 140, 90));
        var qualFill   = new SolidColorBrush(Color.FromRgb(238, 252, 238));

        var qualRect = new Rectangle
        {
            Width = ActionQualW, Height = ActionRowH,
            Stroke = qualStroke, StrokeThickness = 1, Fill = qualFill
        };
        Canvas.SetLeft(qualRect, stepX);
        Canvas.SetTop(qualRect, rowY);
        _canvas.Children.Add(qualRect);

        AddText(action.Qualifier?.ToString() ?? "",
            stepX + ActionQualW / 2, rowY + ActionRowH / 2,
            8.5, FontWeights.SemiBold,
            new SolidColorBrush(Color.FromRgb(40, 90, 40)),
            centered: true, vCenter: true);

        var bodyLines = action.Body?.ToList();
        var isCodeAction = bodyLines is { Count: > 0 };

        var bodyFill = isCodeAction
            ? new SolidColorBrush(Color.FromRgb(248, 248, 255))
            : Brushes.White;

        var bodyRect = new Rectangle
        {
            Width = StepWidth - ActionQualW, Height = ActionRowH,
            Stroke = qualStroke, StrokeThickness = 1, Fill = bodyFill
        };

        if (isCodeAction)
        {
            var editor = new TextEditor
            {
                FontFamily             = new FontFamily("Consolas"),
                FontSize               = 11,
                IsReadOnly             = true,
                ShowLineNumbers        = false,
                Text                   = string.Join("\n", bodyLines!.Select(l => l.Text ?? "")),
                SyntaxHighlighting     = _stHighlighting,
                Width                  = 320,
                Height                 = Math.Min(bodyLines!.Count * 16 + 8, 300),
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility   = ScrollBarVisibility.Auto
            };
            bodyRect.ToolTip = new ToolTip
            {
                Content   = editor,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Right
            };
        }

        Canvas.SetLeft(bodyRect, stepX + ActionQualW);
        Canvas.SetTop(bodyRect, rowY);
        _canvas.Children.Add(bodyRect);

        var label = isCodeAction
            ? $"[ST]  {bodyLines![0].Text?.Trim()}"
            : action.Operand ?? "";

        AddText(label,
            stepX + ActionQualW + 4, rowY + ActionRowH / 2,
            9, FontWeights.Normal, Brushes.Black,
            vCenter: true);
    }

    // ── Transitions ───────────────────────────────────────────────────────────

    private void DrawTransition(ISfcTransition trans)
    {
        var x       = trans.X * CoordScale;
        var y       = trans.Y * CoordScale;
        var centerX = x + TransWidth / 2;
        var midY    = y + TransHeight / 2;
        var stroke  = new SolidColorBrush(Color.FromRgb(50, 50, 160));

        AddLine(centerX, y, centerX, y + TransHeight, stroke, 2);
        AddLine(x, midY, x + TransWidth, midY, stroke, 2.5);

        var lines = trans.Condition?.ToList();
        if (lines is { Count: > 0 })
            DrawConditionEditor(lines, x + TransWidth + 8, y);
        else if (!string.IsNullOrEmpty(trans.Operand))
            AddText(trans.Operand, x + TransWidth + 8, midY, 10, FontWeights.Normal, stroke, vCenter: true);
    }

    private void DrawConditionEditor(List<IStLine> lines, double x, double y)
    {
        const double lineHeight  = 14;
        const double editorWidth = 220;
        var editorHeight = Math.Max(lineHeight, lines.Count * lineHeight) + 4;

        var text = string.Join("\n", lines.Select(l => l.Text ?? ""));

        var editor = new TextEditor
        {
            FontFamily        = new FontFamily("Consolas"),
            FontSize          = 10,
            IsReadOnly        = true,
            ShowLineNumbers   = false,
            WordWrap          = false,
            Text              = text,
            SyntaxHighlighting = _stHighlighting,
            Width             = editorWidth,
            Height            = editorHeight,
            Background        = new SolidColorBrush(Color.FromRgb(250, 250, 255)),
            BorderThickness   = new Thickness(1),
            BorderBrush       = new SolidColorBrush(Color.FromRgb(180, 180, 220)),
            Padding           = new Thickness(2, 1, 2, 1),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility   = ScrollBarVisibility.Hidden
        };

        Canvas.SetLeft(editor, x);
        Canvas.SetTop(editor, y);
        _canvas.Children.Add(editor);
    }

    private static void LoadSyntaxHighlighting()
    {
        if (_stHighlighting != null) return;
        var path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Syntax", "StructuredText.xshd");
        if (!System.IO.File.Exists(path)) return;
        using var reader = new XmlTextReader(path);
        _stHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
    }

    // ── Stops ─────────────────────────────────────────────────────────────────

    private void DrawStop(ISfcStop stop)
    {
        var x = stop.X * CoordScale;
        var y = stop.Y * CoordScale;

        var ellipse = new Ellipse
        {
            Width  = StopRadius * 2,
            Height = StopRadius * 2,
            Stroke          = new SolidColorBrush(Color.FromRgb(160, 30, 30)),
            StrokeThickness = 2.5,
            Fill            = new SolidColorBrush(Color.FromRgb(255, 232, 232))
        };
        Canvas.SetLeft(ellipse, x);
        Canvas.SetTop(ellipse, y);
        _canvas.Children.Add(ellipse);

        AddText(!string.IsNullOrEmpty(stop.Operand) ? stop.Operand : "STOP",
            x + StopRadius, y + StopRadius,
            9, FontWeights.Bold,
            new SolidColorBrush(Color.FromRgb(160, 30, 30)),
            centered: true, vCenter: true);
    }

    // ── Branches ──────────────────────────────────────────────────────────────

    private void DrawBranch(ISfcBranch branch)
    {
        if (!_positions.TryGetValue(branch.Id, out _)) return;

        var branchY = branch.Y * CoordScale;
        var legIds  = branch.LegIds.Where(_legX.ContainsKey).ToList();
        if (legIds.Count < 2) return;

        var leftX  = legIds.Min(id => _legX[id]);
        var rightX = legIds.Max(id => _legX[id]);
        var stroke = new SolidColorBrush(Color.FromRgb(40, 40, 40));

        AddLine(leftX, branchY, rightX, branchY, stroke, 2.5);
        if (branch.BranchKind == SfcBranchKind.Simultaneous)
            AddLine(leftX, branchY + 6, rightX, branchY + 6, stroke, 2.5);
    }

    // ── Directed links ────────────────────────────────────────────────────────

    private void DrawLink(ISfcDirectedLink link)
    {
        if (!TryGetLinkPoint(link.FromId, bottom: true,  out var from)) return;
        if (!TryGetLinkPoint(link.ToId,   bottom: false, out var to))   return;

        var stroke = new SolidColorBrush(Color.FromRgb(50, 50, 50));

        if (Math.Abs(from.X - to.X) < 2)
        {
            AddLine(from.X, from.Y, to.X, to.Y, stroke, 1.5);
        }
        else
        {
            var midY = (from.Y + to.Y) / 2;
            var poly = new Polyline
            {
                Stroke          = stroke,
                StrokeThickness = 1.5,
                StrokeLineJoin  = PenLineJoin.Round,
                Points          = new PointCollection([from, new Point(from.X, midY), new Point(to.X, midY), to])
            };
            _canvas.Children.Add(poly);
        }

        DrawArrowhead(to, stroke);
    }

    private bool TryGetLinkPoint(ulong id, bool bottom, out Point point)
    {
        if (_positions.TryGetValue(id, out var info))
        {
            point = bottom ? new Point(info.CX, info.BottomY) : new Point(info.CX, info.TopY);
            return true;
        }
        if (_legX.TryGetValue(id, out var lx) && _legY.TryGetValue(id, out var ly))
        {
            point = new Point(lx, ly);
            return true;
        }
        point = default;
        return false;
    }

    private void DrawArrowhead(Point tip, Brush fill)
    {
        _canvas.Children.Add(new Polygon
        {
            Points          = new PointCollection([tip, new Point(tip.X - ArrowSize / 2, tip.Y - ArrowSize), new Point(tip.X + ArrowSize / 2, tip.Y - ArrowSize)]),
            Fill            = fill,
            Stroke          = fill,
            StrokeThickness = 1
        });
    }

    // ── Drawing helpers ───────────────────────────────────────────────────────

    private void AddLine(double x1, double y1, double x2, double y2, Brush stroke, double thickness)
    {
        _canvas.Children.Add(new Line
        {
            X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
            Stroke = stroke, StrokeThickness = thickness
        });
    }

    private void AddText(string text, double x, double y,
        double fontSize, FontWeight weight, Brush foreground,
        bool centered = false, bool vCenter = false)
    {
        var tb = new TextBlock
        {
            Text       = text,
            FontFamily = new FontFamily("Consolas"),
            FontSize   = fontSize,
            FontWeight = weight,
            Foreground = foreground
        };
        tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        Canvas.SetLeft(tb, centered ? x - tb.DesiredSize.Width  / 2 : x);
        Canvas.SetTop (tb, vCenter  ? y - tb.DesiredSize.Height / 2 : y);
        _canvas.Children.Add(tb);
    }
}
