using RethinkWeb.Actions;
using RethinkWeb.Metadata;

namespace RethinkWeb.Core.Tests;

public class RegistryTests
{
    [Entity(slug: "widgets", displayName: "Widgets")]
    public class Widget
    {
        public Guid Id { get; set; }

        [TextBox("Name", GridVisible = true, GridOrder = 1, Required = true)]
        public string Name { get; set; } = string.Empty;

        [NumberBox("Quantity", GridVisible = true, GridOrder = 2)]
        public int Quantity { get; set; }
    }

    public sealed record RenameInput(string NewName);
    public sealed record RenameResult(string OldName, string NewName);

    [Action(name: "rename", displayName: "Rename")]
    public sealed class RenameAction : IAction<Widget, RenameInput, RenameResult>
    {
        public Task<RenameResult> ExecuteAsync(Widget entity, RenameInput input, IActionContext context, CancellationToken ct)
            => Task.FromResult(new RenameResult(entity.Name, input.NewName));
    }

    [Fact]
    public void EntityRegistry_builds_metadata_from_attributes()
    {
        var reg = new EntityRegistry();
        reg.Register(typeof(Widget));

        var meta = reg.GetBySlug("widgets");

        meta.Should().NotBeNull();
        meta!.Slug.Should().Be("widgets");
        meta.DisplayName.Should().Be("Widgets");
        meta.Fields.Should().HaveCount(2);
        meta.GridFields.Select(f => f.Name).Should().Equal("Name", "Quantity");
    }

    [Fact]
    public void ActionRegistry_finds_action_by_entity_and_name()
    {
        var reg = new ActionRegistry();
        reg.Register(typeof(RenameAction));

        var descriptor = reg.Find(typeof(Widget), "rename");

        descriptor.Should().NotBeNull();
        descriptor!.EntityType.Should().Be<Widget>();
        descriptor.InputType.Should().Be<RenameInput>();
        descriptor.OutputType.Should().Be<RenameResult>();
        descriptor.ImplementationType.Should().Be<RenameAction>();
    }

    [Fact]
    public void ActionRegistry_throws_when_implementation_lacks_attribute()
    {
        var reg = new ActionRegistry();
        var act = () => reg.Register(typeof(NoAttrAction));
        act.Should().Throw<InvalidOperationException>().WithMessage("*missing [Action*");
    }

    public sealed class NoAttrAction : IAction<Widget, RenameInput, RenameResult>
    {
        public Task<RenameResult> ExecuteAsync(Widget e, RenameInput i, IActionContext c, CancellationToken ct)
            => throw new NotImplementedException();
    }
}
