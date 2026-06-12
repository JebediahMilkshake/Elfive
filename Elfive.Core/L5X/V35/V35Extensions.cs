using L5X.Base;

namespace L5X.V35;

public partial class RsLogix5000ContentType : IL5XContent
{
    IController? IL5XContent.Controller => Controller;
}

public partial class ControllerType : IController
{
    IEnumerable<IProgram> IController.Programs => Programs?.Program ?? [];
    IEnumerable<IModule>  IController.Modules  => Modules?.Module  ?? [];
    IEnumerable<ITag>     IController.Tags      => Tags?.Tag        ?? [];
    IEnumerable<ITask>    IController.Tasks     => Tasks?.Task      ?? [];
}

public partial class ProgramType : IProgram
{
    IEnumerable<IRoutine> IProgram.Routines => Routines?.Routine ?? [];
    IEnumerable<ITag> IProgram.Tags => Tags?.Tag ?? [];
}

public partial class ModuleType : IModule
{
    string? IModule.ParentModule    => ParentModule;
    ushort  IModule.ParentModPortId => ParentModPortId;
    string? IModule.IpAddress       => Ports.FirstOrDefault(p => p.Type == "Ethernet")?.Address;
    IEnumerable<IPort> IModule.Ports => Ports;
    //Assume if the type is parseable, it's a number, and represents the backplane port (i.e. 5069)
    ushort? IModule.Slot => ushort.TryParse(Ports.FirstOrDefault(p => int.TryParse(p.Type, out int _))?.Address, out ushort slot)
        ? slot
        : null;
}

public partial class PortType : IPort
{
    ushort  IPort.Id       => Id;
    string? IPort.Type     => Type;
    string? IPort.Address  => Address;
    bool    IPort.Upstream => Upstream == BoolEnum.True;
}

public partial class EthernetLinkType : IPort
{
    ushort IPort.Id => Port;
    string? IPort.Type => "Ethernet";
    string? IPort.Address { get; }
    bool IPort.Upstream => false;
}

public partial class TagType : ITag
{
    string ITag.Description => Description.FirstOrDefault()?.Value.FirstOrDefault() ?? string.Empty;
    string? ITag.Value => Data.FirstOrDefault(d => d.Format == "Decorated")
        ?.DataValue.FirstOrDefault()?.Value;
}

public partial class RoutineType : IRoutine
{
    string IRoutine.Type => Type switch
    {
        RoutineTypeEnum.Rll => "RLL",
        RoutineTypeEnum.St  => "ST",
        RoutineTypeEnum.Fbd => "FBD",
        RoutineTypeEnum.Sfc => "SFC",
        _                   => Type.ToString()
    };
    IRoutineContent? IRoutine.Content => Type switch
    {
        RoutineTypeEnum.Rll => (IRoutineContent?)RllContent.FirstOrDefault(),
        RoutineTypeEnum.St  => StContent.FirstOrDefault(),
        RoutineTypeEnum.Fbd => FbdContent.FirstOrDefault(),
        RoutineTypeEnum.Sfc => SfcContent.FirstOrDefault(),
        _                   => null
    };
}

// Ladder Logic

public partial class RllContentType : IRllContent
{
    IEnumerable<IRung> IRllContent.Rungs => Rung;
}

public partial class RungType : IRung
{
    string? IRung.Comment => Comment?.Value?.FirstOrDefault();
}

// Structured Text

public partial class StContentType : IStContent
{
    IEnumerable<IStLine> IStContent.Lines => Line;
}

public partial class StLineType : IStLine
{
    string? IStLine.Text => Text?.FirstOrDefault();
}

// Function Block Diagram

public partial class FbdContentType : IFbdContent
{
    IEnumerable<IFbdSheet> IFbdContent.Sheets => Sheet;
}

public partial class SheetType : IFbdSheet
{
    IEnumerable<IFbdBlock> IFbdSheet.Blocks => Block;
}

public partial class FbdBlockType : IFbdBlock
{
    string? IFbdBlock.Type    => Type;
    ulong  IFbdBlock.Id       => Id;
    ulong  IFbdBlock.X        => X;
    ulong  IFbdBlock.Y        => Y;
    string? IFbdBlock.Operand => Operand;
}

// Sequence Flow Chart

public partial class SfcContentType : ISfcContent
{
    IEnumerable<ISfcStep>       ISfcContent.Steps       => Step;
    IEnumerable<ISfcTransition> ISfcContent.Transitions => Transition;
}

public partial class SfcStepType : ISfcStep
{
    ulong  ISfcStep.Id        => Id;
    ulong  ISfcStep.X         => X;
    ulong  ISfcStep.Y         => Y;
    string? ISfcStep.Operand  => Operand;
}

public partial class SfcTransType : ISfcTransition
{
    ulong  ISfcTransition.Id       => Id;
    ulong  ISfcTransition.X        => X;
    ulong  ISfcTransition.Y        => Y;
    string? ISfcTransition.Operand => Operand;
}

// Tasks

public partial class TaskType : ITask
{
    string?             ITask.Name        => Name;
    string?             ITask.Description => Description?.Value.FirstOrDefault();
    TaskScanType        ITask.ScanType    => Type switch
    {
        TaskTypeEnum.Continuous => TaskScanType.Continuous,
        TaskTypeEnum.Periodic   => TaskScanType.Periodic,
        TaskTypeEnum.Event      => TaskScanType.Event,
        _                       => TaskScanType.Continuous,
    };
    float?              ITask.ScanRate    => RateSpecified ? (float)Rate : null;
    IEnumerable<string> ITask.Children    => ScheduledPrograms.Select(sp => sp.Name);
}
