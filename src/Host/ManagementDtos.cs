namespace LibreLms.Host;

/// <summary>DTOs used by Management module endpoints in Program.cs.</summary>
public static class ManagementDtos
{
    public record CreateUserRequest(string Name, string Email, string Password, string Role, Guid OrganizationId);
    public record UpdateUserRequest(string? Name, string? Role, Guid? OrganizationId);
    public record UserCreatedDto(Guid Id, string Name, string Email, string Role, Guid OrganizationId);
    public record UserUpdatedDto(Guid Id, string Name, string Email, string Role, Guid OrganizationId);
}
