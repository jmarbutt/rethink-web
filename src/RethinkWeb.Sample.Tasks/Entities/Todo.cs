using System.ComponentModel.DataAnnotations;
using RethinkWeb.Metadata;

namespace RethinkWeb.Sample.Tasks.Entities;

/// <summary>
/// Named "Todo" rather than "Task" to avoid colliding with System.Threading.Tasks.Task.
/// CompletedAt is auto-stamped by StampCompletedAtSubscriber when Completed flips true.
/// </summary>
[Entity(slug: "tasks", displayName: "Tasks")]
public class Todo
{
    [Key]
    public Guid Id { get; set; }

    [TextBox("Title", GridVisible = true, GridOrder = 1, Required = true,
        Sample = "Write the docs")]
    public string Title { get; set; } = "";

    [TextBox("Notes", Multiline = true)]
    public string? Notes { get; set; }

    [CheckBox("Completed", GridVisible = true, GridOrder = 2)]
    public bool Completed { get; set; }

    [DateBox("Completed At", Disabled = true, GridVisible = true, GridOrder = 3)]
    public DateTime? CompletedAt { get; set; }
}
