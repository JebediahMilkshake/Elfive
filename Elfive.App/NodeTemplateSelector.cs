using System.Windows;
using System.Windows.Controls;
using L5X.Base;

namespace Elfive.App;

public class NodeTemplateSelector : DataTemplateSelector
{
    public DataTemplate? EmptyTemplate { get; set; }
    public DataTemplate? TagsTemplate { get; set; }
    public DataTemplate? TextTemplate { get; set; }
    public DataTemplate? LadderTemplate { get; set; }
    public DataTemplate? BlockTemplate { get; set; }
    public DataTemplate? SequenceTemplate { get; set; }
    public DataTemplate? ModuleTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object? item, DependencyObject container)
    {
        if (item is not TreeNode node) return EmptyTemplate;

        return node.NodeType switch
        {
            "Tags" or "Program" => TagsTemplate,
            "Routine" => GetRoutineTemplate(node),
            "Module"  => ModuleTemplate,
            _         => EmptyTemplate
        };
    }

    private DataTemplate? GetRoutineTemplate(TreeNode node)
    {
        var routine = node.Source as IRoutine;
        return routine!.Content switch
        {
            IStContent => TextTemplate,
            IRllContent => LadderTemplate,
            IFbdContent => BlockTemplate,
            ISfcContent => SequenceTemplate,
            _ => throw new NotSupportedException()
        };
    }
}