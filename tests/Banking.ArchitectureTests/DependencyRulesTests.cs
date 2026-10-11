namespace Banking.ArchitectureTests;

public sealed class DependencyRulesTests
{
    public static TheoryData<string> ComponentNames => new(Solution.Components.Select(component => component.Name));

    [Fact]
    public void Discovery_finds_the_production_assemblies()
    {
        Solution.Components.Select(component => component.Name).ShouldContain("Banking.Modules.Ledger.Domain");
        Solution.Components.Select(component => component.Name).ShouldContain("Banking.Api");
    }

    [Theory]
    [MemberData(nameof(ComponentNames))]
    public void Component_depends_only_on_what_the_architecture_allows(string name)
    {
        var component = Solution.Find(name);

        var violations = component.Dependencies
            .Select(Solution.Find)
            .Where(dependency => !DependencyRules.IsAllowed(component, dependency))
            .Select(dependency => dependency.Name)
            .ToList();

        violations.ShouldBeEmpty($"{name} has forbidden dependencies");
    }
}
