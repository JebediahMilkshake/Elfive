using Elfive.Core.L5X.Base;
using Elfive.Core.SFC;
using Elfive.Core.TAG;

namespace L5X.V33;

public partial class RsLogix5000ContentType : IL5XContent
{
    IController? IL5XContent.Controller => Controller;
}

public partial class ControllerType : IController
{
    IEnumerable<IProgram>   IController.Programs   => Programs?.Program     ?? [];
    IEnumerable<IModule>    IController.Modules    => Modules?.Module       ?? [];
    IEnumerable<ITag>       IController.Tags       => Tags?.Tag             ?? [];
    IEnumerable<ITask>      IController.Tasks      => Tasks?.Task           ?? [];
    IEnumerable<IDataType>  IController.DataTypes  => DataTypes?.DataType   ?? [];
}

public partial class DataTypeType : IDataType
{
    string  IDataType.Name        => Name;
    string? IDataType.Description => Description is { } d ? string.Concat(d.Text ?? []) : null;
    IEnumerable<ITagMember> IDataType.Members => Members.Select(m =>
    {
        var isArray = m.DimensionSpecified && m.Dimension > 0;
        var children = isArray
            ? (IEnumerable<ITagMember>)Enumerable.Range(0, m.Dimension)
                .Select(i => (ITagMember)new TagMember { Name = $"{m.Name}[{i}]", DataType = m.DataType })
            : [];
        return (ITagMember)new TagMember
        {
            Name = m.Name,
            DataType = isArray ? $"{m.DataType}[{m.Dimension}]" : m.DataType,
            Children = children,
        };
    });
}

public partial class ProgramType : IProgram
{
    IEnumerable<IRoutine> IProgram.Routines => WithParent(Routines?.Routine ?? []);
    IEnumerable<ITag> IProgram.Tags => Tags?.Tag ?? [];

    private IEnumerable<RoutineType> WithParent(IEnumerable<RoutineType> routines)
    {
        foreach (var r in routines) { r._program = this; yield return r; }
    }
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

    IEnumerable<ITagMember> ITag.Children
    {
        get
        {
            var decorated = Data.FirstOrDefault(d => d.Format == "Decorated");
            if (decorated is null) return [];

            if (decorated.Structure.Count > 0)
            {
                var comments = BuildCommentMap();
                return decorated.Structure.SelectMany(s => BuildMembers(s, comments, ""));
            }

            var bitCount = DataType switch { "SINT" => 8, "INT" => 16, "DINT" => 32, "LINT" => 64, _ => 0 };
            if (bitCount > 0 && long.TryParse(decorated.DataValue.FirstOrDefault()?.Value, out var intVal))
            {
                var comments = BuildCommentMap();
                return Enumerable.Range(0, bitCount).Select(i =>
                {
                    comments.TryGetValue(i.ToString(), out var desc);
                    return (ITagMember)new TagMember
                        { Name = $"{Name}.{i}", DataType = "BOOL", Value = ((intVal >> i) & 1L).ToString(), Description = desc };
                });
            }

            return [];
        }
    }

    private IReadOnlyDictionary<string, string> BuildCommentMap() =>
        Comments.SelectMany(cc => cc.Comment)
            .Where(c => c.Operand is not null)
            .ToDictionary(c => c.Operand!, c => string.Concat(c.Text ?? []), StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<ITagMember> BuildMembers(DataStructure s, IReadOnlyDictionary<string, string> comments, string prefix) =>
        s.DataValueMember.Select(m =>
        {
            var bitCount = m.DataType switch
            {
                "SINT" => 8, "INT" => 16, "DINT" => 32, "LINT" => 64, _ => 0
            };
            var bitChildren = bitCount > 0 && long.TryParse(m.Value, out var intVal)
                ? (IEnumerable<ITagMember>)Enumerable.Range(0, bitCount)
                    .Select(i =>
                    {
                        comments.TryGetValue($"{prefix}{m.Name}.{i}", out var desc);
                        return (ITagMember)new TagMember
                            { Name = $"{m.Name}.{i}", DataType = "BOOL", Value = ((intVal >> i) & 1L).ToString(), Description = desc };
                    })
                : [];
            return (ITagMember)new TagMember
                { Name = m.Name, DataType = m.DataType, Value = m.Value, Children = bitChildren };
        })
        .Concat(s.StructureMember.Select(sm => new TagMember
            { Name = sm.Name, DataType = sm.DataType, Children = BuildMembers(sm, comments, $"{prefix}{sm.Name}.").ToList() }))
        .Concat(s.ArrayMember.Select(am => new TagMember
            { Name = am.Name, DataType = $"{am.DataType}[{am.Dimensions}]", Children = BuildArrayChildren(am, comments, prefix).ToList() }));

    private static IEnumerable<ITagMember> BuildArrayChildren(DataArray a, IReadOnlyDictionary<string, string> comments, string prefix)
    {
        if (a.Element.Count > 0)
            return a.Element.Select(e => (ITagMember)new TagMember
            {
                Name = $"{a.Name}{e.Index}",
                DataType = a.DataType,
                Value = e.Value,
                Children = e.Structure.SelectMany(s => BuildMembers(s, comments, $"{prefix}{a.Name}{e.Index}.")).ToList()
            });

        if (int.TryParse(a.Dimensions, out var count) && count > 0)
            return Enumerable.Range(0, count).Select(i => (ITagMember)new TagMember
            {
                Name = $"{a.Name}[{i}]",
                DataType = a.DataType,
            });

        return [];
    }
}

public partial class RoutineType : IRoutine
{
    internal IProgram? _program;
    IProgram? IRoutine.Program => _program;

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
        RoutineTypeEnum.Rll => RllContent.FirstOrDefault(),
        RoutineTypeEnum.St  => WithRoutine(StContent.FirstOrDefault()),
        RoutineTypeEnum.Fbd => FbdContent.FirstOrDefault(),
        RoutineTypeEnum.Sfc => WithSfcRoutine(SfcContent.FirstOrDefault()),
        _                   => null
    };

    private StContentType? WithRoutine(StContentType? content)
    {
        if (content != null)
            foreach (var line in content.Line)
                line._routine = this;
        return content;
    }

    private SfcContentType? WithSfcRoutine(SfcContentType? content)
    {
        if (content != null)
        {
            foreach (var step in content.Step)
                step._routine = this;
            foreach (var trans in content.Transition)
                trans._routine = this;
        }
        return content;
    }
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
    internal IRoutine? _routine;
    string? IStLine.Text => Text?.FirstOrDefault();
    IRoutine? IXRefElement.Routine => _routine;
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
    IEnumerable<ISfcStep>         ISfcContent.Steps         => Step;
    IEnumerable<ISfcTransition>   ISfcContent.Transitions   => Transition;
    IEnumerable<ISfcDirectedLink> ISfcContent.DirectedLinks => DirectedLink;
    IEnumerable<ISfcBranch>       ISfcContent.Branches      => Branch;
    IEnumerable<ISfcStop>         ISfcContent.Stops         => Stop;
}

public partial class SfcdLinkType : ISfcDirectedLink
{
    ulong ISfcDirectedLink.FromId => FromId;
    ulong ISfcDirectedLink.ToId   => ToId;
}

public partial class SfcBranchType : ISfcBranch
{
    ulong         ISfcBranch.Id         => Id;
    ulong         ISfcBranch.Y          => Y;
    SfcBranchKind ISfcBranch.BranchKind => BranchType == SfcBranchTypeEnum.Simultaneous
                                            ? SfcBranchKind.Simultaneous : SfcBranchKind.Selection;
    SfcBranchFlow ISfcBranch.Flow       => BranchFlow == SfcBranchFlowEnum.Diverge
                                            ? SfcBranchFlow.Diverge : SfcBranchFlow.Converge;
    IEnumerable<ulong> ISfcBranch.LegIds => Leg.Select(l => l.Id);
}

public partial class SfcStopType : ISfcStop
{
    ulong   ISfcStop.Id      => Id;
    ulong   ISfcStop.X       => X;
    ulong   ISfcStop.Y       => Y;
    string? ISfcStop.Operand => Operand;
}

public partial class SfcStepType : ISfcStep
{
    internal IRoutine? _routine;
    ulong  ISfcStep.Id        => Id;
    ulong  ISfcStep.X         => X;
    ulong  ISfcStep.Y         => Y;
    string? ISfcStep.Operand  => Operand;
    IEnumerable<ISfcAction>? ISfcStep.Actions => Action;
    IRoutine? IXRefElement.Routine => _routine;
}

public partial class SfcActionType : ISfcAction
{
    ulong ISfcAction.Id => Id;
    string? ISfcAction.Operand => Operand;
    IEnumerable<IStLine>? ISfcAction.Body => Body.StContent.Line;
    SfcQualifier? ISfcAction.Qualifier => Qualifier switch
    {
        SfcActionQualifierEnum.QualifierTypeNull or SfcActionQualifierEnum.QualifierTypeLast => null,
        SfcActionQualifierEnum.NonStored or SfcActionQualifierEnum.N => SfcQualifier.NonStored,
        SfcActionQualifierEnum.Reset or SfcActionQualifierEnum.R => SfcQualifier.Reset,
        SfcActionQualifierEnum.Stored or SfcActionQualifierEnum.S => SfcQualifier.Stored,
        SfcActionQualifierEnum.TimeLimited or SfcActionQualifierEnum.L => SfcQualifier.TimeLimited,
        SfcActionQualifierEnum.TimeDelayed or SfcActionQualifierEnum.D => SfcQualifier.TimeDelayed,
        SfcActionQualifierEnum.Pulse or SfcActionQualifierEnum.P => SfcQualifier.Pulse,
        SfcActionQualifierEnum.PulseRisingEdge or SfcActionQualifierEnum.P1 => SfcQualifier.PulseRising,
        SfcActionQualifierEnum.PulseFallingEdge or SfcActionQualifierEnum.P0 => SfcQualifier.PulseFalling,
        SfcActionQualifierEnum.StoredTimeLimited or SfcActionQualifierEnum.Sl => SfcQualifier.StoredLimited,
        SfcActionQualifierEnum.StoredTimeDelayed or SfcActionQualifierEnum.Sd => SfcQualifier.StoredDelayed,
        SfcActionQualifierEnum.TimeDelayedStored or SfcActionQualifierEnum.Ds => SfcQualifier.DelayedStored,
        _ => throw new ArgumentOutOfRangeException()
    };
}

public partial class SfcTransType : ISfcTransition
{
    internal IRoutine? _routine;
    ulong  ISfcTransition.Id        => Id;
    ulong  ISfcTransition.X         => X;
    ulong  ISfcTransition.Y         => Y;
    string? ISfcTransition.Operand  => Operand;
    IEnumerable<IStLine>? ISfcTransition.Condition => Condition?.StContent?.Line;
    IRoutine? IXRefElement.Routine => _routine;
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
    string? ITask.Trigger => EventInfo.EventTrigger.ToString();
    ushort ITask.Priority => Priority;
    IEnumerable<string> ITask.Children    => ScheduledPrograms.Select(sp => sp.Name);
}
