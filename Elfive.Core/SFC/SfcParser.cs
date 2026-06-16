using Elfive.Core.L5X.Base;

namespace Elfive.Core.SFC;

public class SfcParser
{
    public SfcSheet ParseSfcRoutine(IRoutine routine)
    {
        var result = new SfcSheet();
        if (routine.Content is not ISfcContent sfc) return result;

        result.Steps = sfc.Steps.ToArray();
        result.Transitions = sfc.Transitions.ToArray();

        return result;
    }
}