using System.Collections.ObjectModel;
using System.Windows;

namespace Elfive.App;

public class TagViewModel
{
    public string Name { get; set; } = "";
    public string Value { get; set; } = "";
    public string DataType { get; set; } = "";
    public string Description { get; set; } = "";
    public int Depth { get; set; } = 0;
    public Thickness DepthMargin => new Thickness(Depth * 20, 0, 0, 0);
    public ObservableCollection<TagViewModel> Children { get; } = [];
}