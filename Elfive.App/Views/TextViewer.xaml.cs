// Views/RoutineViewer.xaml.cs

using System.Windows;
using System.Windows.Controls;
using System.Xml;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;

namespace Elfive.App.Views;

public partial class TextViewer : UserControl
{
    private readonly TextEditor _editor;
    private static IHighlightingDefinition? _stHighlighting;

    public static readonly DependencyProperty RoutineContentProperty =
        DependencyProperty.Register(nameof(RoutineContent), typeof(string), typeof(TextViewer),
            new PropertyMetadata("", OnRoutineContentChanged));

    public static readonly DependencyProperty RoutineTypeProperty =
        DependencyProperty.Register(nameof(RoutineType), typeof(string), typeof(TextViewer),
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

    public TextViewer()
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

        Content = _editor;
        LoadSyntaxHighlighting();
    }

    private static void OnRoutineContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var viewer = (TextViewer)d;
        viewer._editor.Text = (string)e.NewValue;
    }

    private static void OnRoutineTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var viewer = (TextViewer)d;
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
