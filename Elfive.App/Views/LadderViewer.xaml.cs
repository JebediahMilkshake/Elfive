using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Elfive.Core.RLL;
using Parallel = Elfive.Core.RLL.Parallel;

namespace Elfive.App.Views;

public partial class LadderViewer : UserControl
{
    private const double CellWidth = 160;
    private const double CellHeight = 48;
    private const double RailWidth = 16;

    private readonly RungParser _parser = new();
    private readonly StackPanel _container = new();
    public LadderViewer()
    {
        var scroll = new ScrollViewer
        {
            Content = _container,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        Content = scroll;

        DataContextChanged += (s, e) => Render();
    }
     private void Render()
    {
        _container.Children.Clear();

        if (DataContext is not TreeNode node ||
            Window.GetWindow(this)?.DataContext is not MainViewModel vm)
            return;

        var rungTexts = MainViewModel.GetRungText(node); // you'll need to expose this

        foreach (var (text, comment, number) in rungTexts)
        {
            // Rung comment header
            if (!string.IsNullOrEmpty(comment))
            {
                _container.Children.Add(new TextBlock
                {
                    Text = $"Rung {number}: {comment}",
                    Margin = new Thickness(RailWidth, 8, 0, 2),
                    Foreground = Brushes.Green,
                    FontFamily = new FontFamily("Consolas"),
                    FontStyle = FontStyles.Italic
                });
            }

            // Parse and render
            var series = _parser.Parse(text);
            var size = LayoutCalculator.Measure(series);
            var canvas = new Canvas
            {
                Width = RailWidth + (size.Width * CellWidth) + RailWidth,
                Height = size.Height * CellHeight,
                Margin = new Thickness(0, 0, 0, 4)
            };

            // Draw left rail
            canvas.Children.Add(MakeLine(
                RailWidth, 0, RailWidth, size.Height * CellHeight,
                Brushes.DarkBlue, 2));

            // Draw right rail
            var rightX = RailWidth + size.Width * CellWidth;
            canvas.Children.Add(MakeLine(
                rightX, 0, rightX, size.Height * CellHeight,
                Brushes.DarkBlue, 2));

            // Draw the rung content
            DrawElement(canvas, series, RailWidth, 0, size.Width);

            _container.Children.Add(canvas);
        }
    }

    private void DrawElement(Canvas canvas, IRungElement element,
        double x, double y, int availableWidth)
    {
        switch (element)
        {
            case Instruction inst:
                DrawInstruction(canvas, inst, x, y);
                break;

            case Series series:
                var seriesX = x;
                foreach (var child in series.Elements)
                {
                    var childSize = LayoutCalculator.Measure(child);
                    DrawElement(canvas, child, seriesX, y, childSize.Width);
                    seriesX += childSize.Width * CellWidth;
                }
                break;

            case Parallel parallel:
                var branchY = y;
                var parallelWidth = LayoutCalculator.Measure(parallel).Width;
                foreach (var branch in parallel.Branches)
                {
                    var branchSize = LayoutCalculator.Measure(branch);

                    // Horizontal wire from left edge to branch start
                    var wireY = branchY + CellHeight / 2;
                    canvas.Children.Add(MakeLine(
                        x, wireY, x, wireY, Brushes.Black, 1));

                    // Draw branch content
                    DrawElement(canvas, branch, x, branchY, parallelWidth);

                    // Extend wire to fill available width if branch is shorter
                    var branchWidth = LayoutCalculator.Measure(branch).Width;
                    if (branchWidth < parallelWidth)
                    {
                        var wireEnd = x + parallelWidth * CellWidth;
                        var wireStart = x + branchWidth * CellWidth;
                        canvas.Children.Add(MakeLine(
                            wireStart, wireY, wireEnd, wireY,
                            Brushes.Black, 1));
                    }

                    branchY += branchSize.Height * CellHeight;
                }

                // Vertical connections on left and right of branch
                var topWireY = y + CellHeight / 2;
                var bottomWireY = branchY - CellHeight / 2;
                // Left vertical
                canvas.Children.Add(MakeLine(
                    x, topWireY, x, bottomWireY, Brushes.Black, 1));
                // Right vertical
                var rightEdge = x + parallelWidth * CellWidth;
                canvas.Children.Add(MakeLine(
                    rightEdge, topWireY, rightEdge, bottomWireY,
                    Brushes.Black, 1));
                break;
        }
    }

    private void DrawInstruction(Canvas canvas, Instruction inst,
        double x, double y)
    {
        var centerY = y + CellHeight / 2;
        var rightEdge = x + CellWidth;

        // Horizontal wire through the cell
        canvas.Children.Add(MakeLine(
            x, centerY, rightEdge, centerY, Brushes.Black, 1));

        var midX = x + CellWidth / 2;

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
                DrawBoxInstruction(canvas, inst, x, y);
                return; // skip the operand label below
        }

        // Operand label below the symbol
        if (inst.Arguments.Length > 0)
        {
            var label = new TextBlock
            {
                Text = inst.Arguments[0],
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
                Foreground = Brushes.DarkSlateGray
            };
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(label, midX - label.DesiredSize.Width / 2);
            Canvas.SetTop(label, centerY + 10);
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
        double x, double y)
    {
        var margin = 12.0;
        var rect = new System.Windows.Shapes.Rectangle
        {
            Width = CellWidth - margin * 2,
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
        for (int i = 0; i < inst.Arguments.Length; i++)
        {
            var opLabel = new TextBlock
            {
                Text = inst.Arguments[i],
                FontFamily = new FontFamily("Consolas"),
                FontSize = 9, Foreground = Brushes.DarkSlateGray
            };
            Canvas.SetLeft(opLabel, x + margin + 4);
            Canvas.SetTop(opLabel, y + 20 + i * 12);
            canvas.Children.Add(opLabel);
        }
    }

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