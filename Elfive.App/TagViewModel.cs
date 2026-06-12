using CommunityToolkit.Mvvm.ComponentModel;

namespace Elfive.App;

public class TagViewModel
{
    public string Name { get; set; } = "";
    public string Value { get; set; } = "";
    public string DataType { get; set; } = "";
    public string Description { get; set; } = "";
}