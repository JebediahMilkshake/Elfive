using L5X.Base;

namespace Elfive.Core.RLL;

public struct Rung
{
    public ulong Number { get; set; }
    public string? Comment { get; set; }
    public string RawText { get; set; }
    public Series Root { get; set; }
    public LayoutSize Size { get; set; }
}

public class RungParser
{
    private string _text = "";
    private int _pos;

    public Rung[] ParseRoutineRungs(IRoutine routine)
    {
        if (routine.Content is not IRllContent rll) return [];
        return rll.Rungs.Select(r =>
        {
            var root = Parse(r.Text ?? "");
           return new Rung
            {
                Number = r.Number,
                Comment = r.Comment,
                RawText = r.Text ?? "",
                Root = root,
                Size = LayoutCalculator.Measure(root)
            };
        }).ToArray();
    }
    
    private Series Parse(string rungText)
    {
        _text = rungText.TrimEnd(';', ' ', '\t', '\r', '\n');
        _pos = 0;
        return ParseSeries();
    }

    private Series ParseSeries()
    {
        var elements = new List<IRungElement>();

        while (_pos < _text.Length)
        {
            var ch = _text[_pos];
            if (ch == '[')
                elements.Add(ParseParallel());
            else if (ch is ' ')
                _pos++;
            else if (ch is ',' or ']')
                break;
            else
                elements.Add(ParseInstruction());
        }
        return new Series {Elements = elements};
    }

    private Parallel ParseParallel()
    {
        _pos++; //skip the opening '['

        var branches = new List<Series> { ParseSeries() };

        while (_pos < _text.Length && _text[_pos] == ',')
        {
            _pos++; // Skip Comma
            branches.Add(ParseSeries());
        }

        if (_pos < _text.Length && _text[_pos] == ']')
            _pos++;
        
        return new Parallel{ Branches = branches };
    }

    private Instruction ParseInstruction()
    {
        var nameStart = _pos;
        while (_pos < _text.Length && _text[_pos] != '(')
            _pos++;
        var name = _text[nameStart.._pos];
        _pos++; //skip the (

        var arguments = new List<string>();
        var argStart = _pos;

        int parenDepth = 0;
        int bracketDepth = 0;

        while (_pos < _text.Length)
        {
            var ch = _text[_pos];
            if (ch == '(') parenDepth++;
            else if (ch == '[') bracketDepth++;
            else if (ch == ']') bracketDepth--;
            else if (ch == ',' && parenDepth == 0 && bracketDepth == 0)
            {
                arguments.Add(_text[argStart.._pos].Trim());
                argStart = _pos + 1;
            }
            else if (ch == ')')
            {
                if (parenDepth > 0)
                    parenDepth--;
                else
                {
                    var last = _text[argStart.._pos].Trim();
                    if (last.Length > 0) arguments.Add(last);
                    _pos++;
                    break;
                }
            }

            _pos++;
        }
        
        return new Instruction { Name = name, Arguments = arguments.ToArray() };
    }
}