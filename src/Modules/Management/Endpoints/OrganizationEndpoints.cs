namespace LibreLms.Modules.Management.Endpoints;

/// <summary>
/// DTO for a single node in the organization chart SVG.
/// Contains layout positions computed by the tree layout algorithm,
/// along with summary counts for users and courses.
/// </summary>
public record OrgChartNodeDto(
    Guid Id,
    string Name,
    string? Description,
    int Depth,
    int X,
    int Y,
    bool IsDisabled,
    bool IsRoot,
    int UserCount,
    int CourseCount,
    bool HasChildren,
    Guid? ParentId
);

/// <summary>DTO for organization listing.</summary>
public record OrganizationDto(
    Guid Id,
    string Name,
    string? Description,
    Guid? ParentId,
    DateTimeOffset CreatedAt
);

/// <summary>Request body for creating an organization.</summary>
public record CreateOrganizationRequest(
    string Name,
    string? Description,
    Guid? ParentId
);

/// <summary>Request body for updating an organization.</summary>
public record UpdateOrganizationRequest(
    string Name,
    string? Description
);

/// <summary>Minimal organization info for parent selection.</summary>
public record OrganizationPickerDto(
    Guid Id,
    string Name
);

/// <summary>
/// Marker for the Management module's endpoint definitions.
/// Actual endpoint mapping is done in Program.cs (Host project).
/// </summary>
public static class OrganizationEndpoints
{
}
