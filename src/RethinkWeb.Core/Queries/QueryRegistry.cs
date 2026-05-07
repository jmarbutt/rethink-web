using System.Reflection;

namespace RethinkWeb.Queries;

public interface IQueryRegistry
{
    QueryDescriptor? Find(string queryName);
    IReadOnlyCollection<QueryDescriptor> All { get; }
}

public sealed class QueryRegistry : IQueryRegistry
{
    private readonly List<QueryDescriptor> _all = [];

    public void Register(Type implementationType)
    {
        var queryAttr = implementationType.GetCustomAttribute<QueryAttribute>()
            ?? throw new InvalidOperationException(
                $"Query class {implementationType.FullName} is missing [Query(name, displayName)].");

        var iface = implementationType
            .GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQuery<,>))
            ?? throw new InvalidOperationException(
                $"Query class {implementationType.FullName} must implement IQuery<TInput, TOutput>.");

        var args = iface.GetGenericArguments();
        TimeSpan? duration = queryAttr.CacheSeconds > 0
            ? TimeSpan.FromSeconds(queryAttr.CacheSeconds)
            : null;

        _all.Add(new QueryDescriptor
        {
            Name = queryAttr.Name,
            DisplayName = queryAttr.DisplayName,
            Description = queryAttr.Description,
            Permission = queryAttr.Permission,
            ExposeToMcp = queryAttr.ExposeToMcp,
            InputType = args[0],
            OutputType = args[1],
            ImplementationType = implementationType,
            CachePolicy = new QueryCachePolicy(
                queryAttr.Cache,
                duration,
                queryAttr.DependsOn),
        });
    }

    public QueryDescriptor? Find(string queryName) =>
        _all.FirstOrDefault(q =>
            string.Equals(q.Name, queryName, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyCollection<QueryDescriptor> All => _all;
}
