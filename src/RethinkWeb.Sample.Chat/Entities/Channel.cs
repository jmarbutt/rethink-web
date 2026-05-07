using System.ComponentModel.DataAnnotations;
using RethinkWeb.Metadata;

namespace RethinkWeb.Sample.Chat.Entities;

[Entity(slug: "channels", displayName: "Channels")]
public class Channel
{
    [Key]
    public Guid Id { get; set; }

    [TextBox("Name", GridVisible = true, GridOrder = 1, Required = true,
        Sample = "general")]
    public string Name { get; set; } = "";

    [TextBox("Description", Multiline = true,
        Sample = "Off-topic chatter")]
    public string? Description { get; set; }
}
