using LibreLms.Contracts.Enrollment;
using LibreLms.Contracts.Management;
using LibreLms.Modules.Management.Application;
using Xunit;
using EnrollmentUserScopeInfo = LibreLms.Contracts.Enrollment.UserScopeInfo;

namespace Management.Tests;

/// <summary>
/// Spec 058 US2 (FR-002): unit coverage for UserInfoLookup (previously untested).
/// Verifies the cross-module boundary: Enrollment.UserScopeInfo is mapped onto
/// the Management.UserScopeInfo record, and null passes through unchanged
/// (spec 027 R9 — Management no longer touches EnrollmentDbContext).
/// </summary>
public class UserInfoLookupTests
{
    private sealed class FakeEnrollmentUserLookup : IUserLookup
    {
        private readonly Dictionary<Guid, EnrollmentUserScopeInfo?> _users;
        public List<Guid> Calls { get; } = new();

        public FakeEnrollmentUserLookup(Dictionary<Guid, EnrollmentUserScopeInfo?> users) => _users = users;

        public Task<EnrollmentUserScopeInfo?> GetUserScopeAsync(Guid studentId)
        {
            Calls.Add(studentId);
            return Task.FromResult(_users.TryGetValue(studentId, out var scope) ? scope : null);
        }

        public Task<int> CountLearnersAsync(Guid? organizationId = null) => Task.FromResult(0);
        public Task<IList<OrgLearnerCount>> GetLearnerCountsByOrgAsync() => Task.FromResult<IList<OrgLearnerCount>>(Array.Empty<OrgLearnerCount>());
        public Task<string?> GetUserNameAsync(Guid studentId) => Task.FromResult<string?>(null);
        public Task<IList<UserSummary>> GetUsersAsync(IEnumerable<Guid> studentIds) => Task.FromResult<IList<UserSummary>>(Array.Empty<UserSummary>());
        public Task<int> CountByRoleAsync(string role) => Task.FromResult(0);
    }

    [Fact]
    public async Task GetUserScopeAsync_maps_enrollment_scope_to_management_contract()
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var fake = new FakeEnrollmentUserLookup(new()
        {
            [userId] = new EnrollmentUserScopeInfo(orgId, "OrgAdmin"),
        });
        var sut = new UserInfoLookup(fake);

        var got = await sut.GetUserScopeAsync(userId);

        Assert.NotNull(got);
        Assert.Equal(orgId, got!.OrganizationId);
        Assert.Equal("OrgAdmin", got.Role);
        // The boundary call used the exact user id.
        Assert.Equal(new[] { userId }, fake.Calls);
    }

    [Fact]
    public async Task GetUserScopeAsync_null_scope_passes_through_as_null()
    {
        var userId = Guid.NewGuid();
        var fake = new FakeEnrollmentUserLookup(new()
        {
            [userId] = null, // user exists in the map but has no scope row
        });
        var sut = new UserInfoLookup(fake);

        var got = await sut.GetUserScopeAsync(userId);

        Assert.Null(got);
    }

    [Fact]
    public async Task GetUserScopeAsync_unknown_user_returns_null()
    {
        var fake = new FakeEnrollmentUserLookup(new Dictionary<Guid, EnrollmentUserScopeInfo?>());
        var sut = new UserInfoLookup(fake);

        var got = await sut.GetUserScopeAsync(Guid.NewGuid());

        Assert.Null(got);
    }
}
