namespace Elfive.Core.TAG;

public class TagElement
{
    public string Name { get; set; } = "";
    public string Value { get; set; } = "";
    public string DataType { get; set; } = "";
    public string Description { get; set; } = "";
    public IEnumerable<TagElement> Children { get; set; } = [];
}


