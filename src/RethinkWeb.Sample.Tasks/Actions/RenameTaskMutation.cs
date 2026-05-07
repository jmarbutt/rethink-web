using RethinkWeb.Mutations;
using RethinkWeb.Sample.Tasks.Entities;
using RethinkWeb.Storage;

namespace RethinkWeb.Sample.Tasks.Actions;

public sealed record RenameTaskInput(string Title);

public sealed record RenameTaskResult(Guid TodoId, string Title);

[Mutation(
    name: "rename",
    displayName: "Rename Task",
    Description = "Rename a task.",
    Icon = "pencil")]
public sealed class RenameTaskMutation(IEntityStore<Todo> store)
    : IMutation<Todo, RenameTaskInput, RenameTaskResult>
{
    public async Task<RenameTaskResult> ExecuteAsync(
        Todo entity,
        RenameTaskInput input,
        IMutationContext context,
        CancellationToken ct = default)
    {
        entity.Title = input.Title;
        await store.SaveAsync(entity, ct);
        return new RenameTaskResult(entity.Id, entity.Title);
    }
}
