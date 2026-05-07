using RethinkWeb.Actions;
using RethinkWeb.Auth;
using RethinkWeb.Manifest;
using RethinkWeb.Metadata;

namespace RethinkWeb.Core.Tests;

public class ManifestTests
{
    [Entity(slug: "things", displayName: "Things")]
    public class Thing
    {
        public Guid Id { get; set; }
        [TextBox("Name", Sample = "Widget", Required = true)] public string Name { get; set; } = string.Empty;
    }

    public sealed record DoStuffInput(string Note);
    public sealed record DoStuffResult(bool Ok);

    [Action(name: "do-stuff", displayName: "Do Stuff", Description = "Does some stuff.")]
    public sealed class DoStuffAction : IAction<Thing, DoStuffInput, DoStuffResult>
    {
        public Task<DoStuffResult> ExecuteAsync(Thing e, DoStuffInput i, IActionContext c, CancellationToken ct)
            => Task.FromResult(new DoStuffResult(true));
    }

    [Fact]
    public void Manifest_includes_registered_entities_and_their_actions()
    {
        var entities = new EntityRegistry();
        entities.Register(typeof(Thing));
        var actions = new ActionRegistry();
        actions.Register(typeof(DoStuffAction));

        var builder = new ManifestBuilder(
            entities, actions,
            new AllowAllAuthContext(),
            new FakeClock(DateTimeOffset.Parse("2026-05-06T10:00:00Z")));

        var manifest = builder.Build();

        manifest.Entities.Should().ContainSingle(e => e.Slug == "things");
        var thing = manifest.Entities.Single();
        thing.Fields.Should().ContainSingle(f => f.Name == "Name" && f.Required && f.Sample == "Widget");
        thing.Actions.Should().ContainSingle(a => a.Name == "do-stuff");
        thing.Actions.Single().InputSchema.Properties.Should().ContainKey("note");
    }

    [Fact]
    public void Manifest_filters_actions_by_user_permission()
    {
        var entities = new EntityRegistry();
        entities.Register(typeof(Thing));
        var actions = new ActionRegistry();
        actions.Register(typeof(SecretAction));

        var deny = new DenyAllAuthContext();
        var builder = new ManifestBuilder(entities, actions, deny,
            new FakeClock(DateTimeOffset.Parse("2026-05-06T10:00:00Z")));

        var manifest = builder.Build();

        manifest.Entities.Single().Actions.Should().BeEmpty(
            "the user lacks the permission required for the only registered action");
    }

    [Action(name: "secret", displayName: "Secret", Permission = "admin.everything")]
    public sealed class SecretAction : IAction<Thing, DoStuffInput, DoStuffResult>
    {
        public Task<DoStuffResult> ExecuteAsync(Thing e, DoStuffInput i, IActionContext c, CancellationToken ct)
            => Task.FromResult(new DoStuffResult(true));
    }

    private sealed class DenyAllAuthContext : IAuthContext
    {
        public string? UserId => "nobody";
        public bool HasPermission(string permission) => false;
    }
}
