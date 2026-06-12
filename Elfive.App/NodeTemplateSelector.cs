using System.Windows;
using System.Windows.Controls;
using L5X.Base;

namespace Elfive.App;

public class NodeTemplateSelector : DataTemplateSelector
{
    public DataTemplate? EmptyTemplate { get; set; }
    public DataTemplate? TagsTemplate { get; set; }
    public DataTemplate? StTemplate { get; set; }
    public DataTemplate? RllTemplate { get; set; }
    public DataTemplate? FdbTemplate { get; set; }
    public DataTemplate? SfcTemplate { get; set; }
    public DataTemplate? ModuleTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object? item, DependencyObject container)
    {
        if (item is not TreeNode node) return EmptyTemplate;

        return node.NodeType switch
        {
            "Tags" or "Program" => TagsTemplate,
            "Routine" => (item is TreeNode<IRoutine> routine) ? GetRoutineTemplate(routine) : null,
            "FbdSheet" => FdbTemplate,
            "Module" => ModuleTemplate,
            _ => EmptyTemplate
        };
    }

    private DataTemplate? GetRoutineTemplate(TreeNode<IRoutine> node)
    {
        var routine = node.Source;
        return routine!.Content switch
        {
            IStContent => StTemplate,
            IRllContent => RllTemplate,
            IFbdContent => FdbTemplate,
            ISfcContent => SfcTemplate,
            _ => throw new NotSupportedException()
        };
    }
}