namespace L5X.Base;

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
    IEnumerable<IModule> Modules { get; }
    IEnumerable<ITag> Tags { get; }
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
    string? Text { get; }
}

// Structured Text

public interface IStContent : IRoutineContent
{
    IEnumerable<IStLine> Lines { get; }
}

public interface IStLine
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
    IEnumerable<IFbdBlock> Blocks { get; }
}

public interface IFbdBlock
{
    string? Type { get; }
    ulong Id { get; }
    ulong X { get; }
    ulong Y { get; }
    string? Operand { get; }
}

// Sequence Flow Chart

public interface ISfcContent : IRoutineContent
{
    IEnumerable<ISfcStep> Steps { get; }
    IEnumerable<ISfcTransition> Transitions { get; }
}

public interface ISfcStep
{
    ulong Id { get; }
    ulong X { get; }
    ulong Y { get; }
    string? Operand { get; }
}

public interface ISfcTransition
{
    ulong Id { get; }
    ulong X { get; }
    ulong Y { get; }
    string? Operand { get; }
}

public interface IModule
{
    string? Name { get; }
    string? CatalogNumber { get; }
    string? Status { get; }
    string? ParentModule { get; }
}

public interface ITag
{
    string? Name { get; }
    string? Description { get; }
    string? DataType { get; }
    string? Value { get; }
}
