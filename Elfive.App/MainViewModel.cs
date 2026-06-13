using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using L5X.Base;
using Newtonsoft.Json;

namespace Elfive.App;

public partial class MainViewModel : ObservableObject
{
    
    public ObservableCollection<TreeNode> TreeItems { get; } = [];
    private readonly List<TagViewModel> _allTags = [];
    private readonly Dictionary<string, string> _controllerTagValues = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, string> ControllerTagValues => _controllerTagValues;
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
    private string _selectedRoutineHeader = "";
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
        
        TreeItems.Add(BuildControllerRootNode(controller!));
        TreeItems.Add(BuildTasksNode(controller));
        TreeItems.Add(BuildMotionsGroupsNode(controller));
        TreeItems.Add(BuildAlarmManagerNode(controller));
        TreeItems.Add(BuildAssetsNode(controller));
        TreeItems.Add(BuildIoConfigNode(controller!));
        

        TagCount = _allTags.Count;
    }

    public static IEnumerable<(string text, string? comment, ulong number)> GetRungText(TreeNode<IRoutine> node)
    {
        if (node.Source is not { Content: IRllContent rll })
            yield break;
        foreach (var rung in rll.Rungs)
            yield return (rung.Text, rung.Comment, rung.Number)!;
    }
    
    private TreeNode BuildMotionsGroupsNode(IController? controller)
    {
        var motionNode = new TreeNode { Name = "Motion Groups", NodeType = "Folder" };
        
        //TODO add parsing and data for motion groups
        
        motionNode.Children.Add(new TreeNode { Name = "Ungrouped Axes", NodeType = "Folder" });

        return motionNode;
    }

    private TreeNode BuildAlarmManagerNode(IController? controller)
    {
        var alarmNode = new TreeNode { Name = "Alarm Manager", NodeType = "Folder" };
        //TODO build typed TreeNodes for "Alarm Views", similar tag views
        //TODO build typed TreeNods for "Alarm Definitions" similar to tag views
        return alarmNode;
    }

    private TreeNode BuildAssetsNode(IController? controller)
    {
        var assetNode = new TreeNode { Name = "Assets", NodeType = "Folder" };
        
        assetNode.Children.Add(new TreeNode { Name = "Add-On Instructions", NodeType = "Folder" });
        var dataTypesNode = new TreeNode { Name = "Data Types", NodeType = "Folder" };
        assetNode.Children.Add(dataTypesNode);
        
        assetNode.Children.Add(new TreeNode { Name = "Trends", NodeType = "Folder" });
        
        dataTypesNode.Children.Add(new TreeNode { Name = "User-Defined", NodeType = "Folder" });
        dataTypesNode.Children.Add(new TreeNode { Name = "Strings", NodeType = "Folder" });
        dataTypesNode.Children.Add(new TreeNode { Name = "Add-On-Defined", NodeType = "Folder" });
        dataTypesNode.Children.Add(new TreeNode { Name = "Predefined", NodeType = "Folder" });
        dataTypesNode.Children.Add(new TreeNode { Name = "Module-Defined", NodeType = "Folder" });

        return assetNode;
    }

    private TreeNode BuildControllerRootNode(IController? controller)
    {
        var controllerRoot = new TreeNode{Name = $"Controller {controller?.Name ?? "[unnamed]"}",  NodeType = "Folder"}; //TODO create "controller view node type
        controllerRoot.Children.Add(BuildTagList(controller));
        controllerRoot.Children.Add(new TreeNode{Name="Controller Fault Handler", NodeType = "Folder"});
        controllerRoot.Children.Add(new TreeNode{Name="Power-Up Handler",NodeType = "Folder"});
        return controllerRoot;
    }

    private TreeNode BuildTagList(IController? controller)
    {
        var node = new TreeNode<IEnumerable<ITag>>
        {
            Name = "Controller Tags",
            NodeType = "Tags",
            Source = controller?.Tags,
        };
        
        if (controller is null) return node;

        _controllerTagValues.Clear();
        foreach (var tag in controller.Tags)
        {
            var tagChildren = tag.Children.ToList();
            var vm = new TagViewModel
            {
                Name = tag.Name ?? "",
                Value = tagChildren.Count > 0 ? "{...}" : (tag.Value ?? ""),
                DataType = tag.DataType ?? "",
                Description = FlattenDescription(tag.Description),
            };
            PopulateChildren(vm, tagChildren);
            _allTags.Add(vm);
            if (tag.Name != null)
                _controllerTagValues[tag.Name] = tag.Value ?? "";
        }
        
        SetupTagFiltering();

        return node;
    }

    private static string FlattenDescription(string? desc) =>
        string.IsNullOrEmpty(desc)
            ? ""
            : string.Join(" ", desc.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries));

    private static void PopulateChildren(TagViewModel parent, IEnumerable<L5X.Base.ITagMember> members, int depth = 1)
    {
        foreach (var m in members)
        {
            var childMembers = m.Children.ToList();
            var child = new TagViewModel
            {
                Name = m.Name ?? "",
                Value = childMembers.Count > 0 ? "{...}" : (m.Value ?? ""),
                DataType = m.DataType ?? "",
                Depth = depth,
            };
            PopulateChildren(child, childMembers, depth + 1);
            parent.Children.Add(child);
        }
    }

    private static TreeNode BuildTasksNode(IController? controller)
    {
        var nodeMap = new Dictionary<string, TreeNode>();
        var tasks = new TreeNode {Name = "Tasks", NodeType = "Folder"};
        
        if (controller is null) return tasks;
        
        try
        {
            foreach (var task in controller.Tasks)
            {
                var taskNode = new TreeNode<ITask>()
                {
                    Name = task.Name,
                    NodeType = "Task",
                    Source = task,
                    Detail = BuildTaskDetail(task).ToArray()
                };
                tasks.Children.Add(taskNode);
                nodeMap[task.Name!] =  taskNode;
            }
            //Add "Unassigned" node
            var unassigned = new TreeNode<ITask>
            {
                Name = "Unassigned",
                NodeType = "Folder",
            };
            tasks.Children.Add(unassigned);
            
            foreach (var program in controller.Programs)
            {
                var programNode = new TreeNode<IProgram>
                {
                    Name = program.Name,
                    NodeType = "Program",
                    Detail = [],
                    Source = program,
                };

                foreach (var routine in program.Routines)
                {
                    var routineNode = new TreeNode<IRoutine>
                    {
                        Name = routine.Name,
                        NodeType = "Routine",
                        Source = routine,
                        Detail = [("",routine.Type)],
                        Parent = programNode
                    };

                    if (routine.Content is IFbdContent fbdContent)
                    {
                        foreach (var sheet in fbdContent.Sheets)
                        {
                            var label = string.IsNullOrEmpty(sheet.Description)
                                ? $"Sheet {sheet.Number}"
                                : $"Sheet {sheet.Number}: {sheet.Description}";
                            routineNode.Children.Add(new TreeNode<IFbdSheet>
                            {
                                Name = label,
                                NodeType = "FbdSheet",
                                Source = sheet,
                                Parent = routineNode
                            });
                        }
                    }

                    programNode.Children.Add(routineNode);
                }
                
                var parentTask = controller.Tasks.FirstOrDefault(t => t.Children.Contains(program.Name));
                if (parentTask?.Name != null && nodeMap.TryGetValue(parentTask.Name, out var taskNode))
                    taskNode.Children.Add(programNode);
                else
                    unassigned.Children.Add(programNode);
                
                
            }
        }
        catch (Exception e)
        {
            tasks = new TreeNode {Name = "Programs", NodeType = "Folder"};
            Console.WriteLine($"Failed to Build Programs Node: {e}");
        }

        return tasks;

        
    }
    
    private static TreeNode BuildIoConfigNode(IController controller)
    {
        var nodeMap = new Dictionary<string, TreeNode>();
        var root = new TreeNode { Name = "I/O Configuration", NodeType = "Folder" };
        nodeMap["root"] = root;

        //Build out modules into map
        foreach (var module in controller.Modules)
        {
            var modNameString = (module.Slot is {} ? $"[{module.Slot}] " : "") + $"{module.CatalogNumber} : ";
            modNameString += module.Name == "Local" ? controller.Name : module.Name;
            
            var node = new TreeNode<IModule>
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

                var portNode = new TreeNode<IPort>
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

    private static IEnumerable<(string name, string value)> BuildTaskDetail(ITask task)
    {
        List<(string name, string value)> results =
        [
            ("Type", task.ScanType.ToString()),
            ("Description", string.IsNullOrEmpty(task.Description) ? "No Description" : task.Description)
        ];

        if (task.ScanType == TaskScanType.Continuous) return results;

        results.Add(task.ScanType == TaskScanType.Periodic
            ? ("Period", $"{task.ScanRate:N0}")
            : ("Trigger", task.Trigger!));

        results.Add(("Priority", $"{task.Priority:N0}"));
        return results;
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
                if (value is TreeNode<IEnumerable<ITag>> tagsNode)
                    LoadTags(tagsNode.Source);
                break;
            case "Program":
                if (value is TreeNode<IProgram> progNode)
                    LoadTags(progNode.Source?.Tags);
                break;
            case "Routine":
                if (value is TreeNode<IRoutine> routineNode)
                {
                    LoadRoutineContent(routineNode.Source);
                    SelectedRoutineHeader = BuildRoutineHeader(routineNode);
                }
                break;
        }
    }

    private void LoadTags(IEnumerable<ITag>? tags)
    {
        _allTags.Clear();
        if (tags is null) return;

        foreach (var tag in tags)
        {
            var tagChildren = tag.Children.ToList();
            var vm = new TagViewModel
            {
                Name = tag.Name ?? "",
                Value = tagChildren.Count > 0 ? "{...}" : (tag.Value ?? ""),
                DataType = tag.DataType ?? "",
                Description = FlattenDescription(tag.Description),
            };
            PopulateChildren(vm, tagChildren);
            _allTags.Add(vm);
        }
        VisibleTags?.Refresh();
    }

    private static string BuildRoutineHeader(TreeNode node)
    {
        var programName = node.Parent?.Name ?? "";
        return string.IsNullOrEmpty(programName)
            ? node.Name ?? ""
            : $"{node.Name}   —   {programName}";
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