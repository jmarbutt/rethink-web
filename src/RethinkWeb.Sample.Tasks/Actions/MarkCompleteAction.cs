using RethinkWeb.Actions;
using RethinkWeb.Sample.Tasks.Entities;
using RethinkWeb.Storage;

namespace RethinkWeb.Sample.Tasks.Actions;

/// <summary>
/// Empty input record — the action takes no parameters beyond the entity itself.
/// MCP tool calls just pass <c>entityId</c>; HTTP form posts have no body fields.
/// </summary>
public sealed record MarkCompleteInput;

public sealed record MarkCompleteResult(Guid TodoId, bool AlreadyCompleted);

[Action(name: "mark-complete", displayName: "Mark Complete",
    Description = "Mark a task as completed. Idempotent — re-running on a completed task is a no-op.",
    Icon = "check")]
public sealed class MarkCompleteAction(IEntityStore<Todo> store)
    : IAction<Todo, MarkCompleteInput, MarkCompleteResult>
{
    public async Task<MarkCompleteResult> ExecuteAsync(
        Todo entity,
        MarkCompleteInput input,
        IActionContext context,
        CancellationToken ct = default)
    {
        if (entity.Completed)
        {
            return new MarkCompleteResult(entity.Id, AlreadyCompleted: true);
        }

        entity.Completed = true;
        // CompletedAt left null on purpose — StampCompletedAtSubscriber stamps it
        // when EntitySaved<Todo> fires. Demonstrates that the same rule fires
        // regardless of write path (HTML form, MCP, action endpoint, future workflow).
        await store.SaveAsync(entity, ct);
        return new MarkCompleteResult(entity.Id, AlreadyCompleted: false);
    }
}
