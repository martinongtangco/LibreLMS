namespace LibreLms.Contracts.Catalog;

/// <summary>
/// No behavior yet — this project is the ONLY thing other modules are allowed to
/// reference from Catalog (see ArchitectureTests). It will hold DTOs/interfaces like
/// "ICourseSummaryLookup" once a slice needs cross-module access to Catalog data.
/// </summary>
public sealed class ModuleMarker;
