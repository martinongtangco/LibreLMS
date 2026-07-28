using System.Reflection;
using NetArchTest.Rules;

namespace ArchitectureTests;

/// <summary>
/// Turns constitution Principle III ("Module Boundaries Are Compiled, Not Conventional")
/// into an actual failing build. A module may depend on another module's *.Contracts
/// namespace, never on its Domain/Application/Infrastructure/Endpoints namespace.
/// </summary>
public class ModuleBoundaryTests
{
    private static readonly (string Name, Assembly Assembly, string InternalNamespace)[] Modules =
    [
        ("Catalog", typeof(LearningLms.Modules.Catalog.ModuleMarker).Assembly, "LearningLms.Modules.Catalog"),
        ("Enrollment", typeof(LearningLms.Modules.Enrollment.ModuleMarker).Assembly, "LearningLms.Modules.Enrollment"),
        ("Scorm", typeof(LearningLms.Modules.Scorm.ModuleMarker).Assembly, "LearningLms.Modules.Scorm"),
    ];

    public static IEnumerable<object[]> ModulePairs()
    {
        foreach (var module in Modules)
        foreach (var other in Modules)
        {
            if (module.Name != other.Name)
                yield return [module, other];
        }
    }

    [Theory]
    [MemberData(nameof(ModulePairs))]
    public void Module_Must_Not_Reference_Another_Modules_Internals(
        (string Name, Assembly Assembly, string InternalNamespace) module,
        (string Name, Assembly Assembly, string InternalNamespace) other)
    {
        var result = Types.InAssembly(module.Assembly)
            .That().ResideInNamespace(module.InternalNamespace)
            .ShouldNot().HaveDependencyOn(other.InternalNamespace)
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            $"{module.Name} must only reference {other.Name}.Contracts, never {other.InternalNamespace} directly. " +
            $"Violating types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }
}
