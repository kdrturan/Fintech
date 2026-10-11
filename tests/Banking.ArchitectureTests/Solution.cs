using System.Reflection;

namespace Banking.ArchitectureTests;

internal static class Solution
{
    public static IReadOnlyList<Component> Components { get; } = Discover();

    public static Component Find(string name) =>
        Components.SingleOrDefault(component => component.Name == name)
        ?? throw new InvalidOperationException($"'{name}' is not a known production assembly.");

    private static List<Component> Discover() =>
        Directory.EnumerateFiles(AppContext.BaseDirectory, $"{Component.SolutionPrefix}*.dll")
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .Where(name => !name.EndsWith("Tests", StringComparison.Ordinal))
            .Select(name => Component.From(Assembly.Load(name)))
            .OrderBy(component => component.Name, StringComparer.Ordinal)
            .ToList();
}
