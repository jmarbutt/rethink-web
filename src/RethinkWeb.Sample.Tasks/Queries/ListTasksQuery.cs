using RethinkWeb.Queries;
using RethinkWeb.Sample.Tasks.Entities;
using RethinkWeb.Storage;

namespace RethinkWeb.Sample.Tasks.Queries;

public sealed record ListTasksInput
{
    public bool? IncludeCompleted { get; init; }
}

public sealed record TaskListRow(Guid Id, string Title, bool Completed, DateTime? CompletedAt);

public sealed record ListTasksResult(IReadOnlyList<TaskListRow> Tasks);

[Query(
    name: "tasks.list",
    displayName: "List Tasks",
    Description = "List tasks with an optional completed-task filter.",
    Cache = QueryCacheMode.PerTenant,
    CacheSeconds = 30,
    DependsOn = ["tasks"])]
public sealed class ListTasksQuery(IEntityStore<Todo> store)
    : IQuery<ListTasksInput, ListTasksResult>
{
    public async Task<ListTasksResult> ExecuteAsync(
        ListTasksInput input,
        IQueryContext context,
        CancellationToken ct = default)
    {
        var includeCompleted = input.IncludeCompleted ?? true;
        var tasks = await store.ListAsync(ct);
        var rows = tasks
            .Where(t => includeCompleted || !t.Completed)
            .OrderBy(t => t.Completed)
            .ThenBy(t => t.Title)
            .Select(t => new TaskListRow(t.Id, t.Title, t.Completed, t.CompletedAt))
            .ToList();

        return new ListTasksResult(rows);
    }
}
