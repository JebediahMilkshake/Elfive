using Elfive.Core.L5X.Base;

namespace Elfive.Core.SFC;

public enum SfcQualifier {NonStored,Reset,Stored,TimeLimited,TimeDelayed,Pulse,StoredDelayed,StoredLimited,
    PulseRising,PulseFalling,DelayedStored
}
public enum SfcBranchKind { Selection, Simultaneous }
public enum SfcBranchFlow { Diverge, Converge }

public class SfcSheet
{
    public ISfcStep[] Steps { get; set; } = [];
    public ISfcTransition[] Transitions { get; set; } = []; 
}