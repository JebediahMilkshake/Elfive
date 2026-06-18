namespace Elfive.App;

public class XRefRowViewModel
{
    public string TagName { get; init; } = "";
    public string InstructionName { get; init; } = "";
    public string Location { get; init; } = "";
    public ulong? LocationIndex { get; init; }
    public string Routine { get; init; } = "";
    public string Program { get; init; } = "";
    public string Description { get; init; } = "";
    public TreeNode? RoutineNode { get; init; }
}
