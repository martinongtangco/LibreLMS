using LibreLms.Contracts.Management;
using LibreLms.Host;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Host.Tests;

/// <summary>
/// Spec 058 US3 (FR-003, ADR 0014): pins the four-row exception->HTTP mapping
/// of ManagementErrors.Translate — status codes AND body shapes — plus the
/// rethrow contract (anything outside the mapped set keeps the 500 path).
/// </summary>
public class ManagementErrorsTests
{
    // Reflection accessors: the exact IResult subtype for a 400-with-body is an
    // implementation detail; the contract is the (StatusCode, Value) pair.
    private static int StatusOf(IResult result)
    {
        if (result is IStatusCodeHttpResult sr)
            return sr.StatusCode ?? -1;
        var prop = result.GetType().GetProperty("StatusCode");
        return prop is null ? -1 : (int)prop.GetValue(result)!;
    }

    private static object? BodyOf(IResult result)
        => result.GetType().GetProperty("Value")?.GetValue(result);

    private static string? ErrorOf(IResult result)
    {
        var body = BodyOf(result);
        if (body is null)
            return null;
        return body.GetType().GetProperty("error")?.GetValue(body) as string;
    }

    [Fact]
    public void ForbiddenAccessException_maps_to_403_json_error_body()
    {
        var result = ManagementErrors.Translate(new ForbiddenAccessException("not in your org"));

        Assert.Equal(StatusCodes.Status403Forbidden, StatusOf(result));
        Assert.Equal("not in your org", ErrorOf(result));
    }

    [Fact]
    public void KeyNotFoundException_maps_to_404_with_no_body()
    {
        var result = ManagementErrors.Translate(new KeyNotFoundException("no such row"));

        Assert.Equal(StatusCodes.Status404NotFound, StatusOf(result));
        Assert.Null(BodyOf(result));
    }

    [Fact]
    public void InvalidOperationException_maps_to_400_json_error_body()
    {
        var result = ManagementErrors.Translate(new InvalidOperationException("cannot delete"));

        Assert.Equal(StatusCodes.Status400BadRequest, StatusOf(result));
        Assert.Equal("cannot delete", ErrorOf(result));
    }

    [Fact]
    public void ArgumentException_maps_to_400_json_error_body()
    {
        var result = ManagementErrors.Translate(new ArgumentException("bad name"));

        Assert.Equal(StatusCodes.Status400BadRequest, StatusOf(result));
        Assert.Equal("bad name", ErrorOf(result));
    }

    [Fact]
    public void Unmapped_exception_rethrows_the_same_instance()
    {
        var unrelated = new NotSupportedException("not a business failure");

        // Anything outside the four mapped types keeps the 500 path —
        // the same instance must escape (no wrapping, no swallowing).
        Assert.Same(unrelated, Assert.Throws<NotSupportedException>(() => ManagementErrors.Translate(unrelated)));
    }
}
