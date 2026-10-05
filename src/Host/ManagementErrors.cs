using LibreLms.Contracts.Management;

namespace LibreLms.Host;

/// <summary>
/// ADR 0014: the single translation point for Management's expected business
/// exceptions into their HTTP responses. Management Application services throw
/// typed exceptions; endpoint handlers catch their (per-handler, unchanged)
/// type sets and delegate here.
///
/// The mapping is test-pinned in tests/Host.Tests/ManagementErrorsTests.cs:
/// ForbiddenAccessException -> 403 JSON {error}; BCL KeyNotFoundException ->
/// 404 no body; InvalidOperationException / ArgumentException -> 400 JSON {error}.
/// Anything else rethrows — this method must never widen a handler's previous
/// 500 path into a translated response.
/// </summary>
public static class ManagementErrors
{
    public static IResult Translate(Exception ex) => ex switch
    {
        // A real 403 JSON (never Results.Forbid() — cookie auth 302s it).
        ForbiddenAccessException fae =>
            Results.Json(new { error = fae.Message }, statusCode: StatusCodes.Status403Forbidden),

        // Missing entity — house shape is a bare 404 (no body).
        KeyNotFoundException =>
            Results.NotFound(),

        // Invalid operation / invalid argument.
        InvalidOperationException io =>
            Results.BadRequest(new { error = io.Message }),

        ArgumentException ae =>
            Results.BadRequest(new { error = ae.Message }),

        _ => throw ex,
    };
}
