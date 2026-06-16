using Elfive.Core.L5X.Base;
using Elfive.Core.TAG;

namespace Elfive.Core.FBD;

public class FbdElement : IXRefElement
{
    public string Type { get; set; } = "";
    public ulong Id { get; set;}
    public ulong X { get; set;}
    public ulong Y { get; set;}
    public string[] Operands { get; set; } = [];
    public Connection[] Connections { get; set; } = [];
    public FbdSheet? ParentSheet { get; set; }

    IRoutine? IXRefElement.Routine => ParentSheet?.Routine;
}

public class Connection
{
    public string? Name { get; init; }
    public FbdElement? Parent { get; set; }
    //public List<Wire> ConnectedWires { get; set; } = new();
    public bool IsInput { get; set; } = false;
}

public class Wire
{
    public Connection? From { get; set; }
    public Connection? To { get; set; }
}

public class FbdSheet
{
    public ulong Number { get; set; }
    public string Description { get; set; } = "";
    public FbdElement[] Elements { get; set;} = [];
    public Wire[] Wires { get; set;} = [];
    public IRoutine? Routine { get; set; }
}