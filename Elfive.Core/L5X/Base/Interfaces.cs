using Elfive.Core.SFC;
using Elfive.Core.TAG;

namespace Elfive.Core.L5X.Base;

public interface IL5XContent
{
    IController? Controller { get; }
    string? SchemaRevision { get; }
    string? SoftwareRevision { get; }
}

public interface IController
{
    string? Name { get; }
    string? ProcessorType { get; }
    IEnumerable<IProgram> Programs { get; }
    IEnumerable<IModule>  Modules  { get; }
    IEnumerable<ITag>     Tags     { get; }
    IEnumerable<ITask>    Tasks    { get; }
}

public interface IProgram
{
    string? Name { get; }
    IEnumerable<IRoutine> Routines { get; }
    IEnumerable<ITag> Tags { get; }
}

public interface IRoutine
{
    string? Name { get; }
    string Type { get; }
    IProgram? Program { get; }
    IRoutineContent? Content { get; }
}


public interface IRoutineContent { }

// Ladder Logic

public interface IRllContent : IRoutineContent
{
    IEnumerable<IRung> Rungs { get; }
}

public interface IRung
{
    ulong Number { get; }
    string? Comment { get; }
    string? Text { get; }
}

// Structured Text

public interface IStContent : IRoutineContent
{
    IEnumerable<IStLine> Lines { get; }
}

public interface IStLine : IXRefElement
{
    ulong Number { get; }
    string? Text { get; }
}

// Function Block Diagram

public interface IFbdContent : IRoutineContent
{
    IEnumerable<IFbdSheet> Sheets { get; }
}

public interface IFbdSheet
{
    ulong Number { get; }
    string? Description { get; }
    IEnumerable<IFbdElement> Blocks { get; }
    IEnumerable<IFbdElement> IRefs { get; }
    IEnumerable<IFbdElement> ORefs { get; }
    IEnumerable<IFbdElement> ICons { get; }
    IEnumerable<IFbdElement> OCons { get; }
    IEnumerable<IFbdWire> Wires { get; }
}

public interface IFbdElement
{
    string? Type { get; }
    ulong Id { get; }
    ulong X { get; }
    ulong Y { get; }
    string? Operand { get; }
    string? Pins { get; }
}

public interface IFbdWire
{
    ulong FromId { get; }
    string? FromParam { get; }
    ulong ToId { get; }
    string? ToParam { get; }
}

// Sequence Flow Chart

public interface ISfcContent : IRoutineContent
{
    IEnumerable<ISfcStep>         Steps         { get; }
    IEnumerable<ISfcTransition>   Transitions   { get; }
    IEnumerable<ISfcDirectedLink> DirectedLinks { get; }
    IEnumerable<ISfcBranch>       Branches      { get; }
    IEnumerable<ISfcStop>         Stops         { get; }
}

public interface ISfcDirectedLink
{
    ulong FromId { get; }
    ulong ToId   { get; }
}

public interface ISfcBranch
{
    ulong          Id         { get; }
    ulong          Y          { get; }
    SfcBranchKind  BranchKind { get; }
    SfcBranchFlow  Flow       { get; }
    IEnumerable<ulong> LegIds { get; }
}

public interface ISfcStop
{
    ulong   Id      { get; }
    ulong   X       { get; }
    ulong   Y       { get; }
    string? Operand { get; }
}

public interface ISfcStep : IXRefElement
{
    ulong Id { get; }
    ulong X { get; }
    ulong Y { get; }
    string? Operand { get; }
    IEnumerable<ISfcAction>? Actions { get; }
}

public interface ISfcAction
{
    ulong Id { get; }
    SfcQualifier? Qualifier { get; }
    string? Operand { get; }
    IEnumerable<IStLine>? Body { get; }
}

public interface ISfcTransition : IXRefElement
{
    ulong Id { get; }
    ulong X { get; }
    ulong Y { get; }
    string? Operand { get; }
    IEnumerable<IStLine>? Condition { get; }
}

public interface IPort
{
    ushort  Id       { get; }
    string? Type     { get; }
    string? Address  { get; }
    bool    Upstream { get; }
}

public interface IModule
{
    string?                   Name            { get; }
    ushort?                   Slot { get; }
    string?                   CatalogNumber   { get; }
    string?                   Status          { get; }
    string?                   ParentModule    { get; }
    ushort                    ParentModPortId { get; }
    string?                   IpAddress       { get; }
    IEnumerable<IPort>? Ports   { get; }
}

public interface ITagMember
{
    string? Name     { get; }
    string? DataType { get; }
    string? Value    { get; }
    IEnumerable<ITagMember> Children { get; }
}

public sealed class TagMember : ITagMember
{
    public string? Name     { get; init; }
    public string? DataType { get; init; }
    public string? Value    { get; init; }
    public IEnumerable<ITagMember> Children { get; init; } = [];
}

public interface ITag
{
    string? Name        { get; }
    string? Description { get; }
    string? DataType    { get; }
    string? Value       { get; }
    IEnumerable<ITagMember> Children { get; }
}

public enum TaskScanType { Continuous, Periodic, Event }

public interface ITask
{
    string?             Name        { get; }
    string?             Description { get; }
    TaskScanType        ScanType    { get; }
    float?              ScanRate    { get; }
    string? Trigger { get; }
    ushort Priority { get; }
    IEnumerable<string> Children    { get; }
   
}
