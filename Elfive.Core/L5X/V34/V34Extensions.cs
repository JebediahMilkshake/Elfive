using L5X.Base;

namespace L5X.V34;

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
    string ITag.Description => Description.FirstOrDefault() is { } d ? string.Concat(d.Text ?? []) : string.Empty;
    string? ITag.Value => Data.FirstOrDefault(d => d.Format == "Decorated")
        ?.DataValue.FirstOrDefault()?.Value;
    IEnumerable<L5X.Base.ITagMember> ITag.Children =>
        Data.FirstOrDefault(d => d.Format == "Decorated")
            ?.Structure.SelectMany(BuildMembers) ?? [];

    private static IEnumerable<L5X.Base.ITagMember> BuildMembers(DataStructure s) =>
        s.DataValueMember.Select(m => (L5X.Base.ITagMember)new L5X.Base.TagMember
            { Name = m.Name, DataType = m.DataType, Value = m.Value })
        .Concat(s.StructureMember.Select(sm => new L5X.Base.TagMember
            { Name = sm.Name, DataType = sm.DataType, Children = BuildMembers(sm).ToList() }));
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
    ulong IFbdSheet.Number => Number;
    string? IFbdSheet.Description => Description.FirstOrDefault() is { } d ? string.Concat(d.Text ?? []) : string.Empty;
    IEnumerable<IFbdElement> IFbdSheet.Blocks => Block ?? [];
    IEnumerable<IFbdElement> IFbdSheet.IRefs => IRef ?? [];
    IEnumerable<IFbdElement> IFbdSheet.ORefs => ORef ?? [];
    IEnumerable<IFbdElement> IFbdSheet.ICons => ICon ?? [];
    IEnumerable<IFbdElement> IFbdSheet.OCons => OCon ?? [];
    IEnumerable<IFbdWire> IFbdSheet.Wires => Wire ?? [];
}

public partial class FbdElementType : IFbdElement
{
    string? IFbdElement.Type    => Type;
    ulong  IFbdElement.Id       => Id;
    ulong  IFbdElement.X        => X;
    ulong  IFbdElement.Y        => Y;
    string? IFbdElement.Operand => Operand;
    string? IFbdElement.Pins    => VisiblePins ?? "";
}

public partial class FbdIRefType : IFbdElement
{
    string? IFbdElement.Type    => "IREF";
    ulong  IFbdElement.Id       => Id;
    ulong  IFbdElement.X        => X;
    ulong  IFbdElement.Y        => Y;
    string? IFbdElement.Operand => Operand;
    string? IFbdElement.Pins    => "";
}

public partial class FbdORefType : IFbdElement
{
    string? IFbdElement.Type    => "OREF";
    ulong  IFbdElement.Id       => Id;
    ulong  IFbdElement.X        => X;
    ulong  IFbdElement.Y        => Y;
    string? IFbdElement.Operand => Operand;
    string? IFbdElement.Pins    => "";
}

public partial class FbdIConType : IFbdElement
{
    string? IFbdElement.Type    => "ICON";
    ulong  IFbdElement.Id       => Id;
    ulong  IFbdElement.X        => X;
    ulong  IFbdElement.Y        => Y;
    string? IFbdElement.Operand => null;
    string? IFbdElement.Pins    => "";
}

public partial class FbdOConType : IFbdElement
{
    string? IFbdElement.Type    => "ICON";
    ulong  IFbdElement.Id       => Id;
    ulong  IFbdElement.X        => X;
    ulong  IFbdElement.Y        => Y;
    string? IFbdElement.Operand => null;
    string? IFbdElement.Pins    => "";
}

public partial class FbdWireType : IFbdWire
{
    ulong IFbdWire.FromId => FromId;
    string IFbdWire.FromParam => FromParam;
    ulong IFbdWire.ToId => ToId;
    string IFbdWire.ToParam => ToParam;
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
