namespace Elfive.Core.RLL;

public interface IRungElement;

public class Instruction : IRungElement
{
    public string Name { get; set; } = "";
    public string[] Arguments { get; set; } = [];
}

public class Series : IRungElement
{
    public List<IRungElement> Elements { get; set; } = [];
}

public class Parallel : IRungElement
{
    public List<Series> Branches { get; set; } = [];
}