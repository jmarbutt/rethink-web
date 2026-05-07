using RethinkWeb.Events;
using RethinkWeb.Sample.Tasks.Entities;
using RethinkWeb.Storage;

namespace RethinkWeb.Sample.Tasks.Events;

/// <summary>
/// Stamps Todo.CompletedAt when Completed flips true. Lives here (not inside
/// MarkCompleteAction) so the rule fires regardless of write path:
///   - User checks the box in the web form → save → this fires
///   - MarkComplete action invoked via HTTP /actions endpoint → save → this fires
///   - MCP client invokes mark-complete tool → save → this fires
///   - Future workflow step that completes a task → save → this fires
///
/// The recursion guard inside PublishingEntityStore prevents the inner save
/// (SaveAsync below) from re-publishing EntitySaved.
/// </summary>
public sealed class StampCompletedAtSubscriber(
    IEntityStore<Todo> store,
    IClock clock) : IEventSubscriber<EntitySaved<Todo>>
{
    public async Task HandleAsync(EntitySaved<Todo> evt, IEventContext context, CancellationToken ct)
    {
        var todo = evt.Entity;
        if (todo.Completed && todo.CompletedAt is null)
        {
            todo.CompletedAt = clock.UtcNow.UtcDateTime;
            await store.SaveAsync(todo, ct);
        }
        else if (!todo.Completed && todo.CompletedAt is not null)
        {
            // Un-completing a task clears the timestamp.
            todo.CompletedAt = null;
            await store.SaveAsync(todo, ct);
        }
    }
}
