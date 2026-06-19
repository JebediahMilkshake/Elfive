using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Elfive.Core.L5X.Base;
using Elfive.Core.RLL;
using Elfive.Core.TAG;
using Parallel = Elfive.Core.RLL.Parallel;

namespace Elfive.App.Views;

public partial class RLLView : UserControl
{
    private const double CellHeight = 64;
    private const double BranchSpacing = 16;
    private const double RailWidth = 16;   // right-side inset
    private const double LeftMargin = 56;  // space left of left rail (rung number)
    private const double RungPadding = 24;
    private int _columnCount = 12;

    private readonly StackPanel _container = new();
    private readonly ScrollViewer _scroll;
    private readonly TextBlock _header;
    private Rung[] _displayedRungs;
    private IReadOnlyDictionary<string, string> _tagValues = new Dictionary<string, string>();
    private TagDatabase? _tagDb;
    private IProgram? _currentProgram;
    public RLLView()
    {
        _scroll = new ScrollViewer
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
        dock.Children.Add(_scroll);
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
                _tagDb = vm?.TagDb;
                _currentProgram = routine.Program;
            }
        }

        _columnCount = _displayedRungs.Length > 0
            ? Math.Max(8, _displayedRungs.Max(r => r.Size.Width) + 1)
            : 8;
        Render();
    }

    private double GetCellWidth()
    {
        var available = ActualWidth - LeftMargin - RailWidth - 20;
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
            // Measure comment so rails can span the full height (comment + ladder)
            double commentHeight = 0;
            TextBlock? commentBlock = null;
            if (!string.IsNullOrEmpty(rung.Comment))
            {
                commentBlock = new TextBlock
                {
                    Text = rung.Comment,
                    Foreground = Brushes.Green,
                    FontFamily = new FontFamily("Consolas"),
                    FontStyle = FontStyles.Italic,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = totalWidth - LeftMargin - 16,
                };
                commentBlock.Measure(new Size(totalWidth - LeftMargin - 16, double.PositiveInfinity));
                commentHeight = commentBlock.DesiredSize.Height + 12; // 6px top + 6px bottom
            }

            // Canvas covers comment region + ladder region + padding
            var rungHeight = MeasurePixelHeight(rung.Root);
            var canvasHeight = commentHeight + rungHeight + RungPadding;
            var canvas = new Canvas
            {
                Width = totalWidth,
                Height = canvasHeight,
                Tag = rung.Number,
            };

            // Comment inside canvas, indented slightly past left rail
            if (commentBlock != null)
            {
                Canvas.SetLeft(commentBlock, LeftMargin + 24);
                Canvas.SetTop(commentBlock, 6);
                canvas.Children.Add(commentBlock);
            }

            // Rung number centred on the ladder section (below the comment)
            var rungLabel = new TextBlock
            {
                Text = rung.Number.ToString("D3"),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Foreground = Brushes.DarkGray,
            };
            rungLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(rungLabel, 4);
            Canvas.SetTop(rungLabel, commentHeight + rungHeight / 2 - rungLabel.DesiredSize.Height / 2);
            canvas.Children.Add(rungLabel);

            // Left rail — full height including comment
            canvas.Children.Add(MakeLine(
                leftRailX, 0, leftRailX, canvasHeight,
                Brushes.DarkBlue, 2));

            // Right rail — full height including comment
            canvas.Children.Add(MakeLine(
                rightRailX, 0, rightRailX, canvasHeight,
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
                    DrawElement(canvas, el, inputX, commentHeight, elW, cellWidth);
                    inputX += elW * cellWidth;
                }

                // Wire bridging input end to output start
                if (inputX < outputStartX)
                    canvas.Children.Add(MakeLine(
                        inputX, commentHeight + CellHeight / 2,
                        outputStartX, commentHeight + CellHeight / 2,
                        Brushes.Black, 1));

                // Draw output at right rail
                DrawElement(canvas, output, outputStartX, commentHeight, outputWidth, cellWidth);
            }
            else
            {
                DrawElement(canvas, rung.Root, leftRailX, commentHeight, rung.Size.Width, cellWidth);

                var contentEndX = leftRailX + rung.Size.Width * cellWidth;
                if (contentEndX < rightRailX)
                    canvas.Children.Add(MakeLine(
                        contentEndX, commentHeight + CellHeight / 2,
                        rightRailX, commentHeight + CellHeight / 2,
                        Brushes.Black, 1));
            }

            _container.Children.Add(canvas);
        }
    }

    public void ScrollToRung(ulong rungNumber)
    {
        foreach (UIElement child in _container.Children)
        {
            if (child is not Canvas canvas || canvas.Tag is not ulong num || num != rungNumber)
                continue;
            var childTop = child.TranslatePoint(new Point(0, 0), _container).Y;
            var offset = childTop + canvas.Height / 2 - _scroll.ViewportHeight / 2;
            _scroll.ScrollToVerticalOffset(Math.Max(0, offset));
            return;
        }
    }

    private static double MeasurePixelHeight(IRungElement element) => element switch
    {
        Instruction   => CellHeight,
        Series s      => s.Elements.Count > 0 ? s.Elements.Max(MeasurePixelHeight) : CellHeight,
        Parallel p    => p.Branches.Sum(MeasurePixelHeight) + Math.Max(0, p.Branches.Count - 1) * BranchSpacing,
        _             => CellHeight,
    };

    private double DrawElement(Canvas canvas, IRungElement element,
        double x, double y, int elementWidth, double cellWidth)
    {
        switch (element)
        {
            case Instruction inst:
                DrawInstruction(canvas, inst, x, y, cellWidth);
                return CellHeight;

            case Series series:
                var seriesX = x;
                var maxH = 0.0;
                foreach (var child in series.Elements)
                {
                    var childSize = LayoutCalculator.Measure(child);
                    var h = DrawElement(canvas, child, seriesX, y, childSize.Width, cellWidth);
                    maxH = Math.Max(maxH, h);
                    seriesX += childSize.Width * cellWidth;
                }
                return maxH > 0 ? maxH : CellHeight;

            case Parallel parallel:
                var branchY = y;
                var parallelWidth = LayoutCalculator.Measure(parallel).Width;
                const double vInset = 8.0;
                var leftBar  = x + vInset;
                var rightBar = x + parallelWidth * cellWidth - vInset;
                var topWireY = y + CellHeight / 2;
                var bottomWireY = topWireY;

                for (var bi = 0; bi < parallel.Branches.Count; bi++)
                {
                    var branch = parallel.Branches[bi];
                    var branchSize = LayoutCalculator.Measure(branch);
                    var wireY = branchY + CellHeight / 2;
                    bottomWireY = wireY;

                    var branchH = DrawElement(canvas, branch, leftBar, branchY, branchSize.Width, cellWidth);

                    // Extend wire to rightBar if branch is narrower than the parallel block
                    var branchEnd = leftBar + branchSize.Width * cellWidth;
                    if (branchEnd < rightBar)
                        canvas.Children.Add(MakeLine(
                            branchEnd, wireY, rightBar, wireY, Brushes.Black, 1));

                    branchY += branchH;
                    if (bi < parallel.Branches.Count - 1)
                        branchY += BranchSpacing;
                }

                // Entry wire (x → leftBar) and exit wire (rightBar → allocated end)
                canvas.Children.Add(MakeLine(x,        topWireY, leftBar,                       topWireY, Brushes.Black, 1));
                canvas.Children.Add(MakeLine(rightBar, topWireY, x + parallelWidth * cellWidth, topWireY, Brushes.Black, 1));

                // Vertical bars span from first branch wire center to last branch wire center
                canvas.Children.Add(MakeLine(leftBar,  topWireY, leftBar,  bottomWireY, Brushes.Black, 1));
                canvas.Children.Add(MakeLine(rightBar, topWireY, rightBar, bottomWireY, Brushes.Black, 1));

                return branchY - y;

            default:
                return CellHeight;
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

        var description = inst.Operands.Length > 0
            ? _tagDb?.GetDescription(inst.Operands[0], _currentProgram)
            : null;

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
                DrawBoxInstruction(canvas, inst, x, y, cellWidth, description);
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
                Foreground = Brushes.DarkSlateGray,
                MaxWidth = cellWidth,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(label, midX - label.DesiredSize.Width / 2);
            Canvas.SetTop(label, centerY - 12 - label.DesiredSize.Height);
            canvas.Children.Add(label);

            if (!string.IsNullOrEmpty(description))
            {
                var descLabel = new TextBlock
                {
                    Text = description,
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 8,
                    FontStyle = FontStyles.Italic,
                    Foreground = Brushes.Gray,
                    MaxWidth = cellWidth,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                descLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(descLabel, midX - descLabel.DesiredSize.Width / 2);
                Canvas.SetTop(descLabel, centerY - 12 - label.DesiredSize.Height - 2 - descLabel.DesiredSize.Height);
                canvas.Children.Add(descLabel);
            }
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
        double x, double y, double cellWidth, string? description = null)
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

        // Description above the box
        if (!string.IsNullOrEmpty(description))
        {
            var descLabel = new TextBlock
            {
                Text = description,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 8,
                FontStyle = FontStyles.Italic,
                Foreground = Brushes.Gray,
                MaxWidth = cellWidth,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            descLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(descLabel, x + cellWidth / 2 - descLabel.DesiredSize.Width / 2);
            Canvas.SetTop(descLabel, y + 4 - 2 - descLabel.DesiredSize.Height);
            canvas.Children.Add(descLabel);
        }

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
