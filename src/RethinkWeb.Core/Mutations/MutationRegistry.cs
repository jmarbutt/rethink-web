using System.Reflection;

namespace RethinkWeb.Mutations;

public interface IMutationRegistry
{
    IReadOnlyList<MutationDescriptor> ForEntity(Type entityType);
    MutationDescriptor? Find(Type entityType, string mutationName);
    IReadOnlyCollection<MutationDescriptor> All { get; }
}

public sealed class MutationRegistry : IMutationRegistry
{
    private readonly List<MutationDescriptor> _all = [];

    public void Register(Type implementationType)
    {
        var mutationAttr = implementationType.GetCustomAttribute<MutationAttribute>()
            ?? throw new InvalidOperationException(
                $"Mutation class {implementationType.FullName} is missing [Mutation(name, displayName)].");

        var iface = implementationType
            .GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IMutation<,,>))
            ?? throw new InvalidOperationException(
                $"Mutation class {implementationType.FullName} must implement IMutation<TEntity, TInput, TOutput>.");

        var args = iface.GetGenericArguments();

        _all.Add(new MutationDescriptor
        {
            Name = mutationAttr.Name,
            DisplayName = mutationAttr.DisplayName,
            Description = mutationAttr.Description,
            Permission = mutationAttr.Permission,
            Icon = mutationAttr.Icon,
            ExposeToMcp = mutationAttr.ExposeToMcp,
            EntityType = args[0],
            InputType = args[1],
            OutputType = args[2],
            ImplementationType = implementationType,
        });
    }

    public IReadOnlyList<MutationDescriptor> ForEntity(Type entityType) =>
        [.. _all.Where(a => a.EntityType == entityType)];

    public MutationDescriptor? Find(Type entityType, string mutationName) =>
        _all.FirstOrDefault(a =>
            a.EntityType == entityType
            && string.Equals(a.Name, mutationName, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyCollection<MutationDescriptor> All => _all;
}
