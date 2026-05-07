namespace RethinkWeb;

/// <summary>
/// Abstraction over the system clock so framework code stays testable.
/// `DateTimeOffset.UtcNow` is banned in framework code — inject this instead.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
