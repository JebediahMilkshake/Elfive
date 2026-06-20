using System.Collections.ObjectModel;
using System.Windows;
using Elfive.Core.L5X.Base;

namespace Elfive.App;

public class TagViewModel
{
    public string Name { get; set; } = "";
    public string Value { get; set; } = "";
    public string DataType { get; set; } = "";
    public string Description { get; set; } = "";
    public string Usage { get; set; } = "";
    public int Depth { get; set; } = 0;
    public Thickness DepthMargin => new Thickness(Depth * 20, 0, 0, 0);
    public ObservableCollection<TagViewModel> Children { get; } = [];

    private IReadOnlyList<ITagMember>? _pendingMembers;
    private bool _childrenLoaded;

    internal void SetPendingChildren(IReadOnlyList<ITagMember> members)
    {
        _pendingMembers = members;
        Children.Add(new TagViewModel()); // sentinel — keeps HasItems=true so expand arrow shows
    }

    public void EnsureChildrenLoaded()
    {
        if (_childrenLoaded || _pendingMembers is null) return;
        _childrenLoaded = true;
        Children.Clear();
        foreach (var m in _pendingMembers)
        {
            var grandChildren = m.Children.ToList();
            var child = new TagViewModel
            {
                Name = m.Name ?? "",
                Value = grandChildren.Count > 0 ? "{...}" : (m.Value ?? ""),
                DataType = m.DataType ?? "",
                Description = m.Description ?? "",
                Depth = Depth + 1,
            };
            if (grandChildren.Count > 0)
                child.SetPendingChildren(grandChildren);
            Children.Add(child);
        }
    }
}
