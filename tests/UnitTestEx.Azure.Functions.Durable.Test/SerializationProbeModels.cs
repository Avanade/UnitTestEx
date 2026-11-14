namespace UnitTestEx.Azure.Functions.Durable.Test;

internal enum SerializationProbeState
{
    Pending,
    Completed,
}

internal sealed record SerializationProbeChild(string? Label, int Sequence);

internal sealed record SerializationProbe(
    SerializationProbeState State,
    string? OptionalValue,
    SerializationProbeChild[] Children);
