using System.ComponentModel.DataAnnotations;
using RethinkWeb.Metadata;

namespace RethinkWeb.Sample.Chat.Entities;

[Entity(slug: "messages", displayName: "Messages")]
public class Message
{
    [Key]
    public Guid Id { get; set; }

    public Guid ChannelId { get; set; }

    [TextBox("Author", GridVisible = true, GridOrder = 1, Required = true,
        Sample = "alice")]
    public string Author { get; set; } = "";

    [TextBox("Body", Multiline = true, Required = true,
        GridVisible = true, GridOrder = 2,
        Sample = "Anyone seen the deployment script?")]
    public string Body { get; set; } = "";

    [DateBox("Posted", Disabled = true, GridVisible = true, GridOrder = 3, IncludeTime = true)]
    public DateTime PostedAt { get; set; }
}
