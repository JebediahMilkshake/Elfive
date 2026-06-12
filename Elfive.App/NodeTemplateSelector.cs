using System.Windows;
using System.Windows.Controls;

namespace Elfive.App;

public class NodeTemplateSelector : DataTemplateSelector
{
    public DataTemplate? EmptyTemplate { get; set; }
    public DataTemplate? TagsTemplate { get; set; }
    public DataTemplate? RoutineTemplate { get; set; }
    public DataTemplate? ModuleTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object? item, DependencyObject container)
    {
        if (item is not TreeNode node) return EmptyTemplate;

        return node.NodeType switch
        {
            "Tags" or "Program" => TagsTemplate,
            "Routine" => RoutineTemplate,
            "Module"  => ModuleTemplate,
            _         => EmptyTemplate
        };
    }
}