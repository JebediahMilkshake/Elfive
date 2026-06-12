namespace Elfive.Core.FBD;

public class FbdElement
{
    public string Type { get; set; } = "";
    public ulong Id { get; set;}
    public ulong X { get; set;}
    public ulong Y { get; set;}
    public string[] Arguments { get; set; } = [];
    public Connection[] Connections { get; set; } = [];
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
}