using RethinkWeb;

namespace RethinkWeb.Core.Tests;

/// <summary>
/// The fakes any test wants. Lives here in the test project for the prototype;
/// in v1 these belong in a public RethinkWeb.Testing package.
/// </summary>
public sealed class FakeClock(DateTimeOffset start) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = start;
    public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);
}

public sealed class FakeIdGenerator(params Guid[] ids) : IIdGenerator
{
    private int _i;
    public Guid NewId() => _i < ids.Length ? ids[_i++] : Guid.Empty;
}
