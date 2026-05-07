using RethinkWeb.Actions;
using RethinkWeb.Auth;
using RethinkWeb.Manifest;
using RethinkWeb.Metadata;

namespace RethinkWeb.Core.Tests;

/// <summary>
/// Regression test for an earlier bug: ManifestBuilder used `!IsClass` as the
/// required-ness heuristic, so non-nullable strings (which ARE classes) were
/// always optional. NullabilityInfoContext gives the right answer for C#-8
/// nullable annotations.
/// </summary>
public class ManifestSchemaTests
{
    [Entity(slug: "things", displayName: "Things")]
    public class Thing
    {
        public Guid Id { get; set; }
        [TextBox("Name")] public string Name { get; set; } = "";
    }

    public sealed record InputWithMix(
        string NonNullableString,        // required
        string? NullableString,          // optional
        int NonNullableInt,              // required
        int? NullableInt,                // optional
        Guid NonNullableGuid             // required
    );

    public sealed record Output(bool Ok);

    [Action(name: "schema-test", displayName: "Schema test")]
    public sealed class SchemaTestAction : IAction<Thing, InputWithMix, Output>
    {
        public Task<Output> ExecuteAsync(Thing e, InputWithMix i, IActionContext c, CancellationToken ct)
            => Task.FromResult(new Output(true));
    }

    [Fact]
    public void Manifest_marks_non_nullable_strings_and_value_types_as_required()
    {
        var entities = new EntityRegistry();
        entities.Register(typeof(Thing));
        var actions = new ActionRegistry();
        actions.Register(typeof(SchemaTestAction));

        var builder = new ManifestBuilder(
            entities, actions, new Queries.QueryRegistry(), new Mutations.MutationRegistry(),
            new AllowAllAuthContext(),
            new FakeClock(DateTimeOffset.Parse("2026-05-06T10:00:00Z")));

        var schema = builder.Build().Entities.Single().Actions.Single().InputSchema;

        schema.Required.Should().Contain("nonNullableString",
            "non-nullable reference type is required (covers the IsClass-heuristic bug)");
        schema.Required.Should().Contain("nonNullableInt");
        schema.Required.Should().Contain("nonNullableGuid");

        schema.Required.Should().NotContain("nullableString");
        schema.Required.Should().NotContain("nullableInt");
    }
}
