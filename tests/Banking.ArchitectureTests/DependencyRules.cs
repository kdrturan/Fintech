namespace Banking.ArchitectureTests;

internal static class DependencyRules
{
    public static bool IsAllowed(Component from, Component to) => from.Layer switch
    {
        // Pure building blocks: depend on nothing else in the solution.
        Layer.SharedKernel => false,

        // Contracts cross module (and later process) boundaries, so they carry primitives only.
        Layer.Contracts => false,

        // The domain model knows nothing about use cases, persistence or other modules.
        Layer.Domain => to.Layer == Layer.SharedKernel,

        // Use cases: own domain, own contracts, and other modules' contracts only.
        Layer.Application =>
            to.Layer is Layer.SharedKernel or Layer.Contracts
            || (from.IsInSameModuleAs(to) && to.Layer is Layer.Domain),

        // Adapters: may see every inner layer of their own module.
        Layer.Infrastructure =>
            to.Layer is Layer.SharedKernel or Layer.Contracts
            || (from.IsInSameModuleAs(to) && to.Layer is Layer.Domain or Layer.Application),

        // Composition root wires modules together through their Infrastructure entry points.
        Layer.Host => to.Layer is Layer.SharedKernel or Layer.Contracts or Layer.Infrastructure,

        _ => throw new ArgumentOutOfRangeException(nameof(from), from.Layer, "Unknown layer."),
    };
}
