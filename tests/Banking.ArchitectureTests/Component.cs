using System.Reflection;

namespace Banking.ArchitectureTests;

internal enum Layer
{
    SharedKernel,
    Domain,
    Application,
    Infrastructure,
    Contracts,
    Host,
}

internal sealed record Component(string Name, string? Module, Layer Layer, IReadOnlyList<string> Dependencies)
{
    public const string SolutionPrefix = "Banking.";

    public static Component From(Assembly assembly)
    {
        var name = assembly.GetName().Name!;
        var dependencies = assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Where(reference => reference.StartsWith(SolutionPrefix, StringComparison.Ordinal))
            .ToList();

        return name.Split('.') switch
        {
            ["Banking", "Modules", var module, var layer] => new(name, module, Enum.Parse<Layer>(layer), dependencies),
            ["Banking", "SharedKernel"] => new(name, null, Layer.SharedKernel, dependencies),
            ["Banking", "Api"] => new(name, null, Layer.Host, dependencies),
            _ => throw new InvalidOperationException(
                $"Assembly '{name}' does not follow a known naming convention. Classify it in {nameof(Component)}.{nameof(From)}."),
        };
    }

    public bool IsInSameModuleAs(Component other) => Module is not null && Module == other.Module;
}
