using System.Collections.ObjectModel;

namespace Elfive.App;

public class TreeNode
{
    public string? Name { get; set; } = "";
    public string NodeType { get; set; } = ""; // "Program", "Routine", "Module", etc.
    public string Detail { get; set; } = "";   // e.g. routine type "RLL", "ST"
    public object? Source { get; set; }         // reference back to your parsed object
    public ObservableCollection<TreeNode> Children { get; set; } = [];

    public TreeNode()
    {
    }
}