namespace LibreLms.Contracts.Management;

/// <summary>Minimal organization data exposed across module boundaries.</summary>
public record OrganizationSummary(
    Guid Id,
    string Name,
    string? Description,
    Guid? ParentId
);
