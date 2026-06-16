using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Elfive.Core.L5X.Base;
using Elfive.Core.RLL;
using Parallel = Elfive.Core.RLL.Parallel;

namespace Elfive.App.Views;

public partial class RLLView : UserControl
{
    private const double CellHeight = 48;
    private const double RailWidth = 16;   // right-side inset
    private const double LeftMargin = 56;  // space left of left rail (rung number)
    private const double RungPadding = 24;
    private int _columnCount = 12;

    private readonly StackPanel _container = new();
    private readonly TextBlock _header;
    private Rung[] _displayedRungs;
    private IReadOnlyDictionary<string, string> _tagValues = new Dictionary<string, string>();
    public RLLView()
    {
        var scroll = new ScrollViewer
        {
            Content = _container,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
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
        dock.Children.Add(_header);
        dock.Children.Add(scroll);
        Content = dock;
        _displayedRungs = [];

        DataContextChanged += (s, e) => LoadRungs();
        SizeChanged += (s, e) => Render();
    }

    private void LoadRungs()
    {
        _displayedRungs = [];

        if (DataContext is TreeNode<IRoutine> node)
        {
            var programName = node.Parent?.Name ?? "";
            _header.Text = string.IsNullOrEmpty(programName)
                ? node.Name ?? ""
                : $"{node.Name}   —   {programName}";

            if (node.Source is { } routine)
            {
                var vm = Window.GetWindow(this)?.DataContext as MainViewModel;
                _displayedRungs = vm?.RoutineDb?.GetRungs(routine) ?? [];
            }
        }

        _columnCount = _displayedRungs.Length > 0
            ? Math.Max(8, _displayedRungs.Max(r => r.Size.Width) + 1)
            : 8;
        Render();
    }

    private double GetCellWidth()
    {
        var available = ActualWidth - LeftMargin - RailWidth;
        return available > 0 ? available / _columnCount : 60;
    }

    private void Render()
    {
        _container.Children.Clear();

        if (DataContext is not TreeNode node ||
            Window.GetWindow(this)?.DataContext is not MainViewModel vm)
            return;

        _tagValues = vm.ControllerTagValues;
        var cellWidth = GetCellWidth();
        var leftRailX = LeftMargin;
        var rightRailX = LeftMargin + _columnCount * cellWidth;
        var totalWidth = rightRailX + RailWidth;

        foreach (var rung in _displayedRungs)
        {
            // Rung comment header
            if (!string.IsNullOrEmpty(rung.Comment))
            {
                _container.Children.Add(new TextBlock
                {
                    Text = $"Rung {rung.Number}: {rung.Comment}",
                    Margin = new Thickness(LeftMargin, 8, 0, 2),
                    Foreground = Brushes.Green,
                    FontFamily = new FontFamily("Consolas"),
                    FontStyle = FontStyles.Italic
                });
            }

            // Ladder Render
            var rungHeight = rung.Size.Height * CellHeight;
            var canvas = new Canvas
            {
                Width = totalWidth,
                Height = rungHeight + RungPadding,
            };

            // Rung number (3 digits) to the left of the left rail
            var rungLabel = new TextBlock
            {
                Text = rung.Number.ToString("D3"),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Foreground = Brushes.DarkGray,
            };
            rungLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(rungLabel, 4);
            Canvas.SetTop(rungLabel, rungHeight / 2 - rungLabel.DesiredSize.Height / 2);
            canvas.Children.Add(rungLabel);

            // Left rail
            canvas.Children.Add(MakeLine(
                leftRailX, 0, leftRailX, rungHeight + RungPadding,
                Brushes.DarkBlue, 2));

            // Right rail
            canvas.Children.Add(MakeLine(
                rightRailX, 0, rightRailX, rungHeight + RungPadding,
                Brushes.DarkBlue, 2));

            // Draw rung content — output instructions pinned to right rail
            if (rung.Root.Elements.Count > 0 && IsOutput(rung.Root.Elements[^1]))
            {
                var output = rung.Root.Elements[^1];
                var outputWidth = LayoutCalculator.Measure(output).Width;
                var outputStartX = rightRailX - outputWidth * cellWidth;

                // Draw input elements from left rail
                var inputX = leftRailX;
                foreach (var el in rung.Root.Elements.Take(rung.Root.Elements.Count - 1))
                {
                    var elW = LayoutCalculator.Measure(el).Width;
                    DrawElement(canvas, el, inputX, 0, elW, cellWidth);
                    inputX += elW * cellWidth;
                }

                // Wire bridging input end to output start
                if (inputX < outputStartX)
                    canvas.Children.Add(MakeLine(
                        inputX, CellHeight / 2, outputStartX, CellHeight / 2,
                        Brushes.Black, 1));

                // Draw output at right rail
                DrawElement(canvas, output, outputStartX, 0, outputWidth, cellWidth);
            }
            else
            {
                DrawElement(canvas, rung.Root, leftRailX, 0, rung.Size.Width, cellWidth);

                var contentEndX = leftRailX + rung.Size.Width * cellWidth;
                if (contentEndX < rightRailX)
                    canvas.Children.Add(MakeLine(
                        contentEndX, CellHeight / 2, rightRailX, CellHeight / 2,
                        Brushes.Black, 1));
            }

            _container.Children.Add(canvas);
        }
    }

    private void DrawElement(Canvas canvas, IRungElement element,
        double x, double y, int elementWidth, double cellWidth)
    {
        switch (element)
        {
            case Instruction inst:
                DrawInstruction(canvas, inst, x, y, cellWidth);
                break;

            case Series series:
                var seriesX = x;
                foreach (var child in series.Elements)
                {
                    var childSize = LayoutCalculator.Measure(child);
                    DrawElement(canvas, child, seriesX, y, childSize.Width, cellWidth);
                    seriesX += childSize.Width * cellWidth;
                }
                break;

            case Parallel parallel:
                var branchY = y;
                var parallelWidth = LayoutCalculator.Measure(parallel).Width;
                const double vInset = 8.0; // gap between left rail/element and parallel bar
                var leftBar  = x + vInset;
                var rightBar = x + parallelWidth * cellWidth;

                foreach (var branch in parallel.Branches)
                {
                    var branchSize = LayoutCalculator.Measure(branch);
                    var wireY = branchY + CellHeight / 2;

                    // Start branches at leftBar so content begins after the left bar
                    DrawElement(canvas, branch, leftBar, branchY, branchSize.Width, cellWidth);

                    // Extend wire to rightBar if branch is narrower than the parallel block
                    var branchEnd = leftBar + branchSize.Width * cellWidth;
                    if (branchEnd < rightBar)
                    {
                        canvas.Children.Add(MakeLine(
                            branchEnd, wireY, rightBar, wireY,
                            Brushes.Black, 1));
                    }

                    branchY += branchSize.Height * CellHeight;
                }

                // Vertical connection bars at left gap and true right edge of the block
                var topWireY = y + CellHeight / 2;
                var bottomWireY = branchY - CellHeight / 2;
                canvas.Children.Add(MakeLine(
                    leftBar, topWireY, leftBar, bottomWireY, Brushes.Black, 1));
                canvas.Children.Add(MakeLine(
                    rightBar, topWireY, rightBar, bottomWireY, Brushes.Black, 1));
                break;
        }
    }

    private void DrawInstruction(Canvas canvas, Instruction inst,
        double x, double y, double cellWidth)
    {
        var centerY = y + CellHeight / 2;
        var rightEdge = x + cellWidth;

        // Semi-opaque highlight when the instruction's tag is energized (value = 1)
        if (BoolInstructions.Contains(inst.Name)
            && inst.Operands.Length > 0
            && _tagValues.TryGetValue(inst.Operands[0], out var tagVal)
            && tagVal == "1")
        {
            var highlight = new System.Windows.Shapes.Rectangle
            {
                Width = cellWidth*0.5,
                Height = CellHeight*0.25,
                Fill = new SolidColorBrush(Color.FromArgb(75, 0, 180, 0)),
            };
            Canvas.SetLeft(highlight, x + (cellWidth - highlight.Width) / 2 );
            Canvas.SetTop(highlight, y + (CellHeight - highlight.Height) / 2);
            canvas.Children.Add(highlight);
        }

        // Horizontal wire through the cell
        canvas.Children.Add(MakeLine(
            x, centerY, rightEdge, centerY, Brushes.Black, 1));

        var midX = x + cellWidth / 2;

        // Categorize and draw the symbol
        switch (inst.Name.ToUpper())
        {
            case "XIC": // Normally open contact: --| |--
                DrawContact(canvas, midX, centerY, false);
                break;
            case "XIO": // Normally closed contact: --|/|--
                DrawContact(canvas, midX, centerY, true);
                break;
            case "OTE": // Output energize: --( )--
                DrawCoil(canvas, midX, centerY, "");
                break;
            case "OTL": // Output latch: --(L)--
                DrawCoil(canvas, midX, centerY, "L");
                break;
            case "OTU": // Output unlatch: --(U)--
                DrawCoil(canvas, midX, centerY, "U");
                break;
            case "ONS": // One-shot: --[ONS]--
                DrawContact(canvas, midX, centerY, false, "↑");
                break;
            default: // Everything else: box instruction
                DrawBoxInstruction(canvas, inst, x, y, cellWidth);
                return; // skip the operand label below
        }

        // Operand label above the symbol
        if (inst.Operands.Length > 0)
        {
            var label = new TextBlock
            {
                Text = inst.Operands[0],
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
                Foreground = Brushes.DarkSlateGray
            };
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(label, midX - label.DesiredSize.Width / 2);
            Canvas.SetTop(label, centerY - 12 - label.DesiredSize.Height);
            canvas.Children.Add(label);
        }
    }

    private void DrawContact(Canvas canvas, double cx, double cy,
        bool closed, string? overlay = null)
    {
        double halfW = 8;
        double halfH = 12;

        // Left vertical line
        canvas.Children.Add(MakeLine(
            cx - halfW, cy - halfH, cx - halfW, cy + halfH,
            Brushes.Blue, 2));
        // Right vertical line
        canvas.Children.Add(MakeLine(
            cx + halfW, cy - halfH, cx + halfW, cy + halfH,
            Brushes.Blue, 2));

        // Diagonal slash for normally closed
        if (closed)
        {
            canvas.Children.Add(MakeLine(
                cx - halfW + 2, cy + halfH - 2,
                cx + halfW - 2, cy - halfH + 2,
                Brushes.Blue, 2));
        }

        // Overlay text (for ONS etc.)
        if (overlay != null)
        {
            var text = new TextBlock
            {
                Text = overlay, FontSize = 10,
                Foreground = Brushes.Blue, FontWeight = FontWeights.Bold
            };
            text.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(text, cx - text.DesiredSize.Width / 2);
            Canvas.SetTop(text, cy - text.DesiredSize.Height / 2);
            canvas.Children.Add(text);
        }
    }

    private void DrawCoil(Canvas canvas, double cx, double cy, string label)
    {
        double radius = 12;

        // Draw arcs as an ellipse outline
        var ellipse = new System.Windows.Shapes.Ellipse
        {
            Width = radius * 2, Height = radius * 2,
            Stroke = Brushes.Blue, StrokeThickness = 2,
            Fill = Brushes.Transparent
        };
        Canvas.SetLeft(ellipse, cx - radius);
        Canvas.SetTop(ellipse, cy - radius);
        canvas.Children.Add(ellipse);

        // Label inside coil (L, U, etc.)
        if (!string.IsNullOrEmpty(label))
        {
            var text = new TextBlock
            {
                Text = label, FontSize = 10,
                Foreground = Brushes.Blue, FontWeight = FontWeights.Bold
            };
            text.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(text, cx - text.DesiredSize.Width / 2);
            Canvas.SetTop(text, cy - text.DesiredSize.Height / 2);
            canvas.Children.Add(text);
        }
    }

    private void DrawBoxInstruction(Canvas canvas, Instruction inst,
        double x, double y, double cellWidth)
    {
        var margin = 12.0;
        var rect = new System.Windows.Shapes.Rectangle
        {
            Width = cellWidth - margin * 2,
            Height = CellHeight - 8,
            Stroke = Brushes.Blue, StrokeThickness = 1.5,
            Fill = Brushes.White
        };
        Canvas.SetLeft(rect, x + margin);
        Canvas.SetTop(rect, y + 4);
        canvas.Children.Add(rect);

        // Instruction name at top of box
        var nameLabel = new TextBlock
        {
            Text = inst.Name,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 11, FontWeight = FontWeights.Bold,
            Foreground = Brushes.DarkBlue
        };
        Canvas.SetLeft(nameLabel, x + margin + 4);
        Canvas.SetTop(nameLabel, y + 6);
        canvas.Children.Add(nameLabel);

        // Operands listed inside box
        for (int i = 0; i < inst.Operands.Length; i++)
        {
            var opLabel = new TextBlock
            {
                Text = inst.Operands[i],
                FontFamily = new FontFamily("Consolas"),
                FontSize = 9, Foreground = Brushes.DarkSlateGray
            };
            Canvas.SetLeft(opLabel, x + margin + 4);
            Canvas.SetTop(opLabel, y + 20 + i * 12);
            canvas.Children.Add(opLabel);
        }
    }

    private static readonly HashSet<string> InputInstructions = new(StringComparer.OrdinalIgnoreCase)
    {
        "XIC", "XIO", "ONS", "OSR", "OSF",
        "GRT", "GEQ", "LES", "LEQ", "EQU", "NEQ", "CMP"
    };

    private static readonly HashSet<string> BoolInstructions = new(StringComparer.OrdinalIgnoreCase)
    {
        "XIC", "XIO", "OTE", "OTL", "OTU", "ONS"
    };

    private static bool IsOutput(IRungElement element) =>
        element is Instruction inst && !InputInstructions.Contains(inst.Name);

    private static System.Windows.Shapes.Line MakeLine(
        double x1, double y1, double x2, double y2,
        Brush stroke, double thickness)
    {
        return new System.Windows.Shapes.Line
        {
            X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
            Stroke = stroke, StrokeThickness = thickness
        };
    }
}
