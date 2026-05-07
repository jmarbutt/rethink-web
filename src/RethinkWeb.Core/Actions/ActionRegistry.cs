using System.Reflection;

namespace RethinkWeb.Actions;

public interface IActionRegistry
{
    IReadOnlyList<ActionDescriptor> ForEntity(Type entityType);
    ActionDescriptor? Find(Type entityType, string actionName);
    IReadOnlyCollection<ActionDescriptor> All { get; }
}

public sealed class ActionRegistry : IActionRegistry
{
    private readonly List<ActionDescriptor> _all = [];

    public void Register(Type implementationType)
    {
        var actionAttr = implementationType.GetCustomAttribute<ActionAttribute>()
            ?? throw new InvalidOperationException(
                $"Action class {implementationType.FullName} is missing [Action(name, displayName)].");

        var iface = implementationType
            .GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IAction<,,>))
            ?? throw new InvalidOperationException(
                $"Action class {implementationType.FullName} must implement IAction<TEntity, TInput, TOutput>.");

        var args = iface.GetGenericArguments();

        _all.Add(new ActionDescriptor
        {
            Name = actionAttr.Name,
            DisplayName = actionAttr.DisplayName,
            Description = actionAttr.Description,
            Permission = actionAttr.Permission,
            Icon = actionAttr.Icon,
            ExposeToMcp = actionAttr.ExposeToMcp,
            EntityType = args[0],
            InputType = args[1],
            OutputType = args[2],
            ImplementationType = implementationType,
        });
    }

    public IReadOnlyList<ActionDescriptor> ForEntity(Type entityType) =>
        [.. _all.Where(a => a.EntityType == entityType)];

    public ActionDescriptor? Find(Type entityType, string actionName) =>
        _all.FirstOrDefault(a =>
            a.EntityType == entityType
            && string.Equals(a.Name, actionName, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyCollection<ActionDescriptor> All => _all;
}
