using System.Windows;
using System.Windows.Controls;
using Elfive.Core.L5X.Base;

namespace Elfive.App;

public class NodeTemplateSelector : DataTemplateSelector
{
    public DataTemplate? EmptyTemplate { get; set; }
    public DataTemplate? TagsTemplate { get; set; }
    public DataTemplate? AoiParametersTemplate { get; set; }
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
            "Tags" or "Program" or "DataType" => TagsTemplate,
            "AoiParameters" => AoiParametersTemplate,
            "Routine" => item is TreeNode<IRoutine> routine ? GetRoutineTemplate(routine) : null,
            "FbdSheet" => FdbTemplate,
            "Module" or "Task" => ModuleTemplate,
            _ => EmptyTemplate
        };
    }

    private DataTemplate? GetRoutineTemplate(TreeNode<IRoutine> node) =>
        node.Source!.Content switch
        {
            IStContent  => StTemplate,
            IRllContent => RllTemplate,
            IFbdContent => FdbTemplate,
            ISfcContent => SfcTemplate,
            _           => EmptyTemplate
        };
}