namespace LibreLms.Contracts.Management;

/// <summary>
/// The target of an admin operation is outside the caller's org scope
/// (ADR 0010). The Host maps this to a JSON 403 — never Forbid() (cookie
/// auth would 302 to the access-denied page).
/// </summary>
public class ForbiddenAccessException(string message) : Exception(message);
