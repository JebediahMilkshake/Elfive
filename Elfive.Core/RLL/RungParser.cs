namespace Elfive.Core.RLL;

public class RungParser
{
    private string _text = "";
    private int _pos;

    public Series Parse(string rungText)
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
            else if (ch == ',' || ch == ']')
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
        var argStart =  _pos;

        int parenDepth = 0;
        int bracketDepth = 0;

        while (_pos < _text.Length)
        {
            var ch = _text[_pos];
            if (ch == '(') parenDepth++;
            else if (ch == '[') bracketDepth++;
            else if (ch == ']')  bracketDepth--;
            else if (ch == ')')
            {
                if (parenDepth > 0) 
                    parenDepth--;
                else
                {
                    arguments.Add(_text[argStart.._pos].Trim());
                    _pos++;
                    break;
                }
            }

            _pos++;
        }
        
        return new Instruction { Name = name, Arguments = arguments.ToArray() };
    }
}