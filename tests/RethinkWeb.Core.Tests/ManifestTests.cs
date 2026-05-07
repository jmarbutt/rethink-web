using RethinkWeb.Actions;
using RethinkWeb.Auth;
using RethinkWeb.Manifest;
using RethinkWeb.Metadata;
using RethinkWeb.Mutations;
using RethinkWeb.Queries;

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
        var queries = new QueryRegistry();
        queries.Register(typeof(ListThingsQuery));
        var mutations = new MutationRegistry();
        mutations.Register(typeof(UpdateThingMutation));

        var builder = new ManifestBuilder(
            entities, actions, queries, mutations,
            new AllowAllAuthContext(),
            new FakeClock(DateTimeOffset.Parse("2026-05-06T10:00:00Z")));

        var manifest = builder.Build();

        manifest.Entities.Should().ContainSingle(e => e.Slug == "things");
        var thing = manifest.Entities.Single();
        thing.Fields.Should().ContainSingle(f => f.Name == "Name" && f.Required && f.Sample == "Widget");
        thing.Actions.Should().ContainSingle(a => a.Name == "do-stuff");
        thing.Actions.Single().InputSchema.Properties.Should().ContainKey("note");
        thing.Mutations.Should().ContainSingle(m => m.Name == "update-thing");
        manifest.Queries.Should().ContainSingle(q => q.Name == "things.list");
        manifest.Queries.Single().Cache.Mode.Should().Be(nameof(QueryCacheMode.PerTenant));
    }

    [Fact]
    public void Manifest_filters_actions_by_user_permission()
    {
        var entities = new EntityRegistry();
        entities.Register(typeof(Thing));
        var actions = new ActionRegistry();
        actions.Register(typeof(SecretAction));
        var queries = new QueryRegistry();
        queries.Register(typeof(SecretQuery));

        var deny = new DenyAllAuthContext();
        var builder = new ManifestBuilder(entities, actions, queries, new MutationRegistry(), deny,
            new FakeClock(DateTimeOffset.Parse("2026-05-06T10:00:00Z")));

        var manifest = builder.Build();

        manifest.Entities.Single().Actions.Should().BeEmpty(
            "the user lacks the permission required for the only registered action");
        manifest.Queries.Should().BeEmpty(
            "the user lacks the permission required for the only registered query");
    }

    [Action(name: "secret", displayName: "Secret", Permission = "admin.everything")]
    public sealed class SecretAction : IAction<Thing, DoStuffInput, DoStuffResult>
    {
        public Task<DoStuffResult> ExecuteAsync(Thing e, DoStuffInput i, IActionContext c, CancellationToken ct)
            => Task.FromResult(new DoStuffResult(true));
    }

    public sealed record ListThingsInput;
    public sealed record ListThingsResult(IReadOnlyList<string> Names);

    [Query(
        name: "things.list",
        displayName: "List Things",
        Cache = QueryCacheMode.PerTenant,
        CacheSeconds = 60,
        DependsOn = ["things"])]
    public sealed class ListThingsQuery : IQuery<ListThingsInput, ListThingsResult>
    {
        public Task<ListThingsResult> ExecuteAsync(ListThingsInput i, IQueryContext c, CancellationToken ct)
            => Task.FromResult(new ListThingsResult(["Widget"]));
    }

    [Query(name: "things.secret", displayName: "Secret Query", Permission = "admin.everything")]
    public sealed class SecretQuery : IQuery<ListThingsInput, ListThingsResult>
    {
        public Task<ListThingsResult> ExecuteAsync(ListThingsInput i, IQueryContext c, CancellationToken ct)
            => Task.FromResult(new ListThingsResult([]));
    }

    public sealed record UpdateThingInput(string Name);
    public sealed record UpdateThingResult(bool Ok);

    [Mutation(name: "update-thing", displayName: "Update Thing")]
    public sealed class UpdateThingMutation : IMutation<Thing, UpdateThingInput, UpdateThingResult>
    {
        public Task<UpdateThingResult> ExecuteAsync(
            Thing e,
            UpdateThingInput i,
            IMutationContext c,
            CancellationToken ct)
            => Task.FromResult(new UpdateThingResult(true));
    }

    private sealed class DenyAllAuthContext : IAuthContext
    {
        public string? UserId => "nobody";
        public bool HasPermission(string permission) => false;
    }
}
