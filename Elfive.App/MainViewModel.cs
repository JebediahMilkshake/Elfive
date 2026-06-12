using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using L5X.Base;
namespace Elfive.App;

public partial class MainViewModel : ObservableObject
{
    
    public ObservableCollection<TreeNode> TreeItems { get; } = [];
    private readonly List<TagViewModel> _allTags = [];
    public ICollectionView? VisibleTags { get; private set; }

    [ObservableProperty]
    private string _tagFilter = "";
    [ObservableProperty] 
    private TreeNode? _selectedNode;
    [ObservableProperty]
    private ObservableCollection<TagViewModel> _selectedTags = [];
    [ObservableProperty]
    private string _selectedRoutineContent = "";
    [ObservableProperty] 
    private string _controllerName = "";
    [ObservableProperty] 
    private string _processorType = "";
    [ObservableProperty] 
    private int _tagCount;


    public void LoadController(IController? controller)
    {
        ControllerName = controller?.Name ?? "No Controller";
        ProcessorType = controller?.ProcessorType ?? "";
        
        
        
        TreeItems.Clear();
        _allTags.Clear();
        
        TreeItems.Add(BuildTagList(controller));
        TreeItems.Add(BuildPrograms(controller));
        if (controller != null) TreeItems.Add(BuildIoConfig(controller));


        TagCount = _allTags.Count;
    }

    public static IEnumerable<(string text, string? comment, ulong number)> GetRungText(TreeNode node)
    {
        if (node.Source is not IRoutine { Content: IRllContent rll })
            yield break;
        foreach (var rung in rll.Rungs)
            yield return (rung.Text, rung.Comment, rung.Number)!;
    }

    private TreeNode BuildTagList(IController? controller)
    {
        var node = new TreeNode
        {
            Name = "Controller Tags",
            NodeType = "Tags",
            Source = controller?.Tags,
        };
        
        if (controller is null) return node;
        
        foreach (var tag in controller.Tags)
        {
            _allTags.Add(new TagViewModel
            {
                Name = tag.Name ?? "",
                Value = tag.Value ?? "",
                DataType = tag.DataType ?? "",
                Description = tag.Description ?? "",
            });
        }
        
        SetupTagFiltering();

        return node;
    }

    private static TreeNode BuildPrograms(IController? controller)
    {
        var programsNode = new TreeNode {Name = "Programs", NodeType = "Folder"};
        
        if (controller is null) return programsNode;
        
        try
        {
            foreach (var program in controller.Programs)
            {
                var node = new TreeNode
                {
                    Name = program.Name,
                    NodeType = "Program",
                    Source = program,
                };

                foreach (var routine in program.Routines)
                {
                    node.Children.Add(new TreeNode
                    {
                        Name = routine.Name,
                        NodeType = "Routine",
                        Source = routine,
                        Detail = routine.Type
                    });
                }

                programsNode.Children.Add(node);
            }
        }
        catch (Exception e)
        {
            programsNode = new TreeNode {Name = "Programs", NodeType = "Folder"};
            Console.WriteLine($"Failed to Build Programs Node: {e}");
        }

        return programsNode;

        
    }
    
    private static TreeNode BuildIoConfig(IController controller)
    {
        var nodeMap = new Dictionary<string, TreeNode>();
        var root = new TreeNode { Name = "I/O Configuration", NodeType = "Folder" };
        nodeMap["root"] = root;

        //Build out modules into map
        foreach (var module in controller.Modules)
        {
            var modNameString = (module.Slot is {} ? $"[{module.Slot}] " : "") + $"{module.CatalogNumber} : ";
            modNameString += module.Name == "Local" ? controller.Name : module.Name;
            
            var node = new TreeNode
            {
                Name = modNameString,
                NodeType = "Module",
                Source = module,
            };
            if (module.Name != null)
                nodeMap[module.Name] = node;
            
            //Add this module's ports
            var modPorts = module.Ports?.ToList();
            if (modPorts is not { Count: > 0 }) continue;
            
            for (var index = 0; index < modPorts.Count; index++)
            {
                var port = modPorts[index];
                if (port.Upstream) continue;

                string portName = "[null]";
                if (port.Type == "Ethernet")
                {
                    if (module.Name == "Local") 
                        portName = modPorts.Count > 1 ? $"A{index}, Ethernet" : "Ethernet";
                }
                else
                {
                    portName = $"{port.Type} Backplane";
                }

                var portNode = new TreeNode
                {
                    Name = portName,
                    NodeType = "Folder",
                    Source = port
                };

                if (module.Name == "Local")
                    root.Children.Add(portNode);
                else
                {
                    node.Children.Add(portNode);
                }
                nodeMap[$"{module.Name}_Port{port.Id}"] = portNode;
                portNode.Children.Add(node);
                
                    
                
            }
        }

        //Map modules to their parents
        foreach (var module in controller.Modules)
        {
            if (module.Name is null or "Local") continue;
            var node = nodeMap[module.Name];
            
            if (nodeMap.TryGetValue($"{module.ParentModule!}_Port{module.ParentModPortId}", out var port))
                if (!port.Children.Contains(node)) port.Children.Add(node);
            else if (nodeMap.TryGetValue(module.ParentModule!, out var parent))
            {
                if (!parent.Children.Contains(node)) 
                    parent.Children.Add(node);
            }
        }

        return root;
    }

    private void SetupTagFiltering()
    {
        VisibleTags = CollectionViewSource.GetDefaultView(_allTags);
        VisibleTags.Filter = obj =>
        {
            if (string.IsNullOrEmpty(TagFilter)) return true;
            if (obj is not TagViewModel tag) return true;
            return tag.Name.Contains(TagFilter, StringComparison.OrdinalIgnoreCase)
                   || tag.DataType.Contains(TagFilter, StringComparison.OrdinalIgnoreCase)
                   || tag.Description.Contains(TagFilter, StringComparison.OrdinalIgnoreCase);
        };
        OnPropertyChanged(nameof(VisibleTags));
    }

    partial void OnTagFilterChanged(string value)
    {
        VisibleTags?.Refresh();
    }

    partial void OnSelectedNodeChanged(TreeNode? value)
    {
        if (value is null) return;
        switch (value.NodeType)
        {
            case "Tags":
                IEnumerable<ITag>? cTags = value.Source as IEnumerable<ITag>;
                LoadTags(cTags);
                break;
            case "Program":
                IProgram? prog = value.Source as IProgram;
                IEnumerable<ITag>? pTags = prog?.Tags;
                LoadTags(pTags);
                break;
            case "Routine":
                LoadRoutineContent(value.Source as IRoutine);
                break;
        }
    }

    private void LoadTags(IEnumerable<ITag>? tags)
    {
        _allTags.Clear();
        if (tags is null) return;

        foreach (var tag in tags)
        {
            _allTags.Add(new TagViewModel
            {
                Name = tag.Name ?? "",
                Value = tag.Value ?? "",
                DataType = tag.DataType ?? "",
                Description = tag.Description ?? "",
            });
        }
        VisibleTags?.Refresh();
    }

    private void LoadRoutineContent(IRoutine? routine)
    {
        if (routine is null) return;
        SelectedRoutineContent = routine.Content switch
        {
            IRllContent rll => BuildLadders(rll),
            IStContent st   => BuildSt(st),
            IFbdContent fbd => BuildFbd(fbd),
            ISfcContent sfc => BuildSfc(sfc),
            _               => $"[{routine.Type}] No content"
        };
    }

    private static string BuildLadders(IRllContent content)
    {
        var sb = new StringBuilder();
        foreach (var rung in content.Rungs)
            sb.AppendLine($"({rung.Number})  {rung.Text}");
        return sb.ToString();
    }

    private static string BuildSt(IStContent content)
    {
        var sb = new StringBuilder();
        foreach (var line in content.Lines)
            sb.AppendLine($"{line.Number:D4}  {line.Text}");
        return sb.ToString();
    }

    private static string BuildFbd(IFbdContent content)
    {
        var sb = new StringBuilder();
        foreach (var sheet in content.Sheets)
        {
            sb.AppendLine("[Sheet]");
            foreach (var block in sheet.Blocks)
                sb.AppendLine($"  [{block.Id}] {block.Type}  Operand={block.Operand}  ({block.X},{block.Y})");
        }
        return sb.ToString();
    }

    private static string BuildSfc(ISfcContent content)
    {
        var sb = new StringBuilder();
        sb.AppendLine("[Steps]");
        foreach (var step in content.Steps)
            sb.AppendLine($"  [{step.Id}] {step.Operand}  ({step.X},{step.Y})");
        sb.AppendLine("[Transitions]");
        foreach (var trans in content.Transitions)
            sb.AppendLine($"  [{trans.Id}] {trans.Operand}  ({trans.X},{trans.Y})");
        return sb.ToString();
    }
}