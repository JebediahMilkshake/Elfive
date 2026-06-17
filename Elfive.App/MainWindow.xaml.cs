using System.IO;
using System.Windows;
using Elfive.App.Views;
using Elfive.Core.L5X.Base;
using L5X;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Elfive.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        Title = "Elfive Logic Viewer";
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        
        var args = Environment.GetCommandLineArgs();
        var path = args.Length > 1 ? args[1] : null;
        
        LoadProject(path ?? "");
    }

    private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is TreeNode node) _viewModel.SelectedNode = node;
    }

    public void NavigateToRoutineNode(TreeNode? node)
    {
        if (node is null) return;
        _viewModel.SelectedNode = node;
        // ActiveTab = 0 is set inside OnSelectedNodeChanged via SelectedNode assignment
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
        LoadProject(path);
       
    }

    private void LoadProject(string path)
    {
        if (!File.Exists(path)) { Console.WriteLine($"File not found at {path}"); return; }
        var content = new L5XReader().Read(path);
        Title = $"{Path.GetFileName(path)} - Elfive Logic Viewer";

        //PrintContent(content?.Controller, true);

        if (content is null) return;
        if (content.Controller is { } controller)
            _viewModel.LoadController(controller);
        _viewModel.LoadRoutineDatabase(content);
    }

    public class InterfaceOnlyContractResolver(
        params Type[]
            interfaces) :
        DefaultContractResolver
    {
        private readonly IReadOnlyList<Type> _interfaces = interfaces;

        protected override IList<JsonProperty>
            CreateProperties(Type type, MemberSerialization
                memberSerialization)
        {
            var match = _interfaces.FirstOrDefault(i =>
                i.IsAssignableFrom(type));
            return base.CreateProperties(match ?? type,
                memberSerialization);
        }
    }

    private static void PrintContent(IController? controller, bool fullProject)
    {
        if (controller is null) return;
        var settings = fullProject
            ? new JsonSerializerSettings { Formatting = Formatting.Indented }
            : new JsonSerializerSettings
            {
                ContractResolver = new InterfaceOnlyContractResolver(
                    typeof(IL5XContent), typeof(IController), typeof(IProgram),
                    typeof(IRoutine), typeof(IRllContent), typeof(IRung),
                    typeof(IStContent), typeof(IStLine), typeof(IFbdContent),
                    typeof(IFbdSheet), typeof(IFbdElement), typeof(ISfcContent),
                    typeof(ISfcStep), typeof(ISfcTransition),
                    typeof(ITag), typeof(ITask), typeof(IModule), typeof(IPort)
                ),
                Formatting = Formatting.Indented
            };
        var json = JsonConvert.SerializeObject(controller, settings);
        File.WriteAllText(@"C:\users\dglan\desktop\controller.json", json);
    }

    
}