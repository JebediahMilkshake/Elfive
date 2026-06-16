using Elfive.Core.FBD;
using Elfive.Core.L5X.Base;
using Elfive.Core.SFC;

namespace Elfive.Core.RLL;

public record ParsedRoutine(
    IRoutineContent? Content,
    Rung[]?      Rungs,
    FbdSheet[]?  FbdSheets,
    SfcSheet?    SfcSheet,
    IEnumerable<IStLine>? StLines
);

public class RoutineDatabase
{
    private readonly Dictionary<IRoutine, ParsedRoutine> _cache = new(ReferenceEqualityComparer.Instance);

    public ParsedRoutine? Get(IRoutine routine)
        => _cache.GetValueOrDefault(routine);

    public Rung[] GetRungs(IRoutine routine)
        => Get(routine)?.Rungs ?? [];

    public IEnumerable<(IRoutine Routine, ParsedRoutine Parsed)> All
        => _cache.Select(kvp => (kvp.Key, kvp.Value));

    public static RoutineDatabase Build(IL5XContent content)
    {
        var db = new RoutineDatabase();
        var rllParser = new RungParser();
        var fbdParser = new FbdParser();
        var sfcParser = new SfcParser();

        foreach (var program in content.Controller?.Programs ?? [])
            foreach (var routine in program.Routines)
                db._cache[routine] = routine.Content switch
                {
                    IRllContent => new ParsedRoutine(routine.Content, rllParser.ParseRoutineRungs(routine), null, null, null),
                    IFbdContent => new ParsedRoutine(routine.Content, null, fbdParser.ParseRoutineFbd(routine), null, null),
                    ISfcContent => new ParsedRoutine(routine.Content, null, null, sfcParser.ParseSfcRoutine(routine), null),
                    IStContent st => new ParsedRoutine(routine.Content, null,null,null, st.Lines ),
                    _           => new ParsedRoutine(routine.Content, null, null, null, null),
                };

        return db;
    }
}
