using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RethinkWeb.Metadata;
using RethinkWeb.Render.Razor;

namespace RethinkWeb.Render.Razor.Tests;

public class RendererSnapshotTests
{
    [Entity(slug: "people", displayName: "People")]
    public class Person
    {
        public Guid Id { get; set; }
        [TextBox("First Name", GridVisible = true, GridOrder = 1, Required = true)]
        public string FirstName { get; set; } = string.Empty;
        [TextBox("Last Name", GridVisible = true, GridOrder = 2)]
        public string LastName { get; set; } = string.Empty;
        [CheckBox("Active")] public bool Active { get; set; }
    }

    [Fact]
    public Task GridView_renders_table_for_two_people()
    {
        var renderer = BuildRenderer();
        var meta = EntityMetadata.Build(typeof(Person));
        var people = new object[]
        {
            new Person { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), FirstName = "Ada", LastName = "Lovelace", Active = true },
            new Person { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), FirstName = "Alan", LastName = "Turing", Active = false },
        };

        return Verifier.Verify(renderer.RenderGridAsync(meta, people)).UseDirectory("Snapshots");
    }

    [Fact]
    public Task EditView_renders_form_with_field_per_attribute()
    {
        var renderer = BuildRenderer();
        var meta = EntityMetadata.Build(typeof(Person));
        var person = new Person
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            FirstName = "Ada",
            LastName = "Lovelace",
            Active = true,
        };

        return Verifier.Verify(renderer.RenderEditAsync(meta, person)).UseDirectory("Snapshots");
    }

    private static RazorEntityRenderer BuildRenderer()
    {
        var services = new ServiceCollection()
            .AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance)
            .AddScoped<HtmlRenderer>()
            .BuildServiceProvider();
        return new RazorEntityRenderer(services.GetRequiredService<HtmlRenderer>());
    }
}
