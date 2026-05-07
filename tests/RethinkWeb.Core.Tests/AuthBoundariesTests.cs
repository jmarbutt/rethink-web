using RethinkWeb.Auth;
using RethinkWeb.Manifest;

namespace RethinkWeb.Core.Tests;

/// <summary>
/// Tests covering Codex Fix #4 — entity/field permissions are now consulted on read,
/// write, and per-field paths. The HTTP and form-binding enforcement live in
/// RethinkWeb.Http.MinimalApi, but the manifest filtering pattern is the canonical
/// permission test that the framework Core supports.
/// </summary>
public class AuthBoundariesTests
{
    private sealed class DenyAll : IAuthContext
    {
        public string? UserId => "no-one";
        public bool HasPermission(string permission) => false;
    }

    private sealed class GrantOnly(params string[] grants) : IAuthContext
    {
        public string? UserId => "test";
        public bool HasPermission(string permission) => grants.Contains(permission);
    }

    [Fact]
    public void Manifest_omits_entity_when_user_lacks_ReadPermission()
    {
        var entities = new Metadata.EntityRegistry();
        entities.Register(typeof(SecretEntity));
        var actions = new Actions.ActionRegistry();

        var manifest = new ManifestBuilder(
            entities,
            actions,
            new Queries.QueryRegistry(),
            new Mutations.MutationRegistry(),
            new DenyAll(),
            new FakeClock(DateTimeOffset.Parse("2026-05-06T10:00:00Z"))).Build();

        manifest.Entities.Should().BeEmpty("user lacks 'admin.read' so the secret entity is hidden");
    }

    [Fact]
    public void Manifest_includes_entity_when_user_has_ReadPermission()
    {
        var entities = new Metadata.EntityRegistry();
        entities.Register(typeof(SecretEntity));
        var actions = new Actions.ActionRegistry();

        var manifest = new ManifestBuilder(
            entities,
            actions,
            new Queries.QueryRegistry(),
            new Mutations.MutationRegistry(),
            new GrantOnly("admin.read"),
            new FakeClock(DateTimeOffset.Parse("2026-05-06T10:00:00Z"))).Build();

        manifest.Entities.Should().ContainSingle(e => e.Slug == "secrets");
    }

    [Metadata.Entity(slug: "secrets", displayName: "Secrets", ReadPermission = "admin.read", WritePermission = "admin.write")]
    public class SecretEntity
    {
        public Guid Id { get; set; }
        [Metadata.TextBox("Value")] public string Value { get; set; } = "";
    }
}
