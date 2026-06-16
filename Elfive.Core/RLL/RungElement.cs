using Elfive.Core.L5X.Base;
using Elfive.Core.TAG;

namespace Elfive.Core.RLL;

public interface IRungElement
{
    IEnumerable<Instruction> Instructions { get; }
}

public class Instruction : IRungElement, IXRefElement
{
    public string Name { get; set; } = "";
    public string[] Operands { get; set; } = [];
    public IEnumerable<Instruction> Instructions => [this];

    public IRoutine? Routine { get; set; }
}

public class Series : IRungElement
{
    public List<IRungElement> Elements { get; set; } = [];

    public IEnumerable<Instruction> Instructions => Elements.SelectMany(x => x.Instructions);
}

public class Parallel : IRungElement
{
    public List<Series> Branches { get; set; } = [];
    public IEnumerable<Instruction> Instructions => Branches.SelectMany(x => x.Instructions);
}