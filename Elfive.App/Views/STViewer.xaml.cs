// Views/RoutineViewer.xaml.cs

using System.Windows;
using System.Windows.Controls;
using System.Xml;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;

namespace Elfive.App.Views;

public partial class STViewer : UserControl
{
    private readonly TextEditor _editor;
    private static IHighlightingDefinition? _stHighlighting;

    public static readonly DependencyProperty RoutineContentProperty =
        DependencyProperty.Register(nameof(RoutineContent), typeof(string), typeof(STViewer),
            new PropertyMetadata("", OnRoutineContentChanged));

    public static readonly DependencyProperty RoutineTypeProperty =
        DependencyProperty.Register(nameof(RoutineType), typeof(string), typeof(STViewer),
            new PropertyMetadata("", OnRoutineTypeChanged));

    public string RoutineContent
    {
        get => (string)GetValue(RoutineContentProperty);
        set => SetValue(RoutineContentProperty, value);
    }

    public string RoutineType
    {
        get => (string)GetValue(RoutineTypeProperty);
        set => SetValue(RoutineTypeProperty, value);
    }

    public static readonly DependencyProperty RoutineHeaderProperty =
        DependencyProperty.Register(nameof(RoutineHeader), typeof(string), typeof(STViewer),
            new PropertyMetadata("", OnRoutineHeaderChanged));

    public string RoutineHeader
    {
        get => (string)GetValue(RoutineHeaderProperty);
        set => SetValue(RoutineHeaderProperty, value);
    }

    public STViewer()
    {
        InitializeComponent();

        _editor = new TextEditor
        {
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            FontSize = 13,
            IsReadOnly = true,
            ShowLineNumbers = true,
            WordWrap = false
        };

        var header = new TextBlock
        {
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Padding = new Thickness(8, 4, 8, 4),
            Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(235, 240, 250)),
            Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(30, 60, 120))
        };
        header.SetBinding(TextBlock.TextProperty,
            new System.Windows.Data.Binding(nameof(RoutineHeader)) { Source = this });

        var dock = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        dock.Children.Add(header);
        dock.Children.Add(_editor);
        Content = dock;

        LoadSyntaxHighlighting();
    }

    private static void OnRoutineHeaderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // header text is bound directly — no imperative update needed
    }

    private static void OnRoutineContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var viewer = (STViewer)d;
        viewer._editor.Text = (string)e.NewValue;
    }

    private static void OnRoutineTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var viewer = (STViewer)d;
        viewer._editor.SyntaxHighlighting = (string)e.NewValue == "ST" ? _stHighlighting : null;
    }

    private void LoadSyntaxHighlighting()
    {
        if (_stHighlighting != null) return;

        var path = System.IO.Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "Syntax", "StructuredText.xshd");

        if (!System.IO.File.Exists(path)) return;

        using var reader = new XmlTextReader(path);
        _stHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
    }
}
