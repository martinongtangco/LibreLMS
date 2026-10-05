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
        ("Catalog", typeof(LibreLms.Modules.Catalog.ModuleMarker).Assembly, "LibreLms.Modules.Catalog"),
        ("Enrollment", typeof(LibreLms.Modules.Enrollment.ModuleMarker).Assembly, "LibreLms.Modules.Enrollment"),
        ("Scorm", typeof(LibreLms.Modules.Scorm.ModuleMarker).Assembly, "LibreLms.Modules.Scorm"),
        ("Management", typeof(LibreLms.Modules.Management.ModuleMarker).Assembly, "LibreLms.Modules.Management"),
    ];

    // spec 058 P4 (T020): the Contracts faces, keyed by module name.
    private static readonly (string Name, Assembly Contracts)[] ContractsAssemblies =
    [
        ("Catalog", typeof(LibreLms.Contracts.Catalog.ModuleMarker).Assembly),
        ("Enrollment", typeof(LibreLms.Contracts.Enrollment.ModuleMarker).Assembly),
        ("Scorm", typeof(LibreLms.Contracts.Scorm.ModuleMarker).Assembly),
        ("Management", typeof(LibreLms.Contracts.Management.ModuleMarker).Assembly),
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

    /// <summary>
    /// spec 058 P4 (T020a): a Contracts face must not depend on its own module's
    /// internals — the contract is the stable surface; if it reaches into
    /// Domain/Application, it has leaked implementation (research R9).
    /// </summary>
    [Theory]
    [MemberData(nameof(AllContracts))]
    public void Contracts_Must_Not_Depend_On_Own_Module_Internals(string name, Assembly contractsAssembly)
    {
        var result = Types.InAssembly(contractsAssembly)
            .That().ResideInNamespace($"LibreLms.Contracts.{name}")
            .ShouldNot().HaveDependencyOn($"LibreLms.Modules.{name}")
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            $"Contracts.{name} must not depend on {name} internals. " +
            $"Violating types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    public static IEnumerable<object[]> AllContracts()
    {
        foreach (var c in ContractsAssemblies)
            yield return [c.Name, c.Contracts];
    }

    /// <summary>
    /// spec 058 P4 (T020b): SharedKernel is the bottom of the dependency graph —
    /// it may depend on nothing in the app (no module, no Contracts).
    /// </summary>
    [Fact]
    public void SharedKernel_Must_Not_Depend_On_Any_Module_Or_Contracts()
    {
        var sharedKernel = typeof(LibreLms.SharedKernel.Entity<Guid>).Assembly;
        foreach (var rootNamespace in new[] { "LibreLms.Modules", "LibreLms.Contracts" })
        {
            var result = Types.InAssembly(sharedKernel)
                .ShouldNot().HaveDependencyOn(rootNamespace)
                .GetResult();

            Assert.True(
                result.IsSuccessful,
                $"SharedKernel must not depend on {rootNamespace}.".TrimEnd('.') + ". " +
                $"Violating types: {string.Join(", ", result.FailingTypeNames ?? [])}");
        }
    }
}
