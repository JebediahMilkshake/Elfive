using System.Text.RegularExpressions;
using Elfive.Core.FBD;
using Elfive.Core.L5X.Base;
using Elfive.Core.RLL;
using Elfive.Core.SFC;

namespace Elfive.Core.TAG;

public class TagDatabase
{
    private static readonly HashSet<string> StKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "IF","THEN","ELSE","ELSIF","END_IF","FOR","TO","BY","DO","END_FOR",
        "WHILE","END_WHILE","REPEAT","UNTIL","END_REPEAT","CASE","OF","END_CASE",
        "RETURN","EXIT","NOT","AND","OR","XOR","MOD","TRUE","FALSE"
    };

    private static readonly Regex StIdentifier = new(@"\b[A-Za-z_]\w*", RegexOptions.Compiled);
    private static readonly Regex BlockComment  = new(@"\(\*.*?\*\)", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex LineComment   = new(@"//.*$", RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex StringLiteral = new(@"'[^']*'", RegexOptions.Compiled);

    private readonly Dictionary<ITag, List<XRefResult>> _xrefTable = new();
    private Dictionary<string, ITag> _tagIndex = new(StringComparer.OrdinalIgnoreCase);

    public IEnumerable<XRefResult> GetReferences(string tagName)
    {
        if (!_tagIndex.TryGetValue(tagName, out var tag)) return [];
        return _xrefTable.TryGetValue(tag, out var list) ? list : [];
    }

    public static TagDatabase? Build(IController? controller, RoutineDatabase rdb)
    {
        if (controller is null)
        {
            Console.WriteLine("Controller instance not found");
            return null;
        }

        var db = new TagDatabase
        {
            _tagIndex = controller.Tags.ToDictionary(t => t.Name!, StringComparer.OrdinalIgnoreCase)
        };

        foreach (var (routine, parsed) in rdb.All)
        {
            switch (parsed.Content)
            {
                case IRllContent:
                    db.ExtractRllReferences(parsed.Rungs!, db._tagIndex);
                    break;
                case IStContent:
                    db.ExtractStReferences(parsed.StLines!, db._tagIndex);
                    break;
                case IFbdContent:
                    db.ExtractFbdReferences(parsed.FbdSheets!, db._tagIndex);
                    break;
                case ISfcContent:
                    db.ExtractSfcReferences(parsed.SfcSheet!, db._tagIndex);
                    break;
                default:
                    Console.WriteLine($"Content type \"{parsed.Content?.GetType()}\" for {routine.Name} not supported.");
                    break;
            }
        }

        return db;
    }

    private void ExtractRllReferences(IEnumerable<Rung> rungs, Dictionary<string, ITag> tagIndex)
    {
        foreach (var rung in rungs)
            foreach (var instruction in rung.Instructions)
                foreach (var operand in instruction.Operands)
                {
                    if (!tagIndex.TryGetValue(GetBaseTag(operand), out var tag)) continue;
                    if (!_xrefTable.TryGetValue(tag, out var list))
                        _xrefTable[tag] = list = [];
                    list.Add(new XRefResult(tag, instruction, operand, instruction.Name));
                }
    }

    private void ExtractStReferences(IEnumerable<IStLine> lines, Dictionary<string, ITag> tagIndex)
    {
        foreach (var line in lines)
        {
            var text = line.Text ?? "";
            text = BlockComment.Replace(text, " ");
            text = LineComment.Replace(text, " ");
            text = StringLiteral.Replace(text, " ");

            foreach (Match m in StIdentifier.Matches(text))
            {
                if (StKeywords.Contains(m.Value)) continue;
                if (!tagIndex.TryGetValue(GetBaseTag(m.Value), out var tag)) continue;
                if (!_xrefTable.TryGetValue(tag, out var list))
                    _xrefTable[tag] = list = [];
                list.Add(new XRefResult(tag, line, m.Value, "ST"));
            }
        }
    }

    private void ExtractFbdReferences(IEnumerable<FbdSheet> sheets, Dictionary<string, ITag> tagIndex)
    {
        foreach (var sheet in sheets)
            foreach (var element in sheet.Elements)
                foreach (var operand in element.Operands)
                {
                    if (!tagIndex.TryGetValue(GetBaseTag(operand), out var tag)) continue;
                    if (!_xrefTable.TryGetValue(tag, out var list))
                        _xrefTable[tag] = list = [];
                    list.Add(new XRefResult(tag, element, operand, element.Type));
                }
    }

    private void ExtractSfcReferences(SfcSheet sheet, Dictionary<string, ITag> tagIndex)
    {
        foreach (var step in sheet.Steps)
            foreach (var action in step.Actions ?? [])
                ExtractStReferences(action.Body ?? [], tagIndex);

        foreach (var trans in sheet.Transitions)
            ExtractStReferences(trans.Condition ?? [], tagIndex);
    }

    private static string GetBaseTag(string operand)
    {
        var i = operand.IndexOfAny(['.', '[']);
        return i < 0 ? operand : operand[..i];
    }
}

public record XRefResult(
    ITag Tag,
    IXRefElement Element,
    string FullOperand,
    string InstructionName
);

public interface IXRefElement
{
    IRoutine? Routine { get; }
}
