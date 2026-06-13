using System.Collections.ObjectModel;

namespace Elfive.App;

public class TreeNode
{
    public string? Name { get; set; } = "";
    public string NodeType { get; set; } = ""; // "Program", "Routine", "Module", etc.
    public (string name, string value)[] Detail { get; set; }   // e.g. routine type "RLL", "ST"
    public TreeNode? Parent { get; set; }
    public ObservableCollection<TreeNode> Children { get; set; } = [];

    public TreeNode()
    {
    }
}

public class TreeNode<T> : TreeNode
{
    public T? Source { get; set; }         // reference back to your parsed object
}