using Elfive.Core.L5X.Base;

namespace Elfive.Core.FBD;

public class FbdParser
{
    public FbdSheet[] ParseRoutineFbd(IRoutine routine)
    {
        
        return routine.Content is not IFbdContent fbd 
            ? [] 
            : fbd.Sheets.Select(ParseSheet).ToArray();
    }

    private FbdSheet ParseSheet(IFbdSheet sheetData)
    {
        var sheet = new FbdSheet()
        {
            Number = sheetData.Number,
            Description = sheetData.Description ?? "",
            Elements = sheetData.IRefs
                .Concat(sheetData.ORefs)
                .Concat(sheetData.ICons)
                .Concat(sheetData.OCons)
                .Concat(sheetData.Blocks)
                .Select(e => new FbdElement
                {
                    Type = e.Type ?? "",
                    Id = e.Id,
                    X = e.X,
                    Y = e.Y,
                    Operands = ParseOperand(e.Operand).ToArray(),
                    Connections = BuildConnections(e.Pins).ToArray(),
                }).ToArray()
        };
        //set parent references
        foreach (var element in sheet.Elements)
        {
            element.ParentSheet = sheet;
            foreach (var connection in element.Connections)
                connection.Parent = element;
        }
        
        // Build wires after all elements and connections are in place
        sheet.Wires = BuildWires(sheet, sheetData.Wires).ToArray();
        
        return sheet;
    }

    private IEnumerable<string> ParseOperand(string? operand)
    {
        return string.IsNullOrEmpty(operand) ? [] : operand.Split(' ');
    }

    private IEnumerable<Connection> BuildConnections(string? visiblePins)
    {
        //Case for IRef, ORef, ICon, OCon, where pins aren't specified
        return string.IsNullOrEmpty(visiblePins) 
            ? [new Connection {Name = "Connection"}] 
            : visiblePins.Split(' ').Select(p => new Connection { Name = p });
    }

    private IEnumerable<Wire> BuildWires(FbdSheet sheet, IEnumerable<IFbdWire> wires)
    {
        var sheetWires = new List<Wire>();
        foreach (var w in wires)
        {
            // For single-pin elements (IRef/ORef/ICon/OCon) the fabricated pin name
            // won't match the wire's param name, so fall back to the lone connection.
            var toElement = sheet.Elements?.FirstOrDefault(e => e.Id == w.ToId);
            var toConnection = toElement?.Connections.FirstOrDefault(c => c.Name == w.ToParam)
                ?? (toElement?.Connections.Length == 1 ? toElement.Connections[0] : null);

            var fromElement = sheet.Elements?.FirstOrDefault(e => e.Id == w.FromId);
            var fromConnection = fromElement?.Connections.FirstOrDefault(c => c.Name == w.FromParam)
                ?? (fromElement?.Connections.Length == 1 ? fromElement.Connections[0] : null);

            var wire = new Wire { To = toConnection, From = fromConnection };

            if (toConnection != null) toConnection.IsInput = true;
            if (fromConnection != null) fromConnection.IsInput = false;

            sheetWires.Add(wire);
        }
        return sheetWires;
    }
}