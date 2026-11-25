namespace Banking.ArchitectureTests;

/// <summary>
/// The allowed dependency graph. See docs/adr/0003, docs/adr/0004 and docs/adr/0013.
/// </summary>
internal static class DependencyRules
{
    public static bool IsAllowed(Component from, Component to) => from.Layer switch
    {
        // Pure building blocks: depend on nothing else in the solution.
        Layer.SharedKernel => false,

        // HTTP building blocks (error responses, idempotency) shared by every module's controllers.
        Layer.Web => to.Layer == Layer.SharedKernel,

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

        // Controllers: HTTP in, MediatR requests of their own module out. Never the database, never the domain.
        // Contracts for the role names of the Identity module. They are constants, which the compiler copies, so this
        // dependency does not even show up in the IL that these tests read.
        Layer.Presentation =>
            to.Layer is Layer.SharedKernel or Layer.Web or Layer.Contracts
            || (from.IsInSameModuleAs(to) && to.Layer is Layer.Application),

        // Composition root wires modules together through their Infrastructure and Presentation entry points.
        Layer.Host => to.Layer is Layer.SharedKernel or Layer.Web or Layer.Contracts or Layer.Infrastructure or Layer.Presentation,

        _ => throw new ArgumentOutOfRangeException(nameof(from), from.Layer, "Unknown layer."),
    };
}
