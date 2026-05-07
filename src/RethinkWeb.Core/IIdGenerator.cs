namespace RethinkWeb;

/// <summary>
/// Abstraction over Guid generation so framework code stays deterministic in tests.
/// `Guid.NewGuid()` is banned in framework code — inject this instead.
/// </summary>
public interface IIdGenerator
{
    Guid NewId();
}

public sealed class GuidIdGenerator : IIdGenerator
{
    public Guid NewId() => Guid.NewGuid();
}
