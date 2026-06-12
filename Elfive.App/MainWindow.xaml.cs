using System.IO;
using System.Windows;
using System.Windows.Controls;
using L5X;
using Microsoft.Win32;

namespace Elfive.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var args = Environment.GetCommandLineArgs();
        var path = args.Length > 1 ? args[1] : null;
        if (path is null) { Console.WriteLine("Path not Specified"); return; }
        if (!File.Exists(path)) { Console.WriteLine($"File not found at {path}"); return; }

        var content = new L5XReader().Read(path);
        if (content?.Controller is { } controller)
            _viewModel.LoadController(controller);
    }

    private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is TreeNode node) _viewModel.SelectedNode = node;
    }

    private void MenuOpen_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "L5X Files (*.L5X)|*.L5X|All Files (*.*)|*.*",
            Title = "Open L5X Project"
        };

        if (dialog.ShowDialog() != true) return;
        
        var path = dialog.FileName;
        if (!File.Exists(path)) { Console.WriteLine($"File not found at {path}"); return; }
        var content = new L5XReader().Read(path);
        Title = $"{Path.GetFileName(path)} - Elfive";
        if (content?.Controller is { } controller)
            _viewModel.LoadController(controller);
    }
    
}